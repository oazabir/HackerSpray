using HackerSpray.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace HackerSpray.WebModule
{
    public class HackerSprayWebDefence
    {
        private static bool Initialized = false;
        private static object lockObject = new object();

        public static async Task<HackerSprayer.Result> DefendURL(HttpContext context)
        {
            HackerSprayer.Result result = HackerSprayer.Result.Allowed;

            if (!Initialized)
            {
                lock(lockObject)
                {
                    if (!Initialized)
                    {
                        Initialized = true;
                        HackerSprayer.Store = new RedisDefenceStore(HackerSprayConfig.Settings.Redis,
                            HackerSprayConfig.Settings.Prefix,
                            HackerSprayer.Config);
                    }
                }
            }

            // This handles load balancers passing the original client IP
            // through this header. 
            // WARNING: If you load balancer is not passing original client IP
            // through this header, then you will be blocking your load balancer,
            // causing a total outage. Also ensure this Header cannot be spoofed.
            var originIP = IPAddress.Parse(context.Request.Headers["X-Forward-For"]
               ?? context.Request.UserHostAddress).MapToIPv4();

            foreach (var path in HackerSprayConfig.Settings.Paths)
            {
                if ((path.Post && context.Request.HttpMethod == "POST")
                    || (!path.Post && context.Request.HttpMethod == "GET")
                    && path.Name == context.Request.Path)
                {
                    if (path.Mode == "perkey")
                        result = await HackerSprayer.DefendAsync(context.Request.Path, originIP,
                            path.Interval, path.MaxAttempts,
                            TimeSpan.MaxValue, long.MaxValue,
                            TimeSpan.MaxValue, long.MaxValue);
                    else if (path.Mode == "perorigin")
                        result = await HackerSprayer.DefendAsync(context.Request.Path, originIP,
                            TimeSpan.MaxValue, long.MaxValue,
                            path.Interval, path.MaxAttempts,
                            TimeSpan.MaxValue, long.MaxValue);
                    else //(path.Mode == "perkeyorigin")
                        result = await HackerSprayer.DefendAsync(context.Request.Path, originIP,
                            TimeSpan.MaxValue, long.MaxValue,
                            TimeSpan.MaxValue, long.MaxValue,
                            path.Interval, path.MaxAttempts);

                    // Blacklist origin. After that, it becomes least expensive to block requests
                    if (result == HackerSprayer.Result.TooManyHitsFromOrigin)
                        await HackerSprayer.BlacklistOriginAsync(originIP, path.Interval);
                    else if (result == HackerSprayer.Result.TooManyHitsOnKey)
                        await HackerSprayer.BlacklistKeyAsync(path.Name, path.Interval);
                    //else if (result == HackerSprayer.Result.TooManyHitsOnKeyFromOrigin)
                    // There is nothing for that, because we neither want to block the key for all ip,
                    // nor do we want to block the ip for all key. It just has to be checked each 
                    // and every time. Thus this is the most expensive check.
                    break;
                }
            }

            return result;
        }
    }
}
