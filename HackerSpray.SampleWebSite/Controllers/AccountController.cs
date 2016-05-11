using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using HackerSpray.SampleWebSite.Models;
using HackerSpray.SampleWebSite.Constants;

namespace HackerSpray.SampleWebSite.Controllers
{
	public class AccountController : Controller
	{

		//
		// GET: /Account/LogOn

		public ActionResult LogOn()
		{
			return View("~/Views/Account/LogOn.cshtml");
        }

		//
		// POST: /Account/LogOn

		[HttpPost]
		public ActionResult LogOn(string username, string password)
		{
			var user = DataStore.Users.Where(u => u.Username == username && u.Password == password).FirstOrDefault();

			if (user != null)
			{
				Session[SessionConstants.USER] = user;
                
				return RedirectToAction("Index", "Home");
			}
			else
			{
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
