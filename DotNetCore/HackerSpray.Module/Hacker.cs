using HackerSpray.Logger;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace HackerSpray.Module
{
    public class Hacker
    {
        private const string DEFEND_FUNC_NAME = "Defend";

        public static IDefenceStore Store;
        public static ILogger Logger; 
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
            Stopwatch watch = new Stopwatch();
            watch.Start();

            // First check origin for blacklisting, since this is the most common
            // scenario for blocking attacks. 
            if (await Store.IsOriginBlacklisted(origin))
            {
                Trace(LogLevel.Debug, "OriginBlocked: " + origin.ToString());
                return Result.OriginBlocked;
            }

            // If origin not blocked, increase the hit counters. 
            var hitStats = await Store.IncrementHit(key, origin,
                keyInterval, originInterval, keyOriginInterval);

            Trace(LogLevel.Debug, "Hits: " + key + '\t' + hitStats.HitsOnKey + '\t' + origin.ToString() + '\t' + hitStats.HitsFromOrigin + ' ' + hitStats.HitsOnKeyFromOrigin);

            if (hitStats.HitsOnKey > keyMaxHit)
            {
                Trace(LogLevel.Debug, "TooManyHitsOnKey: " + key);
                return Result.TooManyHitsOnKey;
            }

            if (hitStats.HitsFromOrigin > originMaxHit)
            {
                Trace(LogLevel.Debug, "TooManyHitsFromOrigin: " + origin.ToString());
                return Result.TooManyHitsFromOrigin;
            }

            if (hitStats.HitsOnKeyFromOrigin > keyOriginMaxHit)
            {
                Trace(LogLevel.Debug, "TooManyHitsOnKeyFromOrigin: " + key + '\t' + origin.ToString());
                return Result.TooManyHitsOnKeyFromOrigin;
            }

            // Finally check the key. You could do it earlier. But then 
            // You will miss the hit counters and you won't be able to 
            // monitor traffic going to each blocked key to take any decision
            if (await Store.IsKeyBlacklisted(key))
            {
                Trace(LogLevel.Debug, "KeyBlocked: " + key);
                return Result.KeyBlocked;
            }

            LogElapsed(DEFEND_FUNC_NAME, watch);

            return Result.Allowed;
        }

        public static Task<bool> BlacklistKeyAsync(string key, TimeSpan expiration)
        {
            Trace(LogLevel.Information, "Blacklist Key: " + key);
            return Store.BlacklistKey(key, expiration);
        }

        public static Task<bool> WhitelistKeyAsync(string key)
        {
            Trace(LogLevel.Information, "Whitelist Key: " + key);
            return Store.WhitelistKey(key);
        }

        public static Task<bool> BlacklistOriginAsync(IPAddress origin, TimeSpan expiration)
        {
            Trace(LogLevel.Information, "Blacklist origin: " + origin.ToString() + '\t' + expiration.ToString());
            return Store.BlacklistOrigin(origin, expiration);
        }

        public static Task<bool> BlacklistOriginAsync(IPAddress origin)
        {
            Trace(LogLevel.Information, "Blacklist origin: " + origin.ToString());
            return BlacklistOriginAsync(origin, Config.OriginBlacklistInterval);
        }

        public static Task<bool> BlacklistOriginAsync(IPAddress start, IPAddress end)
        {
            Trace(LogLevel.Information, "Blacklist origin: " + start.ToString() + " to " + end.ToString());
            return Store.BlacklistOrigin(start, end);
        }

        public static Task<bool> WhitelistOriginAsync(IPAddress origin)
        {
            Trace(LogLevel.Information, "Whitelist origin: " + origin.ToString());
            return Store.WhitelistOrigin(origin);
        }

        public static Task<bool> WhitelistOriginAsync(IPAddress start, IPAddress end)
        {
            Trace(LogLevel.Information, "Whitelist origin: " + start.ToString() + " to " + end.ToString());
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
            Trace(LogLevel.Information, "Clear All Blacklists");
            return Store.ClearBlacklists();
        }

        public static Task<bool> ClearAllHitsAsync()
        {
            Trace(LogLevel.Information, "Clear All Hits");
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
            Func<Hacker.Result, TResult> blocked,
            string validActionKey,
            long maxValidAttempt,
            TimeSpan validAttemptInterval,
            string invalidActionKey,
            long maxInvalidAttempt,
            TimeSpan invalidAttemptInterval,
            IPAddress origin)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            if (await Hacker.IsKeyBlacklistedAsync(invalidActionKey))
            {
                Trace(LogLevel.Debug, "InvalidActionKeyBlacklisted: " + invalidActionKey);
                LogElapsed(DEFEND_FUNC_NAME, watch);

                return blocked(Hacker.Result.KeyBlocked);
            }

            Func<TResult, Task<TResult>> success = async (returnType) =>
            {
                var result = await Hacker.DefendAsync(
                    validActionKey,
                    origin,
                    validAttemptInterval,
                    maxValidAttempt,
                    TimeSpan.MaxValue, long.MaxValue,
                    TimeSpan.MaxValue, long.MaxValue);

                if (result == Hacker.Result.TooManyHitsOnKey)
                {
                    // Too many valid login on same username. 
                    await Hacker.BlacklistKeyAsync(validActionKey, validAttemptInterval);
                    LogElapsed(DEFEND_FUNC_NAME, watch);
                    return blocked(result);
                }

                return returnType;
            };

            Func<TResult, Task<TResult>> fail = async (returnType) =>
            {
                // Check for too many invalid login on a username    
                var result = await Hacker.DefendAsync(
                    invalidActionKey,
                    origin,
                    invalidAttemptInterval,
                    maxInvalidAttempt,
                    TimeSpan.MaxValue, long.MaxValue,
                    TimeSpan.MaxValue, long.MaxValue);

                if (result == Hacker.Result.TooManyHitsOnKey)
                {
                    await Hacker.BlacklistKeyAsync(invalidActionKey, invalidAttemptInterval);
                    LogElapsed(DEFEND_FUNC_NAME, watch);
                    return blocked(result);
                }

                return returnType;
            };

            return await work(success, fail);
        }

        private static void LogElapsed(string name, Stopwatch watch)
        {
            watch.Stop();
            
            Trace(LogLevel.Debug, name + ':' + ' ' + watch.ElapsedMilliseconds);
        }

        public static void Trace(LogLevel logLevel, string message)
        {
            if (Logger != null)
                Logger.Log<object>(logLevel, 1, null, null, (s, e) => message);
        }

    }
}
