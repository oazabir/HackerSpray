using HackerSpray.Module;
using HackerSpray.Web;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace HackerSpray.SampleWebSite
{
    public class MvcApplication : System.Web.HttpApplication
    {

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            if (Convert.ToBoolean(ConfigurationManager.AppSettings["HackerSprayEnabled"]))
            {
                HackerSprayer.Store = new RedisDefenceStore(ConfigurationManager.AppSettings["RedisConnection"], ConfigurationManager.AppSettings["RedisPrefix"], HackerSprayer.Config);
                HackerSprayModule.PathToDefend = ConfigurationManager.AppSettings["PathToDefend"] ?? "/";
            }
        }
    }
}
