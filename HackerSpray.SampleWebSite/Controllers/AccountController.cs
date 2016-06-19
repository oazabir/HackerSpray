using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using HackerSpray.SampleWebSite.Models;
using HackerSpray.SampleWebSite.Constants;
using System.Net;
using System.Threading.Tasks;
using System.Configuration;
using HackerSpray.Module;

namespace HackerSpray.SampleWebSite.Controllers
{
	public class AccountController : Controller
	{
        public static int MaxValidLogin = 10;
        public static TimeSpan MaxValidLoginInterval = TimeSpan.FromMinutes(10);

        public static int MaxInvalidLogin = 3;
        public static TimeSpan MaxInvalidLoginInterval = TimeSpan.FromMinutes(15);
        //
        // GET: /Account/LogOn

        public ActionResult LogOn()
		{
			return View("~/Views/Account/LogOn.cshtml");
        }
        
		//
		// POST: /Account/LogOn

		[HttpPost]
		public async Task<ActionResult> LogOn(string username, string password)
		{
            // This handles load balancers passing the original client IP
            // through this header. 
            // WARNING: If you load balancer is not passing original client IP
            // through this header, then you will be blocking your load balancer,
            // causing a total outage. Also ensure this Header cannot be spoofed.
            var originIP = Request.GetClientIp();

            // Don't forget to do this check!
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData[TempDataConstants.ERROR_MESSAGE] = "Invalid username or password";
                return View("~/Views/Account/LogOn.cshtml");
            }

            return await Hacker.DefendAsync<ActionResult>(async (success, fail) =>
                {
                    var user = DataStore.Users.Where(u => u.Username == username 
                                                && u.Password == password).FirstOrDefault();
                    if (user!= null)
                    {
                        Session[SessionConstants.USER] = user;
                        return await success(RedirectToAction("Index", "Home"));
                    }
                    else
                    {
                        TempData[TempDataConstants.ERROR_MESSAGE] = "Invalid username or password";
                        return await fail(View("~/Views/Account/LogOn.cshtml"));
                    }
                },
                blocked => new HttpStatusCodeResult(HttpStatusCode.Forbidden),
                "ValidLogin:" + username, MaxValidLogin, MaxValidLoginInterval,
                "InvalidLogin:" + username, MaxInvalidLogin, MaxInvalidLoginInterval,
                originIP
            );            
            
		}

		//
		// GET: /Account/LogOff

		public ActionResult LogOff()
		{
			Session.Remove(SessionConstants.USER);
			TempData[TempDataConstants.SUCCESS_MESSAGE] = "You have been logged off";
			return RedirectToAction("Index", "Home");
		}

        public async Task<ActionResult> ClearAllBlocks()
        {
            await Hacker.ClearAllHitsAsync();
            await Hacker.ClearBlacklistsAsync();
            return LogOff();
        }

        //
        // GET: /Account/Register

        public ActionResult Register()
		{
			return View("~/Views/Account/Register.cshtml");
        }

		//
		// POST: /Account/Register

		[HttpPost]
		public ActionResult Register(User user)
		{
			if (ModelState.IsValid)
			{
				DataStore.Users.Add(user);
				Session[SessionConstants.USER] = user;
				TempData[TempDataConstants.SUCCESS_MESSAGE] = "Your account has been created";
				return RedirectToAction("Index", "Home");
			}

			// If we got this far, something failed, redisplay form
			return View("~/Views/Account/Register.cshtml", user);
		}
		
	}
}
