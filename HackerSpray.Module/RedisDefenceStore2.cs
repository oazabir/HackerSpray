using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using RedisBoost;

namespace HackerSpray.Module
{
    public class RedisDefenceStore2 : IDefenceStore
    {
        private const string BLACKLIST_KEY = "BLACKLISTS-KEY-";
        private const string BLACKLIST_ORIGIN = "BLACKLISTS-ORIGIN-";
        private const string BLACKLIST_ORIGIN_RANGE = "BLACKLISTS-ORIGIN-RANGE-";

        private string connectionString;
        private string prefix;
        private DefenceConfig config;

        private static object lockObject = new object();
        private static IRedisClientsPool connectionManager;

        public RedisDefenceStore2(string connectionString, string prefix, DefenceConfig config)
        {
            if (connectionManager == null)
            {
                lock (lockObject)
                {
                    if (connectionManager == null)
                    {
                        connectionManager = RedisClient.CreateClientsPool();
                    }
                }
            }

            this.connectionString = connectionString;
            this.prefix = prefix;
            this.config = config;
        }

        async Task<bool> IDefenceStore.BlacklistKey(string key, TimeSpan expiry)
        {
            using (var client = await GetClient())
            {
                var result = await client.SetExAsync(this.prefix + BLACKLIST_KEY + key, (int)expiry.TotalSeconds, 1);
                return result.Length > 0;
            }
        }

        async Task<bool> IDefenceStore.BlacklistOrigin(IPAddress origin, TimeSpan expiry)
        {
            var originValue = IP2Number(origin);
            using (var client = await GetClient())
            {
                var result = await client.SetExAsync(this.prefix + BLACKLIST_ORIGIN + originValue, (int)expiry.TotalSeconds, 1);
                return result.Length > 0;
            }

        }

        async Task<bool> IDefenceStore.BlacklistOrigin(IPAddress start, IPAddress end)
        {
            var originStart = IP2Number(start);
            var originEnd = IP2Number(end);

            using (var client = await GetClient())
            {
                var result = await client.SAddAsync(this.prefix + BLACKLIST_ORIGIN_RANGE, originStart + "-" + originEnd);
                return result > 0;
            }
        }

        async Task<long> IDefenceStore.GetHitsForKey(string key)
        {
            var keyKey = prefix + "key-" + key;
            using (var client = await GetClient())
            {
                return (await client.GetAsync(keyKey)).As<long>();
            }
        }

        async Task<long> IDefenceStore.GetHitsFromOrigin(IPAddress origin)
        {
            var originValue = IP2Number(origin);
            var originkey = prefix + "origin-" + originValue;
            using (var client = await GetClient())
            {
                return (await client.GetAsync(originkey)).As<long>();
            }
        }

        async Task<string[]> IDefenceStore.GetKeyBlacklists(string key)
        {
            using (var client = await GetClient())
                return (await client.KeysAsync(this.prefix + BLACKLIST_KEY + "*")).AsArray<string>();
        }

        async Task<string[]> IDefenceStore.GetOriginBlacklists(IPAddress origin)
        {
            using (var client = await GetClient())
            {
                var result = await client.ZRangeAsync(this.prefix + BLACKLIST_ORIGIN_RANGE, 0, 1000);
                return result.AsArray<string>();
            }
        }

        private Task<IRedisClient> GetClient()
        {
            return connectionManager.CreateClientAsync(this.connectionString);
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

            using (var client = await GetClient())
            {
                var originTask = client.IncrAsync(originkey);
                var keyTask = client.IncrAsync(keyKey);
                var keyOriginTask = client.IncrAsync(keyoriginkey);

                await Task.WhenAll(originTask, keyTask, keyOriginTask);

                stats.HitsFromOrigin = originTask.Result;
                stats.HitsOnKey = keyTask.Result;
                stats.HitsOnKeyFromOrigin = keyOriginTask.Result;

                var now = DateTime.Now;
                // If any of the counter was created for the first time,
                // need to set expiration time for them.
                var writeTasks = new List<Task>();
                if (originTask.Result == 1)
                    writeTasks.Add(client.ExpireAsync(originkey, (int)this.config.MaxHitsPerOriginInterval.TotalSeconds));
                if (keyTask.Result == 1)
                    writeTasks.Add(client.ExpireAsync(keyKey, (int)keyInterval.TotalSeconds));
                if (keyOriginTask.Result == 1)
                    writeTasks.Add(client.ExpireAsync(keyoriginkey, (int)this.config.MaxHitsPerKeyPerOriginInterval.TotalSeconds));

                if (writeTasks.Count > 0)
                    await Task.WhenAll(writeTasks);

                return stats;
            }
        }

        async Task<bool> IDefenceStore.IsKeyBlacklisted(string key)
        {
            using (var client = await GetClient())
            {
                var result = await client.ExistsAsync(this.prefix + BLACKLIST_KEY + key);
                return result > 0;
            }
        }

        async Task<bool> IDefenceStore.IsOriginBlacklisted(IPAddress origin)
        {
            int originValue = IP2Number(origin);

            using (var client = await GetClient())
            {
                // Check if the IP itself is blacklisted
                var exists = await client.ExistsAsync(this.prefix + BLACKLIST_ORIGIN + originValue);
                if (exists > 0)
                    return true; 

                var ranges = await client.ZRangeByScoreAsync(this.prefix + BLACKLIST_ORIGIN, originValue, originValue, 0, 1);


                var e = ranges.GetEnumerator();
                if (e.MoveNext())
                {
                    var range = e.Current.ToString();
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
        }

        private static int IP2Number(IPAddress origin)
        {
            return BitConverter.ToInt32(origin.GetAddressBytes(), 0);
        }

        async Task<bool> IDefenceStore.WhitelistKey(string key)
        {
            using (var client = await GetClient())
                return await client.DelAsync(this.prefix + BLACKLIST_KEY + key) > 0;
        }

        async Task<bool> IDefenceStore.WhitelistOrigin(IPAddress origin)
        {
            var originKey = IP2Number(origin);
            using (var client = await GetClient())
                return await client.DelAsync(this.prefix + BLACKLIST_ORIGIN + originKey) > 0;            
        }
    }
}
