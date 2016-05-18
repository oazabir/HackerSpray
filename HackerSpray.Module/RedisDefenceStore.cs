using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;
using System.Net;
using System.Threading;

namespace HackerSpray.Module
{
    public class RedisDefenceStore : IDefenceStore
    {
        private const string BLACKLIST_KEY = "BLACKLISTS-KEY-";
        private const string BLACKLIST_ORIGIN = "BLACKLISTS-ORIGIN-";
        private const string BLACKLIST_ORIGIN_RANGE = "BLACKLISTS-ORIGIN-RANGE-";

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

        Task<bool> IDefenceStore.BlacklistKey(string key, TimeSpan expiry)
        {
            return this.db.StringSetAsync(this.prefix + BLACKLIST_KEY + key, 1, expiry);
        }

        Task<bool> IDefenceStore.BlacklistOrigin(IPAddress origin, TimeSpan expiry)
        {
            var originValue = IP2Number(origin);

            return this.db.StringSetAsync(this.prefix + BLACKLIST_ORIGIN + originValue, 1, expiry);
        }

        Task<bool> IDefenceStore.BlacklistOrigin(IPAddress start, IPAddress end)
        {
            var originStart = IP2Number(start);
            var originEnd = IP2Number(end);

            return this.db.SortedSetAddAsync(this.prefix + BLACKLIST_ORIGIN_RANGE, originStart + "-" + originEnd, originStart);
        }

        async Task<long> IDefenceStore.GetHitsForKey(string key)
        {
            var keyKey = prefix + "key-" + key;
            var result = await this.db.StringGetAsync(keyKey);
            long count;
            return result.IsInteger && result.TryParse(out count) ? count : 0;
        }

        async Task<long> IDefenceStore.GetHitsFromOrigin(IPAddress origin)
        {
            var originkey = prefix + "origin-" + IP2Number(origin);
            var result = await this.db.StringGetAsync(originkey);
            long count;
            return result.IsInteger && result.TryParse(out count) ? count : 0;
        }

        Task<string[]> IDefenceStore.GetKeyBlacklists(string key)
        {
            throw new NotImplementedException();
        }

        async Task<string[]> IDefenceStore.GetOriginBlacklists(IPAddress origin)
        {
            RedisValue[] values = await this.db.SortedSetRangeByScoreAsync(this.prefix + BLACKLIST_ORIGIN);
            return Array.ConvertAll<RedisValue, string>(values, v => v.ToString());
        }

        Task<HitStats> IDefenceStore.IncrementHit(string key, IPAddress origin)
        {
            return ((IDefenceStore)this).IncrementHit(key, origin, this.config.MaxHitsPerKeyInterval);
        }
        async Task<HitStats> IDefenceStore.IncrementHit(string key, IPAddress origin, TimeSpan keyInterval)
        {
            var stats = new HitStats
            {
                HitsFromOrigin = 0,
                HitsOnKeyFromOrigin = 0
            };

            var originValue = origin.ToString();

            var originkey = prefix + "origin-" + originValue;
            var keyKey = prefix + "key-" + key;
            var keyoriginkey = prefix + "key-" + key + "-origin-" + originValue;
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
            if (originTask.Result == 1)
                writeTasks.Add(this.db.KeyExpireAsync(originkey, now + this.config.MaxHitsPerOriginInterval));
            if (keyTask.Result == 1)
                writeTasks.Add(this.db.KeyExpireAsync(keyKey, now + keyInterval));
            if (keyOriginTask.Result == 1)
                writeTasks.Add(this.db.KeyExpireAsync(keyoriginkey, now + this.config.MaxHitsPerKeyPerOriginInterval));

            if (writeTasks.Count > 0)
                await Task.WhenAll(writeTasks);

            return stats;
        }

        async Task<bool> IDefenceStore.IsKeyBlacklisted(string key)
        {
            return (await this.db.StringGetAsync(this.prefix + BLACKLIST_KEY + key) != RedisValue.Null);
        }

        async Task<bool> IDefenceStore.IsOriginBlacklisted(IPAddress origin)
        {
            int originValue = IP2Number(origin);

            // Check if the IP itself is blacklisted
            var task1 = this.db.StringGetAsync(this.prefix + BLACKLIST_ORIGIN + originValue);
            var task2 = this.db.SortedSetRangeByScoreAsync(this.prefix + BLACKLIST_ORIGIN, originValue, take: 1);

            if (await task1 != RedisValue.Null)
                return true;

            // Check if the IP falls in a blacklist range
            var rangeMap = await task2;

            if (rangeMap.Length > 0)
            {
                var range = rangeMap[0].ToString();
                var parts = range.Split('-');
                if (originValue < Convert.ToInt32(parts[0]))
                    return false;
                if (originValue > Convert.ToInt32(parts[1]))
                    return false;

                return true;
            }
            else
            {
                return false;
            }
        }

        private static int IP2Number(IPAddress origin)
        {
            return BitConverter.ToInt32(origin.GetAddressBytes(), 0);
        }

        Task<bool> IDefenceStore.WhitelistKey(string key)
        {
            return this.db.KeyDeleteAsync(this.prefix + BLACKLIST_KEY + key);
        }

        Task<bool> IDefenceStore.WhitelistOrigin(IPAddress origin)
        {
            var originKey = IP2Number(origin);
            return this.db.KeyDeleteAsync(this.prefix + BLACKLIST_ORIGIN + originKey);
        }
    }
}
