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
        public enum Result
        {
            Allowed = 0,
            OriginBlocked,
            TooManyHitsFromOrigin,
            KeyBlocked,
            TooManyHitsOnKey,
            TooManyHitsOnKeyFromOrigin
        }
        public static async Task<Result> DefendAsync(string key, IPAddress origin)
        {
            if (await Store.IsOriginBlacklisted(origin))
                return Result.OriginBlocked;

            var hitStats = await Store.IncrementHit(key, origin);

            if (hitStats.HitsOnKeyFromOrigin > Config.MaxHitsPerKeyPerOrigin)
                return Result.TooManyHitsOnKeyFromOrigin;

            if (hitStats.HitsOnKey > Config.MaxHitsPerKey)
                return Result.TooManyHitsOnKey;

            if (hitStats.HitsFromOrigin > Config.MaxHitsPerOrigin)
                return Result.TooManyHitsFromOrigin;
            
            if (await Store.IsKeyBlacklisted(key))
                return Result.KeyBlocked;
            
            return Result.Allowed;
        }

        public static async Task<Result> DefendAsync(string key, IPAddress origin, TimeSpan interval, long maxHit)
        {
            if (await Store.IsOriginBlacklisted(origin))
                return Result.OriginBlocked;

            var hitStats = await Store.IncrementHit(key, origin, interval);

            if (hitStats.HitsOnKeyFromOrigin > Config.MaxHitsPerKeyPerOrigin)
                return Result.TooManyHitsOnKeyFromOrigin;

            if (hitStats.HitsOnKey > maxHit)
                return Result.TooManyHitsOnKey;

            if (hitStats.HitsFromOrigin > Config.MaxHitsPerOrigin)
                return Result.TooManyHitsFromOrigin;

            if (await Store.IsKeyBlacklisted(key))
                return Result.KeyBlocked;

            return Result.Allowed;
        }

        public static Task<bool> BlacklistKeyAsync(string key, TimeSpan expiration)
        {
            return Store.BlacklistKey(key, expiration);
        }

        public static Task<bool> WhitelistKeyAsync(string key)
        {
            return Store.WhitelistKey(key);
        }

        public static Task<bool> BlacklistOriginAsync(IPAddress origin, TimeSpan expiration)
        {
            return Store.BlacklistOrigin(origin, expiration);
        }

        public static Task<bool> BlacklistOriginAsync(IPAddress origin)
        {
            return BlacklistOriginAsync(origin, Config.OriginBlacklistInterval);
        }

        public static Task<bool> BlacklistOriginAsync(IPAddress start, IPAddress end)
        {
            return Store.BlacklistOrigin(start, end);
        }

        public static Task<bool> WhitelistOriginAsync(IPAddress origin)
        {
            return Store.WhitelistOrigin(origin);
        }

        public static Task<bool> WhitelistOriginAsync(IPAddress start, IPAddress end)
        {
            return Store.WhitelistOrigin(start, end);
        }

        public static Task<bool> IsKeyBlacklistedAsync(string key)
        {
            return Store.IsKeyBlacklisted(key);
        }

        public static Task<bool> isOriginBlacklistedAsync(IPAddress origin)
        {
            return Store.IsOriginBlacklisted(origin);
        }

        public static Task<bool> ClearBlacklistsAsync()
        {
            return Store.ClearBlacklists();
        }

        public static Task<bool> ClearAllHitsAsync()
        {
            return Store.ClearAllHits();
        }

        public static Task<long> GetHitsForKey(string key)
        {
            return Store.GetHitsForKey(key);
        }

        public static Task<long> GetHitsFromOrigin(IPAddress origin)
        {
            return Store.GetHitsFromOrigin(origin);
        }

        public static Task<string[]> GetKeyBlacklists()
        {
            return Store.GetKeyBlacklists();
        }


        public static async Task<TResult> Defend<TResult>(            
            Func<object, Task<bool>> work, 
            Func<object, TResult> success, 
            Func<object, TResult> failed,
            Func<object, TResult> blocked,
            string validActionKey,
            long maxValidAttempt,
            TimeSpan validAttemptInterval,
            string invalidActionKey,
            long maxInvalidAttempt,
            TimeSpan invalidAttemptInterval,
            IPAddress origin)
        {
            var state = new object();
            if (await HackerSprayer.IsKeyBlacklistedAsync(invalidActionKey))
            {
                return blocked(state);
            }

            var status = await work(state);
            if (status)
            {

                var result = await HackerSprayer.DefendAsync(
                    validActionKey,
                    origin,
                    validAttemptInterval,
                    maxValidAttempt);

                if (result == HackerSprayer.Result.TooManyHitsOnKey)
                {
                    // Too many valid login on same username. 
                    await HackerSprayer.BlacklistKeyAsync(validActionKey, validAttemptInterval);
                    return blocked(state);
                }
                
                return success(state);

            }
            else
            {
                // Check for too many invalid login on a username    
                var result = await HackerSprayer.DefendAsync(
                    invalidActionKey,
                    origin,
                    invalidAttemptInterval,
                    maxInvalidAttempt);

                if (result == HackerSprayer.Result.TooManyHitsOnKey)
                {
                    await HackerSprayer.BlacklistKeyAsync(invalidActionKey, invalidAttemptInterval);
                    return blocked(state);
                }

                return failed(state);
            }
        }
    }
}
