using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackExchange.Redis;
using System.Net;
using System.Threading;

namespace HackerSpray.Module
{
    public class RedisDefenceStore : IDefenceStore
    {
        private const string BLACKLIST_KEY = "BLACKLISTS-KEY";
        private const string BLACKLIST_KEY_SET = "BLACKLISTS-KEY-SET";
        private const string BLACKLIST_ORIGIN = "BLACKLISTS-ORIGIN";
        private const string BLACKLIST_ORIGIN_SET = "BLACKLISTS-ORIGIN-SET";
        private const string BLACKLIST_ORIGIN_RANGE = "BLACKLISTS-ORIGIN-RANGE";
        private const string KEY_LIST = "KEYLIST";
        private const long KEY_LIST_LENGTH = 1000000;

        private static object lockObject = new object();
        private static ConnectionMultiplexer redis;
        private IDatabase db;
        private string prefix;
        private DefenceConfig config;

        public RedisDefenceStore(string connectionString, string prefix, DefenceConfig config)
        {
            if (redis == null)
            {
                lock(lockObject)
                {
                    if(redis == null)
                        redis = ConnectionMultiplexer.Connect(connectionString);

                    Thread.Sleep(1000);
                }
            }

            this.db = redis.GetDatabase();
            this.prefix = prefix;
            this.config = config;
        }

        void IDisposable.Dispose()
        {
            // Nothing to dispose
        }

        Task<bool> IDefenceStore.BlacklistKey(string key, TimeSpan expiry)
        {          
            return this.db.SetAddAsync(this.prefix + BLACKLIST_KEY_SET, key).ContinueWith(t =>
                this.db.StringSetAsync(this.prefix + BLACKLIST_KEY + ':' + key, 1, expiry)).Unwrap();
        }

        Task<bool> IDefenceStore.BlacklistOrigin(IPAddress origin, TimeSpan expiry)
        {
            var originValue = IP2Number(origin);

            return this.db.SetAddAsync(this.prefix + BLACKLIST_ORIGIN_SET, originValue).ContinueWith(t => 
                this.db.StringSetAsync(this.prefix + BLACKLIST_ORIGIN + ':' + originValue, 1, expiry)).Unwrap();
        }

        async Task<bool> IDefenceStore.BlacklistOrigin(IPAddress start, IPAddress end)
        {
            var originStart = IP2Number(start);
            var originEnd = IP2Number(end);

            // for empty sorted set, we need to add an item with 0 score
            if (await this.db.SortedSetLengthAsync(this.prefix + BLACKLIST_ORIGIN_RANGE) == 0)
                await this.db.SortedSetAddAsync(this.prefix + BLACKLIST_ORIGIN_RANGE, "0-0", 0);
            
            return await this.db.SortedSetAddAsync(this.prefix + BLACKLIST_ORIGIN_RANGE, originStart + "-" + originEnd, originStart);
        }

        async Task<long> IDefenceStore.GetHitsForKey(string key)
        {
            var keyKey = prefix + "key:" + key;
            var result = await this.db.StringGetAsync(keyKey);
            long count;
            return result.TryParse(out count) ? count : 0;
        }

        async Task<long> IDefenceStore.GetHitsFromOrigin(IPAddress origin)
        {
            var originkey = prefix + "origin:" + origin.ToString();
            var result = await this.db.StringGetAsync(originkey);
            long count;
            return result.TryParse(out count) ? count : 0;
        }

        async Task<string[]> IDefenceStore.GetKeyBlacklists()
        {
            RedisValue[] values = await this.db.SetMembersAsync(this.prefix + BLACKLIST_KEY_SET);
            return values.ToStringArray();
        }

        async Task<string[]> IDefenceStore.GetOriginBlacklists(IPAddress origin)
        {
            RedisValue[] values = await this.db.SortedSetRangeByScoreAsync(this.prefix + BLACKLIST_ORIGIN);
            return values.ToStringArray();
        }

        Task<HitStats> IDefenceStore.IncrementHit(string key, IPAddress origin)
        {
            return ((IDefenceStore)this).IncrementHit(key, origin, 
                this.config.MaxHitsPerKeyInterval,
                this.config.MaxHitsPerOriginInterval,
                this.config.MaxHitsPerKeyPerOriginInterval);
        }
        async Task<HitStats> IDefenceStore.IncrementHit(string key, IPAddress origin, 
            TimeSpan keyInterval, TimeSpan originInterval, TimeSpan keyOriginInterval)
        {
            var stats = new HitStats
            {
                HitsFromOrigin = 0,
                HitsOnKeyFromOrigin = 0
            };

            var originValue = origin.ToString();

            var originkey = prefix + "origin:" + originValue;
            var keyKey = prefix + "key:" + key;
            var keyoriginkey = prefix + "key:" + key + ":origin:" + originValue;
            var originTask = this.db.StringIncrementAsync(originkey);
            var keyTask = this.db.StringIncrementAsync(keyKey);
            var keyOriginTask = this.db.StringIncrementAsync(keyoriginkey);

            await Task.WhenAll(originTask, keyTask, keyOriginTask);

            stats.HitsFromOrigin = originTask.Result;
            stats.HitsOnKey = keyTask.Result;
            stats.HitsOnKeyFromOrigin = keyOriginTask.Result;

            var now = DateTime.Now;
            // If any of the counter was created for the first time,
            // need to set expiration time for them.
            var writeTasks = new List<Task>();
            if (originTask.Result == 1 && originInterval != TimeSpan.MaxValue)
            {
                writeTasks.Add(this.db.KeyExpireAsync(originkey, now + originInterval));
                writeTasks.Add(RecordNewKey(originkey));
            }
            if (keyTask.Result == 1 && keyInterval != TimeSpan.MaxValue)
            {
                writeTasks.Add(this.db.KeyExpireAsync(keyKey, now + keyInterval));
                writeTasks.Add(RecordNewKey(keyKey));
            }
            if (keyOriginTask.Result == 1 && keyOriginInterval != TimeSpan.MaxValue)
            {
                writeTasks.Add(this.db.KeyExpireAsync(keyoriginkey, now + keyOriginInterval));
                writeTasks.Add(RecordNewKey(keyoriginkey));
            }

            if (writeTasks.Count > 0)
                await Task.WhenAll(writeTasks);

            return stats;
        }

        /// <summary>
        /// Record all the keys that is ever created so that we can use
        /// it to perform a full cleanup. 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private Task<long> RecordNewKey(string key)
        {
            return this.db.ListTrimAsync(this.prefix + KEY_LIST, 0, KEY_LIST_LENGTH).ContinueWith(t =>
                this.db.ListLeftPushAsync(this.prefix + KEY_LIST, key)).Unwrap();
        }

        async Task<bool> IDefenceStore.IsKeyBlacklisted(string key)
        {
            return (await this.db.StringGetAsync(this.prefix + BLACKLIST_KEY + ':' + key) != RedisValue.Null);
        }

        async Task<bool> IDefenceStore.IsOriginBlacklisted(IPAddress origin)
        {
            var originValue = IP2Number(origin);

            // Check if the IP itself is blacklisted
            var task1 = this.db.StringGetAsync(this.prefix + BLACKLIST_ORIGIN + ':' + originValue);
            var task2 = this.db.SortedSetRangeByScoreAsync(this.prefix + BLACKLIST_ORIGIN_RANGE, start: originValue, stop: 1, take: 1, order: Order.Descending);

            if (await task1 != RedisValue.Null)
                return true;

            // Check if the IP falls in a blacklist range
            var rangeMap = await task2;

            if (rangeMap.Length > 0)
            {
                var range = rangeMap[0].ToString();
                var parts = range.Split('-');
                if (originValue < Convert.ToUInt32(parts[0]))
                    return false;
                if (originValue > Convert.ToUInt32(parts[1]))
                    return false;

                return true;
            }
            else
            {
                return false;
            }
        }

        private static UInt32 IP2Number(IPAddress origin)
        {
            byte[] bytes = origin.GetAddressBytes();
            Array.Reverse(bytes); // flip big-endian(network order) to little-endian
            uint intAddress = BitConverter.ToUInt32(bytes, 0);
            return intAddress;
        }

        Task<bool> IDefenceStore.WhitelistKey(string key)
        {
            return this.db.SetRemoveAsync(this.prefix + BLACKLIST_KEY_SET, key).ContinueWith(t =>
                this.db.KeyDeleteAsync(this.prefix + BLACKLIST_KEY + ':' + key)).Unwrap();
        }

        Task<bool> IDefenceStore.WhitelistOrigin(IPAddress origin)
        {
            var originKey = IP2Number(origin);
            return this.db.SetRemoveAsync(this.prefix + BLACKLIST_ORIGIN_SET, originKey).ContinueWith(t =>
                this.db.KeyDeleteAsync(this.prefix + BLACKLIST_ORIGIN + ':' + originKey)).Unwrap();
        }

        Task<bool> IDefenceStore.WhitelistOrigin(IPAddress start, IPAddress end)
        {
            var originStart = IP2Number(start);
            var originEnd = IP2Number(end);

            return this.db.SortedSetRemoveAsync(this.prefix + BLACKLIST_ORIGIN_RANGE, originStart + "-" + originEnd);            
        }

        async Task<bool> IDefenceStore.ClearBlacklists()
        {
            var keys = this.db.SetMembersAsync(this.prefix + BLACKLIST_KEY_SET);
            var origins = this.db.SetMembersAsync(this.prefix + BLACKLIST_ORIGIN_SET);
            
            await Task.WhenAll(keys, origins);

            List<Task> tasks = new List<Task>();
            foreach (var key in keys.Result)
                tasks.Add(this.db.KeyDeleteAsync(key.ToString()));
            foreach (var key in origins.Result)
                tasks.Add(this.db.KeyDeleteAsync(key.ToString()));
            
            tasks.Add(this.db.KeyDeleteAsync(this.prefix + BLACKLIST_ORIGIN_RANGE));
            tasks.Add(this.db.KeyDeleteAsync(this.prefix + BLACKLIST_KEY_SET));

            await Task.WhenAll(tasks);
            return true;
        }

        Task<bool> IDefenceStore.ClearAllHits()
        {
            var keys = new List<RedisKey>();
            List<Task> tasks = new List<Task>();
                
            foreach (var endpoint in redis.GetEndPoints())
            {
                // get the target server
                var server = redis.GetServer(endpoint);

                // show all keys in database 0 that include "foo" in their name
                foreach (var key in server.Keys(pattern: this.prefix+"key:*"))
                {
                    keys.Add(key);
                    tasks.Add(this.db.KeyDeleteAsync(key.ToString()));
                }
                foreach (var key in server.Keys(pattern: this.prefix + "origin:*"))
                {
                    keys.Add(key);
                    tasks.Add(this.db.KeyDeleteAsync(key.ToString()));
                }
            }
            tasks.Add(this.db.KeyDeleteAsync(this.prefix + KEY_LIST));
            return Task.WhenAll(tasks).ContinueWith(t => Task.FromResult(true)).Unwrap();
        }
    }
}
