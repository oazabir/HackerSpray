using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;
using System.Net;

namespace HackerSpray.Module
{
    public class RedisDefenceStore : IDefenceStore
    {
        private const string BLACKLIST_KEY = "BLACKLISTS-KEY";
        private const string BLACKLIST_ORIGIN = "BLACKLISTS-ORIGIN";
        private const string BLACKLIST_ORIGIN_RANGE = "BLACKLISTS-ORIGIN-RANGE";

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

        Task<int> IDefenceStore.GetHitsForKey(string key)
        {
            throw new NotImplementedException();
        }

        Task<int> IDefenceStore.GetHitsFromOrigin(IPAddress origin)
        {
            throw new NotImplementedException();
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
            var task1 = this.db.StringGetAsync(originkey);
            var task2 = this.db.StringGetAsync(keyKey);
            var task3 = this.db.StringGetAsync(keyoriginkey);

            await Task.WhenAll(task1, task2, task3);

            var writeTasks = new List<Task>();

            long numericValue;
            if (task1.Result.HasValue)
            {
                if (task1.Result.TryParse(out numericValue))
                    stats.HitsFromOrigin = numericValue;

                writeTasks.Add(this.db.StringIncrementAsync(originkey));
                stats.HitsFromOrigin++;
            }
            else
            {
                writeTasks.Add(this.db.StringSetAsync(originkey, 1, this.config.MaxHitsPerOriginInterval));
            }


            if (task2.Result.HasValue)
            {
                if (task2.Result.TryParse(out numericValue))
                    stats.HitsOnKey = numericValue;

                writeTasks.Add(this.db.StringIncrementAsync(keyKey));
                stats.HitsOnKey++;
            }
            else
            {
                writeTasks.Add(this.db.StringSetAsync(keyKey, 1, keyInterval));
            }

            if (task3.Result.HasValue)
            {
                if (task3.Result.TryParse(out numericValue))
                    stats.HitsOnKeyFromOrigin = numericValue;

                writeTasks.Add(this.db.StringIncrementAsync(keyoriginkey));
                stats.HitsOnKeyFromOrigin++;
            }
            else
            {
                writeTasks.Add(this.db.StringSetAsync(keyoriginkey, 1, this.config.MaxHitsPerKeyPerOriginInterval));
            }

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
