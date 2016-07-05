
using HackerSpray.Module;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace HackerSpray.WebModule
{
    public class HackerSprayHttpModule : IHttpModule
    {
        public static string PathToDefend;
        private static readonly string ClassName = typeof(HackerSprayHttpModule).Name;

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

            Hacker.Result result = await HackerSprayWebDefence.DefendURL(context);
            var resultName = Enum.GetName(typeof(Hacker.Result), result);

            if (result != Hacker.Result.Allowed)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotAcceptable;
                context.Response.StatusDescription = resultName;
                context.Response.End();
            }

        }        

        #endregion
    }
}
