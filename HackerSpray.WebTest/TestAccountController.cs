using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HackerSpray.SampleWebSite.Controllers;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Net;
using System.Threading;
using HackerSpray.Module;
using UnitTest.TestUtilities;
using Moq;
using HackerSpray.SampleWebSite.Constants;
using System.Configuration;
using HackerSpray.SampleWebSite.Models;

namespace HackerSpray.WebTest
{
    [TestClass]
    public class TestAccountController
    {
        [TestInitialize]
        public void Init()
        {
            HackerSprayer.Config.MaxHitsPerKey = 15;
            HackerSprayer.Config.MaxHitsPerOrigin = 20;
            HackerSprayer.Config.MaxHitsPerKeyPerOrigin = 15;
            HackerSprayer.Config.MaxHitsPerKeyInterval = TimeSpan.FromMinutes(1);
            HackerSprayer.Config.MaxHitsPerOriginInterval = TimeSpan.FromMinutes(1);
            HackerSprayer.Config.MaxHitsPerKeyPerOriginInterval = TimeSpan.FromMinutes(1);

            ConfigurationManager.AppSettings["HackerSprayEnabled"] = "true";
            HackerSprayer.Store = new RedisDefenceStore("localhost", "HttpDefenceTest-", HackerSprayer.Config);
            //HackerSprayer.Store = new RedisDefenceStore("10.187.146.206:7001,10.187.146.206:7002,10.187.146.206:7003,10.187.146.207:7001,10.187.146.207:7002,10.187.146.207:7003", "HttpDefenceTest-", HackerSprayer.Config);
        }

        private AccountController GetAccountController()
        {
            var controller = new AccountController();
            controller.SetMockControllerContext();
            var request = Mock.Get(controller.Request);
            request.Setup(r => r.UserHostAddress).Returns(IPAddress.Loopback.ToString());
            return controller;
        }



        [TestMethod]
        public void TestValidLogin()
        {
            var controller = GetAccountController();

            var result = controller.LogOn("user1", "user1").Run();
            AssertLoginSuccess(result, "Valid login failed");            
        }

        private static void AssertLoginSuccess(ActionResult result, string message = "")
        {
            Assert.IsInstanceOfType(result, typeof(RedirectToRouteResult), message);
            var redirectResult = result as RedirectToRouteResult;
            Assert.AreEqual("Index", redirectResult.RouteValues["action"], message);
        }

        [TestMethod]
        public void TestValidLoginBruteForce()
        {
            AccountController.MaxValidLogin = 10;
            AccountController.MaxValidLoginInterval = TimeSpan.FromMinutes(1);

            var controller = GetAccountController();

            var startTime = DateTime.Now;
            Parallel.For(0, AccountController.MaxValidLogin,
                performValidLogin =>
                {
                    var successResult = controller.LogOn("user2", "user2").Run();
                    AssertLoginSuccess(successResult, "Allow login upto Max Valid Login");
                });            

            // more than allowed hit must be blocked
            var invalidResult = controller.LogOn("user2", "user2").Run();
            AssertBlockedLogin(invalidResult, "Block login attempt after max login attempt");

            // wait for expiration time
            WaitForExpirationTime(AccountController.MaxValidLoginInterval, startTime);

            var result = controller.LogOn("user2", "user2").Run();
            AssertLoginSuccess(result, "Allow login after the expiration time");
        }

        private static void WaitForExpirationTime(TimeSpan interval, DateTime startTime)
        {
            Thread.Sleep(interval - (DateTime.Now - startTime));
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }

        [TestMethod]
        public void TestInvalidLoginBruteForce()
        {
            AccountController.MaxInvalidLogin = 3;
            AccountController.MaxInvalidLoginInterval = TimeSpan.FromMinutes(1);

            var controller = GetAccountController();

            var startTime = DateTime.Now;
            Parallel.For(0, AccountController.MaxInvalidLogin,
                performInvalidLogin =>
                {
                    var successResult = controller.LogOn("user2", "invalidpassword").Run();
                    AssertInvalidLoginSuccess(successResult, "Allow invalid login upto Max Invalid Login");
                });

            // more than allowed hit must be blocked
            var invalidResult = controller.LogOn("user2", "invalidpassword").Run();
            AssertBlockedLogin(invalidResult, "Block login attempt after max login attempt");

            WaitForExpirationTime(AccountController.MaxInvalidLoginInterval, startTime);

            var result = controller.LogOn("user2", "invalidpassword").Run();
            AssertInvalidLoginSuccess(result, "Allow invalid login after the expiration time");
        }

        private void AssertInvalidLoginSuccess(ActionResult successResult, string message)
        {
            Assert.IsInstanceOfType(successResult, typeof(ViewResult), message);
            var viewResult = successResult as ViewResult;
            Assert.AreEqual("~/Views/Account/LogOn.cshtml", viewResult.ViewName, message);
            Assert.IsNotNull(viewResult.TempData[TempDataConstants.ERROR_MESSAGE]);
        }

        private static void AssertBlockedLogin(ActionResult invalidResult, string message = "")
        {
            Assert.IsInstanceOfType(invalidResult, typeof(HttpStatusCodeResult), message);
            var statusResult = invalidResult as HttpStatusCodeResult;
            Assert.AreEqual(statusResult.StatusCode, (int)HttpStatusCode.Forbidden, message);
        }
    }

    public static class Extensions
    {
        public static TResult Run<TResult>(this Task<TResult> t)
        {
            return t.GetAwaiter().GetResult();
        }

    }
}
