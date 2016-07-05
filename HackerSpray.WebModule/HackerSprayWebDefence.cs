using HackerSpray.Logger;
using HackerSpray.Module;
using Microsoft.Extensions.Logging;
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

        public static async Task<Hacker.Result> DefendURL(HttpContext context)
        {
            Hacker.Result result = Hacker.Result.Allowed;
            Stopwatch watch = new Stopwatch();
            watch.Start();

            if (!Initialized)
            {
                lock(lockObject)
                {
                    if (!Initialized)
                    {
                        Hacker.Logger = new TraceLogger();
                        Hacker.Logger.LogInformation(ClassName + ' ' + "Initialize");
                        Hacker.Store = new RedisDefenceStore(HackerSprayConfig.Settings.Redis,
                            HackerSprayConfig.Settings.Prefix,
                            Hacker.Config);
                        Hacker.Logger.LogInformation(ClassName + ' ' + " Initialized");
                        Initialized = true;

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
            var originIP = context.Request.GetClientIp();

            foreach (var path in HackerSprayConfig.Settings.Paths)
            {
                if ((path.Post && context.Request.HttpMethod == "POST")
                    || (!path.Post && context.Request.HttpMethod == "GET")
                    && path.Name == context.Request.Path)
                {
                    Hacker.Logger.LogDebug(ClassName + ' ' + "Path matched: " + context.Request.Path);
                    if (path.Mode == "key")
                    {
                        result = await Hacker.DefendAsync(context.Request.Path, originIP,
                            path.Interval, path.MaxAttempts,
                            TimeSpan.MaxValue, long.MaxValue,
                            TimeSpan.MaxValue, long.MaxValue);

                        if (result == Hacker.Result.TooManyHitsOnKey)
                        {
                            Hacker.Logger.LogInformation(ClassName + ' ' + "TooManyHitsOnKey Blacklist Path: " + context.Request.Path);
                            await Hacker.BlacklistKeyAsync(path.Name, path.Interval);
                        }
                    }
                    else if (path.Mode == "origin")
                    {
                        result = await Hacker.DefendAsync(context.Request.Path, originIP,
                            TimeSpan.MaxValue, long.MaxValue,
                            path.Interval, path.MaxAttempts,
                            TimeSpan.MaxValue, long.MaxValue);
                        if (result == Hacker.Result.TooManyHitsFromOrigin)
                        {
                            Hacker.Logger.LogInformation(ClassName + ' ' + "TooManyHitsFromOrigin Blacklist origin: " + originIP);
                            await Hacker.BlacklistOriginAsync(originIP, path.Interval);
                        }
                    }
                    else //(path.Mode == "key+origin")
                    {
                        result = await Hacker.DefendAsync(context.Request.Path, originIP,
                            TimeSpan.MaxValue, long.MaxValue,
                            TimeSpan.MaxValue, long.MaxValue,
                            path.Interval, path.MaxAttempts);
                    }
                    
                    break;
                }
            }

            watch.Stop();
            Hacker.Logger.LogDebug(ClassName + ' ' + "DefendURL: " + context.Request.Path + " " + watch.ElapsedMilliseconds);
            return result;
        }
    }
}
