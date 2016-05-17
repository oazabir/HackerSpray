using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using HackerSpray.SampleWebSite.Models;
using HackerSpray.SampleWebSite.Constants;
using HackerSpray.Module;
using System.Net;
using System.Threading.Tasks;

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
            var invalidLoginkey = "InvalidLogin-" + username;
            
            // If username is blacklisted for some reason, reject
            if (await HackerSprayer.IsKeyBlacklisted(invalidLoginkey))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            var user = DataStore.Users.Where(u => u.Username == username && u.Password == password).FirstOrDefault();

			if (user != null)
			{
                // Prevent DOS attack using valid username, password. 
                // Maybe trying to generate many Session ID and guess
                // how it is generated.
                var validLoginKey = "ValidLogin-" + username;

                var result = await HackerSprayer.Defend(
                    validLoginKey, 
                    IPAddress.Parse(Request.UserHostAddress).MapToIPv4(), 
                    MaxValidLoginInterval, 
                    MaxValidLogin);

                if (result == HackerSprayer.Result.TooManyHitsOnKey)
                {
                    // Too many valid login on same username. 
                    await HackerSprayer.BlacklistKey(validLoginKey, MaxValidLoginInterval);
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }
                else
                {
                    Session[SessionConstants.USER] = user;
                    return RedirectToAction("Index", "Home");
                }
			}
			else
			{            
                // Check for too many invalid login on a username    
                var result = await HackerSprayer.Defend(
                    invalidLoginkey, 
                    IPAddress.Parse(Request.UserHostAddress).MapToIPv4(), 
                    MaxInvalidLoginInterval,
                    MaxInvalidLogin);

                if (result == HackerSprayer.Result.TooManyHitsOnKey)
                {
                    await HackerSprayer.BlacklistKey(invalidLoginkey, MaxInvalidLoginInterval);
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }

				TempData[TempDataConstants.ERROR_MESSAGE] = "Invalid username or password";
				return View("~/Views/Account/LogOn.cshtml");
            }
		}

		//
		// GET: /Account/LogOff

		public ActionResult LogOff()
		{
			Session.Remove(SessionConstants.USER);
			TempData[TempDataConstants.SUCCESS_MESSAGE] = "You have been logged off";
			return RedirectToAction("Index", "Home");
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
