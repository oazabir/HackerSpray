
using HackerSpray.Module;
using System;
using System.Configuration;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace HackerSpray.Web
{
    public class HackerSprayModule : IHttpModule
    {
        public static string PathToDefend;
        
        /// <summary>
        /// You will need to configure this module in the Web.config file of your
        /// web and register it with IIS before being able to use it. For more information
        /// see the following link: http://go.microsoft.com/?linkid=8101007
        /// </summary>
#region IHttpModule Members

        public void Dispose()
        {
            //clean-up code here.
        }

        public void Init(HttpApplication context)
        {               
            var wrapper = new EventHandlerTaskAsyncHelper(DefendRequest);
            context.AddOnBeginRequestAsync(wrapper.BeginEventHandler, wrapper.EndEventHandler);
        }       

        async Task DefendRequest(object sender, EventArgs e)
        {
            var context = HttpContext.Current;

            // Block too many HTTP POST attempt on LogOn page
            if (context.Request.HttpMethod == "POST" && context.Request.Path == PathToDefend)
            {
                var ip = IPAddress.Parse(context.Request.Headers["OriginIP"] ?? context.Request.UserHostAddress).MapToIPv4();
                var result = await HackerSprayer.DefendAsync(context.Request.Path, ip);

                // Blacklist origin. After that, it becomes least expensive to block requests
                if (result == HackerSprayer.Result.TooManyHitsFromOrigin
                    || result == HackerSprayer.Result.TooManyHitsOnKeyFromOrigin)
                    await HackerSprayer.BlacklistOriginAsync(ip);

                if (result != HackerSprayer.Result.Allowed
                    && result != HackerSprayer.Result.TooManyHitsOnKey)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    context.Response.StatusDescription = Enum.GetName(typeof(HackerSprayer.Result), result);
                    context.Response.End();
                }
            }
            
        }

#endregion
    }
}
