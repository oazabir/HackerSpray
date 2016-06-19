using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace HackerSpray.Module
{
    public static class HackerSprayWebExtensions
    {
        public static IPAddress GetClientIp(this HttpRequestBase request)
        {
            return IPAddress.Parse(request.Headers["X-Forwarded-For"]
               ?? request.UserHostAddress).MapToIPv4();
        }

        public static IPAddress GetClientIp(this HttpRequest request)
        {
            return IPAddress.Parse(request.Headers["X-Forwarded-For"]
               ?? request.UserHostAddress).MapToIPv4();
        }
    }
}
