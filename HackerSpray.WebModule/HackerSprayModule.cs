
using HackerSpray.Module;
using System;
using System.Configuration;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace HackerSpray.WebModule
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
            var context = ((HttpApplication)sender).Context;

            HackerSprayer.Result result = await HackerSprayWebDefence.DefendURL(context);

            if (result != HackerSprayer.Result.Allowed)
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                context.Response.StatusDescription = Enum.GetName(typeof(HackerSprayer.Result), result);
                context.Response.End();
            }

        }

        

        #endregion
    }
}
