using HackerSpray.Module;
using System;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace HackerSpray.SampleWebSite.Modules
{
    public class HackerSprayModule : IHttpModule
    {
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
            HackerSprayer.Store = new RedisDefenceStore("localhost", "HackerSpray.SampleWebSite-", HackerSprayer.Config);
                
            var wrapper = new EventHandlerTaskAsyncHelper(DefendRequest);
            context.AddOnBeginRequestAsync(wrapper.BeginEventHandler, wrapper.EndEventHandler);
        }       

        async Task DefendRequest(object sender, EventArgs e)
        {
            var context = HttpContext.Current;

            if (context.Request.HttpMethod == "POST" && context.Request.Path == "/Account/LogOn")
            {
                var result = await HackerSprayer.Defend(context.Request.Path, IPAddress.Parse(context.Request.UserHostAddress));
                if (result != HackerSprayer.DefenceResult.Allowed)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    context.Response.Status = Enum.GetName(typeof(HackerSprayer.DefenceResult), result);
                    context.Response.End();
                }
            }
            
        }

        #endregion
    }
}
