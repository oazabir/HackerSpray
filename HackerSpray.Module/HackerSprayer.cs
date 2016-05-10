using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HackerSpray.Module
{
    public class HackerSprayer
    {
        public static IDefenceStore Store;
        public static DefenceConfig Config = new DefenceConfig
        {
            KeyBlacklistInterval = TimeSpan.FromMinutes(15),
            MaxHitsPerKey = 10000,
            MaxHitsPerKeyPerOrigin = 100,
            MaxHitsPerOrigin = 10000,
            MaxHitsPerKeyInterval = TimeSpan.FromMinutes(15),
            MaxHitsPerKeyPerOriginInterval = TimeSpan.FromMinutes(15),
            MaxHitsPerOriginInterval = TimeSpan.FromMinutes(15),
            OriginBlacklistInterval = TimeSpan.FromMinutes(15)
        };
        public enum DefenceResult
        {
            Allowed = 0,
            OriginBlocked,
            TooManyHitsFromOrigin,
            KeyBlocked,
            TooManyHitsOnKey,
            TooManyHitsOnKeyFromOrigin
        }
        public static async Task<DefenceResult> Defend(string key, IPAddress origin)
        {
            if (await Store.IsOriginBlacklisted(origin))
                return DefenceResult.OriginBlocked;

            var hitStats = await Store.IncrementHit(key, origin);

            if (hitStats.HitsOnKeyFromOrigin > Config.MaxHitsPerKeyPerOrigin)
                return DefenceResult.TooManyHitsOnKeyFromOrigin;

            if (hitStats.HitsOnKey > Config.MaxHitsPerKey)
                return DefenceResult.TooManyHitsOnKey;

            if (hitStats.HitsFromOrigin > Config.MaxHitsPerOrigin)
                return DefenceResult.TooManyHitsFromOrigin;
            
            if (await Store.IsKeyBlacklisted(key))
                return DefenceResult.KeyBlocked;
            
            return DefenceResult.Allowed;
        }

        public static async Task<DefenceResult> Defend(string key, IPAddress origin, TimeSpan interval, long maxHit)
        {
            if (await Store.IsOriginBlacklisted(origin))
                return DefenceResult.OriginBlocked;

            var hitStats = await Store.IncrementHit(key, origin, interval);

            if (hitStats.HitsOnKeyFromOrigin > Config.MaxHitsPerKeyPerOrigin)
                return DefenceResult.TooManyHitsOnKeyFromOrigin;

            if (hitStats.HitsOnKey > maxHit)
                return DefenceResult.TooManyHitsOnKey;

            if (hitStats.HitsFromOrigin > Config.MaxHitsPerOrigin)
                return DefenceResult.TooManyHitsFromOrigin;

            if (await Store.IsKeyBlacklisted(key))
                return DefenceResult.KeyBlocked;

            return DefenceResult.Allowed;
        }

        public static Task<bool> BlacklistKey(string key, TimeSpan expiration)
        {
            return Store.BlacklistKey(key, expiration);
        }

        public static Task<bool> WhitelistKey(string key)
        {
            return Store.WhitelistKey(key);
        }

        public static Task<bool> BlacklistOrigin(IPAddress origin, TimeSpan expiration)
        {
            return Store.BlacklistOrigin(origin, expiration);
        }

        public static Task<bool> BlacklistOrigin(IPAddress origin)
        {
            return BlacklistOrigin(origin, Config.OriginBlacklistInterval);
        }

        public static Task<bool> WhitelistOrigin(IPAddress origin)
        {
            return Store.WhitelistOrigin(origin);
        }
    }
}
