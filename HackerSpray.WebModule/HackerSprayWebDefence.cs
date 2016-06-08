using HackerSpray.Module;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static readonly string ClassName = typeof(HackerSprayWebDefence).Name;

        public static async Task<HackerSprayer.Result> DefendURL(HttpContext context)
        {
            HackerSprayer.Result result = HackerSprayer.Result.Allowed;

            if (!Initialized)
            {
                lock(lockObject)
                {
                    if (!Initialized)
                    {
                        Trace.TraceInformation(ClassName, "Initialize");
                        Initialized = true;
                        HackerSprayer.Store = new RedisDefenceStore(HackerSprayConfig.Settings.Redis,
                            HackerSprayConfig.Settings.Prefix,
                            HackerSprayer.Config);
                        Trace.TraceInformation(ClassName + " Initialized");
                    }
                }
            }

            // This handles load balancers passing the original client IP
            // through this header. 
            // WARNING: If your load balancer is not passing original client IP
            // through this header, then you will be blocking your load balancer,
            // causing a total outage. Also ensure this Header cannot be spoofed.
            // Your load balancer should be configured in a way that it does not accept
            // this header from the request, instead it always sets it itself.
            var originIP = IPAddress.Parse(context.Request.Headers["X-Forwarded-For"]
               ?? context.Request.UserHostAddress).MapToIPv4();

            foreach (var path in HackerSprayConfig.Settings.Paths)
            {
                if ((path.Post && context.Request.HttpMethod == "POST")
                    || (!path.Post && context.Request.HttpMethod == "GET")
                    && path.Name == context.Request.Path)
                {
                    Trace.TraceInformation(ClassName + " Path matched" + context.Request.Path);
                    if (path.Mode == "key")
                    {
                        result = await HackerSprayer.DefendAsync(context.Request.Path, originIP,
                            path.Interval, path.MaxAttempts,
                            TimeSpan.MaxValue, long.MaxValue,
                            TimeSpan.MaxValue, long.MaxValue);

                        if (result == HackerSprayer.Result.TooManyHitsOnKey)
                        {
                            Trace.TraceInformation(ClassName + " TooManyHitsOnKey Blacklist Path:" + context.Request.Path);
                            await HackerSprayer.BlacklistKeyAsync(path.Name, path.Interval);
                        }
                    }
                    else if (path.Mode == "origin")
                    {
                        result = await HackerSprayer.DefendAsync(context.Request.Path, originIP,
                            TimeSpan.MaxValue, long.MaxValue,
                            path.Interval, path.MaxAttempts,
                            TimeSpan.MaxValue, long.MaxValue);
                        if (result == HackerSprayer.Result.TooManyHitsFromOrigin)
                        {
                            Trace.TraceInformation(ClassName + " TooManyHitsFromOrigin Blacklist origin:" + originIP);
                            await HackerSprayer.BlacklistOriginAsync(originIP, path.Interval);
                        }
                    }
                    else //(path.Mode == "key+origin")
                    {
                        result = await HackerSprayer.DefendAsync(context.Request.Path, originIP,
                            TimeSpan.MaxValue, long.MaxValue,
                            TimeSpan.MaxValue, long.MaxValue,
                            path.Interval, path.MaxAttempts);
                    }
                    
                    break;
                }
            }
                        
            return result;
        }
    }
}
