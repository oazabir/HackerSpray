using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HackerSpray.Module;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HackerSpray.UnitTests
{
    public static class Extensions
    {
        public static TResult Run<TResult>(this Task<TResult> t)
        {
            return t.GetAwaiter().GetResult();
        }

    }
    [TestClass]
    public class HackerSprayTest
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
            var result = HackerSprayer.Defend("TestAllowed" + GetRandomKey(), GetRandomIP()).Run();
            Assert.AreEqual(HackerSprayer.DefenceResult.Allowed, result);
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
            for (int i = 0; i < HackerSprayer.Config.MaxHitsPerKey; i++)
            {
                Assert.AreEqual(
                    HackerSprayer.DefenceResult.Allowed,
                    HackerSprayer.Defend(fixedKey, GetRandomIP()).Run()
                    );
            }

            Assert.AreEqual(
                HackerSprayer.DefenceResult.TooManyHitsOnKey,
                HackerSprayer.Defend(fixedKey, GetRandomIP()).Run());

            var ip = GetRandomIP();
            Assert.AreEqual(
                HackerSprayer.DefenceResult.Allowed,
                HackerSprayer.Defend(keyGenerator(), ip).Run(),
                "Allow traffic from aonther key on same IP");

            // Wait for max key interval to expire, then try another hit from same key but different IP
            Thread.Sleep(HackerSprayer.Config.MaxHitsPerKeyInterval - (DateTime.Now - startTime));
            Thread.Sleep(TimeSpan.FromSeconds(1));

            // Hit from another IP using same key should be allowed
            Assert.AreEqual(
                HackerSprayer.DefenceResult.Allowed,
                HackerSprayer.Defend(fixedKey, GetRandomIP()).Run(),
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

            for (int i = 0; i < HackerSprayer.Config.MaxHitsPerOrigin; i++)
            {
                Assert.AreEqual(
                    HackerSprayer.DefenceResult.Allowed,
                    HackerSprayer.Defend(keyGenerator(), ip).Run()
                    );
            }

            // No more requests from same IP
            Assert.AreEqual(
                    HackerSprayer.DefenceResult.TooManyHitsFromOrigin,
                    HackerSprayer.Defend(keyGenerator(), ip).Run()
                    );

            // Allow requests from other IPs
            Assert.AreEqual(
                HackerSprayer.DefenceResult.Allowed,
                HackerSprayer.Defend(keyGenerator(), GetRandomIP()).Run());

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
            for (int i = 0; i < HackerSprayer.Config.MaxHitsPerKeyPerOrigin; i++)
            {
                Assert.AreEqual(
                    HackerSprayer.DefenceResult.Allowed,
                    HackerSprayer.Defend(key, ip).Run(),
                    "Allow hits on same key and IP"
                    );
            }

            // No more requests from same key and IP
            Assert.AreEqual(
                HackerSprayer.DefenceResult.TooManyHitsOnKeyFromOrigin,
                HackerSprayer.Defend(key, ip).Run());

            Assert.AreEqual(
                HackerSprayer.DefenceResult.Allowed,
                HackerSprayer.Defend(keyGenerator(), ip).Run(),
                "From different key, same IP, allow");

            Assert.AreEqual(
                HackerSprayer.DefenceResult.Allowed,
                HackerSprayer.Defend(key, GetRandomIP()).Run(),
                "From different IP, same key, allow");            
            
        }

        [TestMethod]
        public void TestOriginBlaclistingAndWhitelisting()
        {
            var ip = GetRandomIP();
            var key = GetRandomKey();

            Assert.AreEqual(
                HackerSprayer.DefenceResult.Allowed,
                HackerSprayer.Defend(key, ip).Run(),
                string.Format("Allow traffic on {0} from {1}", key, ip));

            HackerSprayer.BlacklistOrigin(ip).Run();

            Assert.AreEqual(
                HackerSprayer.DefenceResult.OriginBlocked,
                HackerSprayer.Defend(key, ip).Run(),
                string.Format("Block traffic on {0} from {1}", key, ip));

            Assert.AreEqual(
                HackerSprayer.DefenceResult.OriginBlocked,
                HackerSprayer.Defend(GetRandomKey(), ip).Run(),
                string.Format("Allow any traffic from {1}", key, ip));

            HackerSprayer.WhitelistOrigin(ip).Run();

            Assert.AreEqual(
                HackerSprayer.DefenceResult.Allowed,
                HackerSprayer.Defend(key, ip).Run(),
                string.Format("Allow traffic on {0} from {1} after whitelist", key, ip));
        }

        [TestMethod]
        public void TestKeyBlaclistingAndWhitelisting()
        {
            var ip = GetRandomIP();
            var key = GetRandomKey();

            Assert.AreEqual(
                HackerSprayer.DefenceResult.Allowed,
                HackerSprayer.Defend(key, ip).Run(),
                string.Format("Allow traffic on {0} from {1}", key, ip));

            HackerSprayer.BlacklistKey(key, TimeSpan.FromMinutes(5)).Run();

            Assert.AreEqual(
                HackerSprayer.DefenceResult.KeyBlocked,
                HackerSprayer.Defend(key, ip).Run(),
                string.Format("Block traffic on {0} from {1}", key, ip));

            Assert.AreEqual(
                HackerSprayer.DefenceResult.KeyBlocked,
                HackerSprayer.Defend(key, GetRandomIP()).Run(),
                string.Format("Allow any traffic from {1}", key, ip));

            HackerSprayer.WhitelistKey(key).Run();

            Assert.AreEqual(
                HackerSprayer.DefenceResult.Allowed,
                HackerSprayer.Defend(key, ip).Run(),
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
            for (int i = 0; i < maxHits; i++)
            {
                Assert.AreEqual(
                    HackerSprayer.DefenceResult.Allowed,
                    HackerSprayer.Defend(key, ip, interval, maxHits).Run(),
                    "Allow hits on key for custom interval");
            }

            Assert.AreEqual(
                    HackerSprayer.DefenceResult.TooManyHitsOnKey,
                    HackerSprayer.Defend(key, ip, interval, maxHits).Run(),
                    "Must not allow hits on key after custom interval");

            Assert.AreEqual(
                    HackerSprayer.DefenceResult.Allowed,
                    HackerSprayer.Defend("InvalidLogin-" + GetRandomKey(), ip, interval, maxHits).Run(),
                    "Allow hits on different key from same IP");

            // Wait for max key interval to expire, then try another hit from same key but different IP
            Thread.Sleep(interval - (DateTime.Now - startTime));
            Thread.Sleep(TimeSpan.FromSeconds(1));

            Assert.AreEqual(
                    HackerSprayer.DefenceResult.Allowed,
                    HackerSprayer.Defend(key, ip, interval, maxHits).Run(),
                    "Allow hits on key for after interval has passed.");
        }
    }
}
