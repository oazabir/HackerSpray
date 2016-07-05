using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HackerSpray.Module;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace HackerSpray.UnitTests
{
    public static class Extensions
    {
        public static TResult Run<TResult>(this Task<TResult> t)
        {
            DateTime start = DateTime.Now;
            var result = t.GetAwaiter().GetResult();
            DateTime end = DateTime.Now;
            if ((end - start).TotalMilliseconds > 10)
                Trace.TraceWarning("Took more than 10ms! HackerSpray cannot be this slow!");

            return result;
        }

    }
    [TestClass]
    public class TestHackerSprayer
    {
        private static Random randomizer = new Random((int)DateTime.Now.Ticks);
        
        [TestInitialize]
        public void Init()
        {
            Hacker.Config.MaxHitsPerKey = 15;
            Hacker.Config.MaxHitsPerOrigin = 20;
            Hacker.Config.MaxHitsPerKeyPerOrigin = 15;
            Hacker.Config.MaxHitsPerKeyInterval = TimeSpan.FromMinutes(1);
            Hacker.Config.MaxHitsPerOriginInterval = TimeSpan.FromMinutes(1);
            Hacker.Config.MaxHitsPerKeyPerOriginInterval = TimeSpan.FromMinutes(1);

            Hacker.Store = new RedisDefenceStore("localhost", "HackerSprayUnitTest:", Hacker.Config);            
            //Hacker.Store = new RedisDefenceStore("10.187.147.120:61149", "AuthTest:", Hacker.Config);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Hacker.ClearAllHitsAsync().Run();
            Hacker.ClearBlacklistsAsync().Run();
            Hacker.Store.Dispose();
        }


        private IPAddress GetRandomIP()
        {
            return IPAddress.Parse(randomizer.Next(255) + "." + randomizer.Next(255) + "." + randomizer.Next(255) + "." + randomizer.Next(255));
        }

        private string GetRandomKey()
        {
            return "-User-" + randomizer.Next(65535);
        }

        [TestMethod]
        public void TestAllowed()
        {
            var result = Hacker.DefendAsync("TestAllowed" + GetRandomKey(), GetRandomIP()).Run();
            Assert.AreEqual(Hacker.Result.Allowed, result);
        }

        [TestMethod]
        public void TestMaxHitsPerKey()
        {
            Hacker.Config.MaxHitsPerKey = 10;
            Hacker.Config.MaxHitsPerOrigin = 20;
            Hacker.Config.MaxHitsPerKeyPerOrigin = 20;

            Func<string> keyGenerator = () => { return "TestMaxHitsPerKey" + GetRandomKey(); };
            var fixedKey = keyGenerator();

            var startTime = DateTime.Now;

            Parallel.For(0, Hacker.Config.MaxHitsPerKey,
                hit =>
                {
                    Assert.AreEqual(
                        Hacker.Result.Allowed,
                        Hacker.DefendAsync(fixedKey, GetRandomIP()).Run());
                });

            Assert.AreEqual(
                Hacker.Config.MaxHitsPerKey,
                Hacker.GetHitsForKey(fixedKey).Run(),
                "Number of hits recorded must match");
            
            Assert.AreEqual(
                Hacker.Result.TooManyHitsOnKey,
                Hacker.DefendAsync(fixedKey, GetRandomIP()).Run());

            var ip = GetRandomIP();
            Assert.AreEqual(
                Hacker.Result.Allowed,
                Hacker.DefendAsync(keyGenerator(), ip).Run(),
                "Allow traffic from aonther key on same IP");

            WaitForIntervalToElapse(Hacker.Config.MaxHitsPerKeyInterval, startTime);

            // Hit from another IP using same key should be allowed
            Assert.AreEqual(
                Hacker.Result.Allowed,
                Hacker.DefendAsync(fixedKey, GetRandomIP()).Run(),
                "After expiration time, key must be unblocked");
        }

        [TestMethod]
        public void TestMaxHitsPerOrigin()
        {
            Hacker.Config.MaxHitsPerKey = 20;
            Hacker.Config.MaxHitsPerOrigin = 10;
            Hacker.Config.MaxHitsPerKeyPerOrigin = 20;

            var ip = GetRandomIP();
            Func<string> keyGenerator = () => { return "TestMaxHitsPerOrigin" + GetRandomKey(); };

            var startTime = DateTime.Now;
            var result = Parallel.For(0, Hacker.Config.MaxHitsPerOrigin,
                hit =>
                {
                    Assert.AreEqual(
                    Hacker.Result.Allowed,
                    Hacker.DefendAsync(keyGenerator(), ip).Run()
                    );
                });

            while (!result.IsCompleted)
                Thread.Sleep(100);

            Assert.AreEqual(
                Hacker.Config.MaxHitsPerOrigin,
                Hacker.GetHitsFromOrigin(ip).Run(),
                "Number of hits recorded must match");


            // No more requests from same IP
            Assert.AreEqual(
                    Hacker.Result.TooManyHitsFromOrigin,
                    Hacker.DefendAsync(keyGenerator(), ip).Run()
                    );

            // Allow requests from other IPs
            Assert.AreEqual(
                Hacker.Result.Allowed,
                Hacker.DefendAsync(keyGenerator(), GetRandomIP()).Run());


            WaitForIntervalToElapse(Hacker.Config.MaxHitsPerOriginInterval, startTime);

            Assert.AreEqual(
                    Hacker.Result.Allowed,
                    Hacker.DefendAsync(keyGenerator(), ip).Run(),
                    "Allow hits from same origin after expiration time."
                    );
        }

        [TestMethod]
        public void TestMaxHitsOnKeyPerOrigin()
        {
            Hacker.Config.MaxHitsPerKey = 20;
            Hacker.Config.MaxHitsPerOrigin = 20;
            Hacker.Config.MaxHitsPerKeyPerOrigin = 10;

            Func<string> keyGenerator = () => { return "TestMaxHitsOnKeyPerOrigin" + GetRandomKey(); };
            var key = keyGenerator();
            var ip = GetRandomIP();

            var startTime = DateTime.Now;
            Parallel.For(0, Hacker.Config.MaxHitsPerKeyPerOrigin,
                hit =>
                {
                    Assert.AreEqual(
                        Hacker.Result.Allowed,
                        Hacker.DefendAsync(key, ip).Run(),
                        "Allow hits on same key and IP");
                });
            
            Assert.AreEqual(
                Hacker.Config.MaxHitsPerKeyPerOrigin,
                Hacker.GetHitsForKey(key).Run(),
                "Number of hits recorded must match");

            // No more requests from same key and IP
            Assert.AreEqual(
                Hacker.Result.TooManyHitsOnKeyFromOrigin,
                Hacker.DefendAsync(key, ip).Run());

            Assert.AreEqual(
                Hacker.Result.Allowed,
                Hacker.DefendAsync(keyGenerator(), ip).Run(),
                "From different key, same IP, allow");

            Assert.AreEqual(
                Hacker.Result.Allowed,
                Hacker.DefendAsync(key, GetRandomIP()).Run(),
                "From different IP, same key, allow");

            WaitForIntervalToElapse(Hacker.Config.MaxHitsPerOriginInterval, startTime);

            Assert.AreEqual(
                        Hacker.Result.Allowed,
                        Hacker.DefendAsync(key, ip).Run(),
                        "Allow hits on same key and IP after expiration");
        }

        [TestMethod]
        public void TestOriginBlaclistingAndWhitelisting()
        {
            var ip = GetRandomIP();
            var key = GetRandomKey();

            Assert.AreEqual(
                Hacker.Result.Allowed,
                Hacker.DefendAsync(key, ip).Run(),
                string.Format("Allow traffic on {0} from {1}", key, ip));

            Hacker.BlacklistOriginAsync(ip).Run();

            Assert.AreEqual(
                Hacker.Result.OriginBlocked,
                Hacker.DefendAsync(key, ip).Run(),
                string.Format("Block traffic on {0} from {1}", key, ip));

            Assert.AreEqual(
                Hacker.Result.OriginBlocked,
                Hacker.DefendAsync(GetRandomKey(), ip).Run(),
                string.Format("Allow any traffic from {1}", key, ip));

            Hacker.WhitelistOriginAsync(ip).Run();

            Assert.AreEqual(
                Hacker.Result.Allowed,
                Hacker.DefendAsync(key, ip).Run(),
                string.Format("Allow traffic on {0} from {1} after whitelist", key, ip));
        }

        [TestMethod]
        public void TestKeyBlacklistingAndWhitelisting()
        {
            var ip = GetRandomIP();
            var key = GetRandomKey();

            Assert.AreEqual(
                Hacker.Result.Allowed,
                Hacker.DefendAsync(key, ip).Run(),
                string.Format("Allow traffic on {0} from {1}", key, ip));

            Hacker.BlacklistKeyAsync(key, TimeSpan.FromMinutes(5)).Run();

            Assert.AreEqual(
                Hacker.Result.KeyBlocked,
                Hacker.DefendAsync(key, ip).Run(),
                string.Format("Block traffic on {0} from {1}", key, ip));

            Assert.AreEqual(
                Hacker.Result.KeyBlocked,
                Hacker.DefendAsync(key, GetRandomIP()).Run(),
                string.Format("Allow any traffic from {1}", key, ip));

            Hacker.WhitelistKeyAsync(key).Run();

            Assert.AreEqual(
                Hacker.Result.Allowed,
                Hacker.DefendAsync(key, ip).Run(),
                string.Format("Allow traffic on {0} from {1} after whitelist", key, ip));
        }

        [TestMethod]
        public void TestKeyCustomHitLimit()
        {
            Hacker.Config.MaxHitsPerKey = 100;
            Hacker.Config.MaxHitsPerOrigin = 100;
            Hacker.Config.MaxHitsPerKeyPerOrigin = 100;

            var key = "InvalidLogin-" + GetRandomKey();
            var ip = GetRandomIP();
            var interval = TimeSpan.FromMinutes(1);
            var maxHits = 10;
            var startTime = DateTime.Now;

            Parallel.For(0, maxHits,
                hit =>
                {
                    Assert.AreEqual(
                    Hacker.Result.Allowed,
                    Hacker.DefendAsync(key, ip, 
                        interval, maxHits,
                        TimeSpan.MaxValue, long.MaxValue,
                        TimeSpan.MaxValue, long.MaxValue).Run(),
                    "Allow hits on key for custom interval");
                });

            Assert.AreEqual(
                    Hacker.Result.TooManyHitsOnKey,
                    Hacker.DefendAsync(key, ip, 
                        interval, maxHits,
                        TimeSpan.MaxValue, long.MaxValue,
                        TimeSpan.MaxValue, long.MaxValue).Run(),
                    "Must not allow hits on key after custom interval");

            Assert.AreEqual(
                    Hacker.Result.Allowed,
                    Hacker.DefendAsync("InvalidLogin-" + GetRandomKey(), ip, 
                        interval, maxHits,
                        TimeSpan.MaxValue, long.MaxValue,
                        TimeSpan.MaxValue, long.MaxValue).Run(),
                    "Allow hits on different key from same IP");

            WaitForIntervalToElapse(interval, startTime);

            Assert.AreEqual(
                    Hacker.Result.Allowed,
                    Hacker.DefendAsync(key, ip, 
                        interval, maxHits,
                        TimeSpan.MaxValue, long.MaxValue,
                        TimeSpan.MaxValue, long.MaxValue).Run(),
                    "Allow hits on key for after interval has passed.");
        }

        [TestMethod]
        public void TestKeyBlacklisting()
        {
            var key = GetRandomKey();
            var ip = GetRandomIP();

            var interval = TimeSpan.FromMinutes(1);

            Assert.AreEqual(
                    false,
                    Hacker.IsKeyBlacklistedAsync(key).Run(),
                    "Key must not be blacklisted");

            var startTime = DateTime.Now;
            Hacker.BlacklistKeyAsync(key, interval).Run();

            Assert.AreEqual(
                    true,
                    Hacker.IsKeyBlacklistedAsync(key).Run(),
                    "Key must be blacklisted");

            string [] blacklistedKeys = Hacker.GetKeyBlacklists().Run();
            Assert.IsTrue(Array.Exists(blacklistedKeys, k => k == key),
                "Key must be in the blacklist");

            WaitForIntervalToElapse(interval, startTime);

            Assert.AreEqual(
                    false,
                    Hacker.IsKeyBlacklistedAsync(key).Run(),
                    "Key must not be blacklisted after expiration time");
        }

        private static void WaitForIntervalToElapse(TimeSpan interval, DateTime startTime)
        {
            Thread.Sleep(interval - (DateTime.Now - startTime));
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }

        [TestMethod]
        public void TestOriginBlacklisting()
        {
            var key = GetRandomKey();
            var ip = GetRandomIP();

            var interval = TimeSpan.FromMinutes(1);

            Assert.AreEqual(
                    false,
                    Hacker.isOriginBlacklistedAsync(ip).Run(),
                    "Origin must not be blacklisted");

            var startTime = DateTime.Now;
            Hacker.BlacklistOriginAsync(ip, interval).Run();

            Assert.AreEqual(
                    true,
                    Hacker.isOriginBlacklistedAsync(ip).Run(),
                    "Origin must be blacklisted");

            WaitForIntervalToElapse(interval, startTime);

            Assert.AreEqual(
                    false,
                    Hacker.isOriginBlacklistedAsync(ip).Run(),
                    "Origin must not be blacklisted after expiration time");
        }


        [TestMethod]
        public void TestOriginRangeBlocking()
        {
            Hacker.ClearBlacklistsAsync().Run();
            Hacker.ClearAllHitsAsync().Run();

            var ipsInRange = new[] {
                IPAddress.Parse("10.10.10.10"),
                IPAddress.Parse("10.10.10.11"),
                IPAddress.Parse("10.10.254.254"),
                IPAddress.Parse("10.11.10.9"),
                IPAddress.Parse("10.11.10.10"),
                IPAddress.Parse("9.1.1.1"),
                IPAddress.Parse("9.1.1.10"),
                IPAddress.Parse("9.10.10.9"),
                IPAddress.Parse("10.11.10.12"),
                IPAddress.Parse("127.254.254.254"),
                IPAddress.Parse("100.100.100.100"),
                IPAddress.Parse("128.10.10.12"),
                IPAddress.Parse("128.10.10.254"),
                IPAddress.Parse("128.10.10.128"),
                };

            var ipsOutofRange = new[] {
                IPAddress.Parse("10.10.10.9"),
                IPAddress.Parse("9.10.10.10"),
                IPAddress.Parse("10.11.10.11"),
                IPAddress.Parse("128.10.10.11"),
                IPAddress.Parse("200.200.200.200"),
                IPAddress.Parse("1.1.1.1"),
                IPAddress.Parse("10.0.0.0")
                };

            Hacker.BlacklistOriginAsync(IPAddress.Parse("10.10.10.10"), IPAddress.Parse("10.11.10.10")).Run();
            Hacker.BlacklistOriginAsync(IPAddress.Parse("9.1.1.1"), IPAddress.Parse("9.10.10.9")).Run();
            Hacker.BlacklistOriginAsync(IPAddress.Parse("10.11.10.12"), IPAddress.Parse("127.254.254.254")).Run();
            Hacker.BlacklistOriginAsync(IPAddress.Parse("128.10.10.12"), IPAddress.Parse("128.10.10.254")).Run();

            Array.ForEach(ipsInRange, ip => 
                Assert.AreEqual(Hacker.Result.OriginBlocked,
                    Hacker.DefendAsync("TestOriginRangeBlocking", ip).Run(),
                    ip.ToString() + " must be blocked."));

            Hacker.ClearAllHitsAsync().Run();

            Array.ForEach(ipsOutofRange, ip => 
                Assert.AreEqual(Hacker.Result.Allowed,
                    Hacker.DefendAsync("TestOriginRangeBlocking", ip).Run(),
                    ip.ToString() + " must be allowed"));

            Hacker.WhitelistOriginAsync(IPAddress.Parse("9.1.1.1"), IPAddress.Parse("9.10.10.9")).Run();

            Array.ForEach(new[]
                {
                    IPAddress.Parse("9.1.1.1"),
                    IPAddress.Parse("9.1.1.10"),
                    IPAddress.Parse("9.10.10.9")
                }, 
                ip => 
                    Assert.AreEqual(Hacker.Result.Allowed,
                        Hacker.DefendAsync("TestOriginRangeBlocking", ip).Run(),
                        ip.ToString() + " must be allowed"));

            Hacker.ClearBlacklistsAsync().Run();
            Hacker.ClearAllHitsAsync().Run();

            Array.ForEach(ipsInRange, ip => 
                Assert.AreEqual(Hacker.Result.Allowed,
                    Hacker.DefendAsync("TestOriginRangeBlocking", ip).Run(),
                    ip.ToString() + " must be allowed when there's no blacklisting."));

            Hacker.ClearAllHitsAsync().Run();

            Array.ForEach(ipsOutofRange, ip => 
                Assert.AreEqual(Hacker.Result.Allowed,
                    Hacker.DefendAsync("TestOriginRangeBlocking", ip).Run(),
                    ip.ToString() + " must be allowed when there's no blacklisting"));
        }

        [TestMethod]
        public void ClearAllHits()
        {
            Func<string> keyGenerator = () => { return "ClearAllHits" + GetRandomKey(); };
            var keys = new List<string>();
            var random = new Random((int)DateTime.Now.Ticks);
            var startTime = DateTime.Now;

            Parallel.For(0, 10,
                hit =>
                {
                    var key = keyGenerator();
                    keys.Add(key);

                    var hits = 1 + random.Next(Hacker.Config.MaxHitsPerKey-1);

                    for (var i = 0; i < hits; i++)
                    {
                        Assert.AreEqual(
                            Hacker.Result.Allowed,
                            Hacker.DefendAsync(key, GetRandomIP()).Run(),
                            "Request must be allowed for a hit on a key");
                    }

                    Assert.AreEqual(hits, Hacker.GetHitsForKey(key).Run(),
                        "Number of hits must be " + hits + " for key: " + key);
                });

            Hacker.ClearAllHitsAsync().Run();

            foreach(var key in keys)
            {
                Assert.AreEqual(0, Hacker.GetHitsForKey(key).Run(),
                        "After clearing hits Number of hits must be 0 for key: " + key);
            }
        }
    }
}
