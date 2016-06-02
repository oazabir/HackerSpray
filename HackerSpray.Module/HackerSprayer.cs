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
        public static Task<Result> DefendAsync(string key, IPAddress origin)
        {
            return DefendAsync(key, origin,
                Config.MaxHitsPerKeyInterval, Config.MaxHitsPerKey,
                Config.MaxHitsPerOriginInterval, Config.MaxHitsPerOrigin,
                Config.MaxHitsPerKeyPerOriginInterval, Config.MaxHitsPerKeyPerOrigin);
        }

        public static async Task<Result> DefendAsync(string key, IPAddress origin, 
            TimeSpan keyInterval, long keyMaxHit,
            TimeSpan originInterval, long originMaxHit,
            TimeSpan keyOriginInterval, long keyOriginMaxHit)
        {
            if (await Store.IsOriginBlacklisted(origin))
                return Result.OriginBlocked;

            var hitStats = await Store.IncrementHit(key, origin, 
                keyInterval, originInterval, keyOriginInterval);

            if (hitStats.HitsOnKey > keyMaxHit)
                return Result.TooManyHitsOnKey;

            if (hitStats.HitsFromOrigin > originMaxHit)
                return Result.TooManyHitsFromOrigin;

            if (hitStats.HitsOnKeyFromOrigin > keyOriginMaxHit)
                return Result.TooManyHitsOnKeyFromOrigin;

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


        public static async Task<TResult> DefendAsync<TResult>(            
            Func<Func<TResult, Task<TResult>>, Func<TResult, Task<TResult>>, Task<TResult>> work,             
            Func<HackerSprayer.Result, TResult> blocked,
            string validActionKey,
            long maxValidAttempt,
            TimeSpan validAttemptInterval,
            string invalidActionKey,
            long maxInvalidAttempt,
            TimeSpan invalidAttemptInterval,
            IPAddress origin)
        {
            Func<TResult, Task<TResult>> success = async (returnType) =>
            {
                var result = await HackerSprayer.DefendAsync(
                    validActionKey,
                    origin,
                    validAttemptInterval,
                    maxValidAttempt,
                    TimeSpan.MaxValue, long.MaxValue,
                    TimeSpan.MaxValue, long.MaxValue);

                if (result == HackerSprayer.Result.TooManyHitsOnKey)
                {
                    // Too many valid login on same username. 
                    await HackerSprayer.BlacklistKeyAsync(validActionKey, validAttemptInterval);
                    return blocked(result);
                }

                return returnType;
            };

            Func<TResult, Task<TResult>> fail = async (returnType) =>
            {
                // Check for too many invalid login on a username    
                var result = await HackerSprayer.DefendAsync(
                    invalidActionKey,
                    origin,
                    invalidAttemptInterval,
                    maxInvalidAttempt,
                    TimeSpan.MaxValue, long.MaxValue,
                    TimeSpan.MaxValue, long.MaxValue);

                if (result == HackerSprayer.Result.TooManyHitsOnKey)
                {
                    await HackerSprayer.BlacklistKeyAsync(invalidActionKey, invalidAttemptInterval);
                    return blocked(result);
                }

                return returnType;
            };

            if (await HackerSprayer.IsKeyBlacklistedAsync(invalidActionKey))
            {
                return blocked(HackerSprayer.Result.KeyBlocked);
            }

            return await work(success, fail);
        }

        
    }
}
