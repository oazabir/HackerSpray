using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HackerSpray.Middleware.Test
{
    public class XForwardForMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        private const string XForwardedForHeaderName = "X-Forwarded-For";
        private const string XOriginalPortName = "X-Original-Port";
        private const string XOriginalProtoName = "X-Original-Proto";
        private const string XOriginalIPName = "X-Original-IP";

        public XForwardForMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, HackerSprayOption option)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<XForwardForMiddleware>();
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var xForwardedForHeaderValue = httpContext.Request.Headers.GetCommaSeparatedValues(XForwardedForHeaderName);
            if (xForwardedForHeaderValue != null && xForwardedForHeaderValue.Length > 0)
            {
                IPAddress ipFromHeader;
                string[] ipparts = xForwardedForHeaderValue[0].Split(':');
                if (IPAddress.TryParse(ipparts[0], out ipFromHeader))
                {
                    var connection = httpContext.Connection;
                    var remoteIPString = connection.RemoteIpAddress?.ToString();
                    if (!string.IsNullOrEmpty(remoteIPString))
                    {
                        httpContext.Request.Headers[XOriginalIPName] = remoteIPString;
                    }
                    int port;
                    if (ipparts.Length > 1)
                    {
                        if (int.TryParse(ipparts[1], out port))
                        {
                            if (connection.RemotePort != 0)
                            {
                                httpContext.Request.Headers[XOriginalPortName] = connection.RemotePort.ToString(CultureInfo.InvariantCulture);
                            }
                            connection.RemotePort = port;
                        }
                    }
                    connection.RemoteIpAddress = ipFromHeader;
                }
            }

            if (httpContext.Request.HttpContext.Connection.RemoteIpAddress == null)
                httpContext.Request.HttpContext.Connection.RemoteIpAddress = IPAddress.Loopback;

            await _next(httpContext);
        }
    }

    public static class ClientIPMiddlewareExtensions
    {
        public static IApplicationBuilder UseXForwardFor(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<XForwardForMiddleware>();
        }
    }
}
