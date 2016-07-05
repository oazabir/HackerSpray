using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using HackerSpray.Module;
using System.Net;
using System.Diagnostics;

namespace HackerSpray.Middleware
{
    
    public class HackerSprayerMiddleware 
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private static HackerSprayOption _option;
        private static HackerSprayOptionKey[] _keys;
        private const string XForwardedForHeader = "X-Forwarded-For";

        private static object _lockObject = new object();
        public HackerSprayerMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, HackerSprayOption option)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger("HackerSpray");

            if (_option == null)
            {
                lock (_lockObject)
                {
                    if (_option == null)
                    {
                        Debug("Initializing HackerSpray options.");

                        _option = option;

                        Hacker.Logger = _logger;
                        Hacker.Store = new RedisDefenceStore(_option.Redis,
                            _option.Prefix, Hacker.Config);

                        _keys = new HackerSprayOptionKey[_option.Keys.Count];

                        for (var i = 0; i < _option.Keys.Count; i++)
                        {
                            var parts = _option.Keys[i].Split(' ');
                            if (parts.Length != 5)
                                throw new Exception("Each key must have exactly 4 parts - METHOD PATH MAXATTEMPTS INTERVAL MODE. But you have put: " + _option.Keys[i]);

                            long maxAttempts;
                            if (!long.TryParse(parts[2], out maxAttempts))
                                throw new Exception($"Invalid max attempts configured for key{parts[0]}. Must be a number.");

                            TimeSpan interval;
                            if (!TimeSpan.TryParse(parts[3], out interval))
                                throw new Exception($"Invalid interval configured for key{parts[0]}. Must be a TimeSpan eg 00:01:00.");

                            var mode = default(HackerSprayOptionKey.HitCountMode);
                            if (parts[4] == "key")
                                mode = HackerSprayOptionKey.HitCountMode.PerKey;
                            else if (parts[4] == "origin")
                                mode = HackerSprayOptionKey.HitCountMode.PerOrigin;
                            else
                                mode = HackerSprayOptionKey.HitCountMode.PerKeyOrigin;

                            _keys[i] = new HackerSprayOptionKey()
                            {
                                    Method = parts[0],
                                    Key = parts[1],
                                MaxAttempts = maxAttempts,
                                Interval = interval,
                                Mode = mode
                            };  
                        }

                        Debug("HackerSpray options processed");
                    }
                }
            }
        }

       
        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path;
            Stopwatch watch = new Stopwatch();
            watch.Start();

            if (path.HasValue)
            {
                Debug("Defend Begin: " + path);

                // This handles load balancers passing the original client IP
                // through this header. 
                // WARNING: If your load balancer is not passing original client IP
                // through this header, then you will be blocking your load balancer,
                // causing a total outage. Also ensure this Header cannot be spoofed.
                // Your load balancer should be configured in a way that it does not accept
                // this header from the request, instead it always sets it itself.
                var originIP = context.Connection.RemoteIpAddress;
                //if (context.Request.Headers.ContainsKey(XForwardedForHeader))
                //    originIP = IPAddress.Parse(context.Request.Headers[XForwardedForHeader]).MapToIPv4();

                var result = Hacker.Result.Allowed;
                foreach (var key in _keys)
                {
                    if (key.Method == context.Request.Method && key.Key == path)
                    {
                        Debug("Defend: " + path);
                        if (key.Mode == HackerSprayOptionKey.HitCountMode.PerKey)
                        {
                            result = await Hacker.DefendAsync(path, originIP,
                                key.Interval, key.MaxAttempts,
                                TimeSpan.MaxValue, long.MaxValue,
                                TimeSpan.MaxValue, long.MaxValue);
                            if (result == Hacker.Result.TooManyHitsOnKey)
                                await Hacker.BlacklistKeyAsync(path, key.Interval);
                        }
                        else if (key.Mode == HackerSprayOptionKey.HitCountMode.PerOrigin)
                        {
                            result = await Hacker.DefendAsync(path, originIP,
                                TimeSpan.MaxValue, long.MaxValue,
                                key.Interval, key.MaxAttempts,
                                TimeSpan.MaxValue, long.MaxValue);

                            if (result == Hacker.Result.TooManyHitsFromOrigin)
                                await Hacker.BlacklistOriginAsync(originIP, key.Interval);
                        }
                        else //(key.Item5 == Mode.PerKeyOrigin)
                            result = await Hacker.DefendAsync(path, originIP,
                                TimeSpan.MaxValue, long.MaxValue,
                                TimeSpan.MaxValue, long.MaxValue,
                                key.Interval, key.MaxAttempts);

                        Debug("Defend Result: " + Enum.GetName(typeof(Hacker.Result), result));
                        break;
                    }
                }

                watch.Stop();
                Debug("Defend End: " + path + " " + watch.ElapsedMilliseconds);

                if (result == Hacker.Result.Allowed)
                    await _next.Invoke(context);
                else
                {
                    Info("Blocked: " + path);

                    context.Response.StatusCode = (int)HttpStatusCode.NotAcceptable;
                    await context.Response.WriteAsync(Enum.GetName(typeof(Hacker.Result), result));
                }

                //watch.Stop();
                //Debug("Finished: " + path + " " + watch.ElapsedMilliseconds);
            }
            else
            {
                await _next.Invoke(context);
            }
        }

        private void Info(string msg)
        {
            _logger.LogInformation(msg);
        }

        private void Debug(string msg)
        {
            _logger.LogDebug(msg);
        }
    }
}
