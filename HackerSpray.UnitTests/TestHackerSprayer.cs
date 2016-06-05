using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HackerSpray.Module;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

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
                Debug.Print("Took more than 10ms! HackerSpray cannot be this slow!");

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
            HackerSprayer.Config.MaxHitsPerKey = 15;
            HackerSprayer.Config.MaxHitsPerOrigin = 20;
            HackerSprayer.Config.MaxHitsPerKeyPerOrigin = 15;
            HackerSprayer.Config.MaxHitsPerKeyInterval = TimeSpan.FromMinutes(1);
            HackerSprayer.Config.MaxHitsPerOriginInterval = TimeSpan.FromMinutes(1);
            HackerSprayer.Config.MaxHitsPerKeyPerOriginInterval = TimeSpan.FromMinutes(1);

            HackerSprayer.Store = new RedisDefenceStore("localhost", "HttpDefenceTest-", HackerSprayer.Config);            
            //HackerSprayer.Store = new RedisDefenceStore("10.187.146.206:7001,10.187.146.206:7002,10.187.146.206:7003,10.187.146.207:7001,10.187.146.207:7002,10.187.146.207:7003", "HttpDefenceTest-", HackerSprayer.Config);
        }

        [TestCleanup]
        public void Cleanup()
        {
            HackerSprayer.ClearAllHitsAsync().Run();
            HackerSprayer.ClearBlacklistsAsync().Run();
            HackerSprayer.Store.Dispose();
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
            var result = HackerSprayer.DefendAsync("TestAllowed" + GetRandomKey(), GetRandomIP()).Run();
            Assert.AreEqual(HackerSprayer.Result.Allowed, result);
        }

        [TestMethod]
        public void TestMaxHitsPerKey()
        {
            HackerSprayer.Config.MaxHitsPerKey = 10;
            HackerSprayer.Config.MaxHitsPerOrigin = 20;
            HackerSprayer.Config.MaxHitsPerKeyPerOrigin = 20;

            Func<string> keyGenerator = () => { return "TestMaxHitsPerKey" + GetRandomKey(); };
            var fixedKey = keyGenerator();

            var startTime = DateTime.Now;

            Parallel.For(0, HackerSprayer.Config.MaxHitsPerKey,
                hit =>
                {
                    Assert.AreEqual(
                    HackerSprayer.Result.Allowed,
                    HackerSprayer.DefendAsync(fixedKey, GetRandomIP()).Run()
                    );
                });

            Assert.AreEqual(
                HackerSprayer.Config.MaxHitsPerKey,
                HackerSprayer.GetHitsForKey(fixedKey).Run(),
                "Number of hits recorded must match");
            
            Assert.AreEqual(
                HackerSprayer.Result.TooManyHitsOnKey,
                HackerSprayer.DefendAsync(fixedKey, GetRandomIP()).Run());

            var ip = GetRandomIP();
            Assert.AreEqual(
                HackerSprayer.Result.Allowed,
                HackerSprayer.DefendAsync(keyGenerator(), ip).Run(),
                "Allow traffic from aonther key on same IP");

            WaitForIntervalToElapse(HackerSprayer.Config.MaxHitsPerKeyInterval, startTime);

            // Hit from another IP using same key should be allowed
            Assert.AreEqual(
                HackerSprayer.Result.Allowed,
                HackerSprayer.DefendAsync(fixedKey, GetRandomIP()).Run(),
                "After expiration time, key must be unblocked");
        }

        [TestMethod]
        public void TestMaxHitsPerOrigin()
        {
            HackerSprayer.Config.MaxHitsPerKey = 20;
            HackerSprayer.Config.MaxHitsPerOrigin = 10;
            HackerSprayer.Config.MaxHitsPerKeyPerOrigin = 20;

            var ip = GetRandomIP();
            Func<string> keyGenerator = () => { return "TestMaxHitsPerOrigin" + GetRandomKey(); };

            var startTime = DateTime.Now;
            var result = Parallel.For(0, HackerSprayer.Config.MaxHitsPerOrigin,
                hit =>
                {
                    Assert.AreEqual(
                    HackerSprayer.Result.Allowed,
                    HackerSprayer.DefendAsync(keyGenerator(), ip).Run()
                    );
                });

            while (!result.IsCompleted)
                Thread.Sleep(100);

            Assert.AreEqual(
                HackerSprayer.Config.MaxHitsPerOrigin,
                HackerSprayer.GetHitsFromOrigin(ip).Run(),
                "Number of hits recorded must match");


            // No more requests from same IP
            Assert.AreEqual(
                    HackerSprayer.Result.TooManyHitsFromOrigin,
                    HackerSprayer.DefendAsync(keyGenerator(), ip).Run()
                    );

            // Allow requests from other IPs
            Assert.AreEqual(
                HackerSprayer.Result.Allowed,
                HackerSprayer.DefendAsync(keyGenerator(), GetRandomIP()).Run());


            WaitForIntervalToElapse(HackerSprayer.Config.MaxHitsPerOriginInterval, startTime);

            Assert.AreEqual(
                    HackerSprayer.Result.Allowed,
                    HackerSprayer.DefendAsync(keyGenerator(), ip).Run(),
                    "Allow hits from same origin after expiration time."
                    );
        }

        [TestMethod]
        public void TestMaxHitsOnKeyPerOrigin()
        {
            HackerSprayer.Config.MaxHitsPerKey = 20;
            HackerSprayer.Config.MaxHitsPerOrigin = 20;
            HackerSprayer.Config.MaxHitsPerKeyPerOrigin = 10;

            Func<string> keyGenerator = () => { return "TestMaxHitsOnKeyPerOrigin" + GetRandomKey(); };
            var key = keyGenerator();
            var ip = GetRandomIP();

            var startTime = DateTime.Now;
            Parallel.For(0, HackerSprayer.Config.MaxHitsPerKeyPerOrigin,
                hit =>
                {
                    Assert.AreEqual(
                        HackerSprayer.Result.Allowed,
                        HackerSprayer.DefendAsync(key, ip).Run(),
                        "Allow hits on same key and IP");
                });
            
            Assert.AreEqual(
                HackerSprayer.Config.MaxHitsPerKeyPerOrigin,
                HackerSprayer.GetHitsForKey(key).Run(),
                "Number of hits recorded must match");

            // No more requests from same key and IP
            Assert.AreEqual(
                HackerSprayer.Result.TooManyHitsOnKeyFromOrigin,
                HackerSprayer.DefendAsync(key, ip).Run());

            Assert.AreEqual(
                HackerSprayer.Result.Allowed,
                HackerSprayer.DefendAsync(keyGenerator(), ip).Run(),
                "From different key, same IP, allow");

            Assert.AreEqual(
                HackerSprayer.Result.Allowed,
                HackerSprayer.DefendAsync(key, GetRandomIP()).Run(),
                "From different IP, same key, allow");

            WaitForIntervalToElapse(HackerSprayer.Config.MaxHitsPerOriginInterval, startTime);

            Assert.AreEqual(
                        HackerSprayer.Result.Allowed,
                        HackerSprayer.DefendAsync(key, ip).Run(),
                        "Allow hits on same key and IP after expiration");
        }

        [TestMethod]
        public void TestOriginBlaclistingAndWhitelisting()
        {
            var ip = GetRandomIP();
            var key = GetRandomKey();

            Assert.AreEqual(
                HackerSprayer.Result.Allowed,
                HackerSprayer.DefendAsync(key, ip).Run(),
                string.Format("Allow traffic on {0} from {1}", key, ip));

            HackerSprayer.BlacklistOriginAsync(ip).Run();

            Assert.AreEqual(
                HackerSprayer.Result.OriginBlocked,
                HackerSprayer.DefendAsync(key, ip).Run(),
                string.Format("Block traffic on {0} from {1}", key, ip));

            Assert.AreEqual(
                HackerSprayer.Result.OriginBlocked,
                HackerSprayer.DefendAsync(GetRandomKey(), ip).Run(),
                string.Format("Allow any traffic from {1}", key, ip));

            HackerSprayer.WhitelistOriginAsync(ip).Run();

            Assert.AreEqual(
                HackerSprayer.Result.Allowed,
                HackerSprayer.DefendAsync(key, ip).Run(),
                string.Format("Allow traffic on {0} from {1} after whitelist", key, ip));
        }

        [TestMethod]
        public void TestKeyBlacklistingAndWhitelisting()
        {
            var ip = GetRandomIP();
            var key = GetRandomKey();

            Assert.AreEqual(
                HackerSprayer.Result.Allowed,
                HackerSprayer.DefendAsync(key, ip).Run(),
                string.Format("Allow traffic on {0} from {1}", key, ip));

            HackerSprayer.BlacklistKeyAsync(key, TimeSpan.FromMinutes(5)).Run();

            Assert.AreEqual(
                HackerSprayer.Result.KeyBlocked,
                HackerSprayer.DefendAsync(key, ip).Run(),
                string.Format("Block traffic on {0} from {1}", key, ip));

            Assert.AreEqual(
                HackerSprayer.Result.KeyBlocked,
                HackerSprayer.DefendAsync(key, GetRandomIP()).Run(),
                string.Format("Allow any traffic from {1}", key, ip));

            HackerSprayer.WhitelistKeyAsync(key).Run();

            Assert.AreEqual(
                HackerSprayer.Result.Allowed,
                HackerSprayer.DefendAsync(key, ip).Run(),
                string.Format("Allow traffic on {0} from {1} after whitelist", key, ip));
        }

        [TestMethod]
        public void TestKeyCustomHitLimit()
        {
            HackerSprayer.Config.MaxHitsPerKey = 100;
            HackerSprayer.Config.MaxHitsPerOrigin = 100;
            HackerSprayer.Config.MaxHitsPerKeyPerOrigin = 100;

            var key = "InvalidLogin-" + GetRandomKey();
            var ip = GetRandomIP();
            var interval = TimeSpan.FromMinutes(1);
            var maxHits = 10;
            var startTime = DateTime.Now;

            Parallel.For(0, maxHits,
                hit =>
                {
                    Assert.AreEqual(
                    HackerSprayer.Result.Allowed,
                    HackerSprayer.DefendAsync(key, ip, 
                        interval, maxHits,
                        TimeSpan.MaxValue, long.MaxValue,
                        TimeSpan.MaxValue, long.MaxValue).Run(),
                    "Allow hits on key for custom interval");
                });

            Assert.AreEqual(
                    HackerSprayer.Result.TooManyHitsOnKey,
                    HackerSprayer.DefendAsync(key, ip, 
                        interval, maxHits,
                        TimeSpan.MaxValue, long.MaxValue,
                        TimeSpan.MaxValue, long.MaxValue).Run(),
                    "Must not allow hits on key after custom interval");

            Assert.AreEqual(
                    HackerSprayer.Result.Allowed,
                    HackerSprayer.DefendAsync("InvalidLogin-" + GetRandomKey(), ip, 
                        interval, maxHits,
                        TimeSpan.MaxValue, long.MaxValue,
                        TimeSpan.MaxValue, long.MaxValue).Run(),
                    "Allow hits on different key from same IP");

            WaitForIntervalToElapse(interval, startTime);

            Assert.AreEqual(
                    HackerSprayer.Result.Allowed,
                    HackerSprayer.DefendAsync(key, ip, 
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
                    HackerSprayer.IsKeyBlacklistedAsync(key).Run(),
                    "Key must not be blacklisted");

            var startTime = DateTime.Now;
            HackerSprayer.BlacklistKeyAsync(key, interval).Run();

            Assert.AreEqual(
                    true,
                    HackerSprayer.IsKeyBlacklistedAsync(key).Run(),
                    "Key must be blacklisted");

            string [] blacklistedKeys = HackerSprayer.GetKeyBlacklists().Run();
            Assert.IsTrue(Array.Exists(blacklistedKeys, k => k == key),
                "Key must be in the blacklist");

            WaitForIntervalToElapse(interval, startTime);

            Assert.AreEqual(
                    false,
                    HackerSprayer.IsKeyBlacklistedAsync(key).Run(),
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
                    HackerSprayer.isOriginBlacklistedAsync(ip).Run(),
                    "Origin must not be blacklisted");

            var startTime = DateTime.Now;
            HackerSprayer.BlacklistOriginAsync(ip, interval).Run();

            Assert.AreEqual(
                    true,
                    HackerSprayer.isOriginBlacklistedAsync(ip).Run(),
                    "Origin must be blacklisted");

            WaitForIntervalToElapse(interval, startTime);

            Assert.AreEqual(
                    false,
                    HackerSprayer.isOriginBlacklistedAsync(ip).Run(),
                    "Origin must not be blacklisted after expiration time");
        }


        [TestMethod]
        public void TestOriginRangeBlocking()
        {
            HackerSprayer.ClearBlacklistsAsync().Run();
            HackerSprayer.ClearAllHitsAsync().Run();

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

            HackerSprayer.BlacklistOriginAsync(IPAddress.Parse("10.10.10.10"), IPAddress.Parse("10.11.10.10")).Run();
            HackerSprayer.BlacklistOriginAsync(IPAddress.Parse("9.1.1.1"), IPAddress.Parse("9.10.10.9")).Run();
            HackerSprayer.BlacklistOriginAsync(IPAddress.Parse("10.11.10.12"), IPAddress.Parse("127.254.254.254")).Run();
            HackerSprayer.BlacklistOriginAsync(IPAddress.Parse("128.10.10.12"), IPAddress.Parse("128.10.10.254")).Run();

            Array.ForEach(ipsInRange, ip => 
                Assert.AreEqual(HackerSprayer.Result.OriginBlocked,
                    HackerSprayer.DefendAsync("TestOriginRangeBlocking", ip).Run(),
                    ip.ToString() + " must be blocked."));

            HackerSprayer.ClearAllHitsAsync().Run();

            Array.ForEach(ipsOutofRange, ip => 
                Assert.AreEqual(HackerSprayer.Result.Allowed,
                    HackerSprayer.DefendAsync("TestOriginRangeBlocking", ip).Run(),
                    ip.ToString() + " must be allowed"));

            HackerSprayer.WhitelistOriginAsync(IPAddress.Parse("9.1.1.1"), IPAddress.Parse("9.10.10.9")).Run();

            Array.ForEach(new[]
                {
                    IPAddress.Parse("9.1.1.1"),
                    IPAddress.Parse("9.1.1.10"),
                    IPAddress.Parse("9.10.10.9")
                }, 
                ip => 
                    Assert.AreEqual(HackerSprayer.Result.Allowed,
                        HackerSprayer.DefendAsync("TestOriginRangeBlocking", ip).Run(),
                        ip.ToString() + " must be allowed"));

            HackerSprayer.ClearBlacklistsAsync().Run();
            HackerSprayer.ClearAllHitsAsync().Run();

            Array.ForEach(ipsInRange, ip => 
                Assert.AreEqual(HackerSprayer.Result.Allowed,
                    HackerSprayer.DefendAsync("TestOriginRangeBlocking", ip).Run(),
                    ip.ToString() + " must be allowed when there's no blacklisting."));

            HackerSprayer.ClearAllHitsAsync().Run();

            Array.ForEach(ipsOutofRange, ip => 
                Assert.AreEqual(HackerSprayer.Result.Allowed,
                    HackerSprayer.DefendAsync("TestOriginRangeBlocking", ip).Run(),
                    ip.ToString() + " must be allowed when there's no blacklisting"));
        }
    }
}
