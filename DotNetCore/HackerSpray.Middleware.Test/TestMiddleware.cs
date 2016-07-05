using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Net;
using Microsoft.AspNetCore.Http.Features;
using HackerSpray.Module;
using System.Threading;
using Serilog;
using System.IO;
using Serilog.Sinks.RollingFile;

namespace HackerSpray.Middleware.Test
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    [TestClass]
    public class TestMiddleware
    {
        private TestServer _server;
        [TestInitialize]
        public void Init()
        {
            _server = GetServer();            
        }

        [TestCleanup]
        public void Cleanup()
        {
            Hacker.ClearAllHitsAsync();
            Hacker.ClearBlacklistsAsync();
            if (_server != null)
                _server.Dispose();
        }

        private TestServer GetServer()
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            builder.AddEnvironmentVariables();
            var config = builder.Build();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.RollingFile("C:\\inetpub\\logs\\LogFiles\\log-{Date}.txt")
                .CreateLogger();

            var bldr = new WebHostBuilder()
               .Configure(app =>
               {
                   var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
                   loggerFactory.AddConsole(config.GetSection("Logging"));
                   loggerFactory.AddDebug(LogLevel.Debug);
                   loggerFactory.AddSerilog();

                   app.UseXForwardedFor();
                   app.UseHackerSpray();

                   app.Run((async (context) =>
                   {                       
                       if (context.Request.Path == "/Account/Login")
                       {
                           context.Response.StatusCode = (int)HttpStatusCode.OK;
                           await context.Response.WriteAsync("Hello World!");
                       }
                   }));
               })
               .ConfigureServices(svcs =>
               {
                   svcs.AddLogging();
                   svcs.AddHackerSpray(config.GetSection("HackerSpray"));
               });               
            return new TestServer(bldr);
        }
        
        [TestMethod]
        public async Task TestAccountLogOn()
        {
            await Hacker.ClearAllHitsAsync();
            await Hacker.ClearBlacklistsAsync();

            var response = await _server.CreateClient().PostAsync("/Account/Login",
                new StringContent("Email=user1@user.com&Password=Password1!"));
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.AreEqual("Hello World!",
                responseString);
        }

        [TestMethod]
        public async Task TestBruteForceOnAccountLogOn()
        {
            await Hacker.ClearAllHitsAsync();
            await Hacker.ClearBlacklistsAsync();

            var startTime = DateTime.Now;
            // Perform brute force login 
            var forResult = Parallel.For(0, 99, async p =>
            {
                string responseString = await HitLoginPage(HttpStatusCode.OK,
                    "Hit must be allowed from IP until limit is reached");

                // Assert
                Assert.AreEqual("Hello World!", responseString);
            });

            forResult.Wait();

            await HitLoginPage(HttpStatusCode.OK, "Hit must be allowed from IP until limit is reached");

            // Ensure any extra hit is now blocked
            await HitLoginPage(HttpStatusCode.NotAcceptable, "Hit should be blocked after maximum number of hits made.");

            // Hits from different IP must be allowed
            var diffClient = _server.CreateClient();
            diffClient.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.2");

            var allowedResponse = await diffClient.PostAsync("/Account/Login", new StringContent("body"));
            await allowedResponse.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.OK, allowedResponse.StatusCode,
                "Hit must be allowed from other IP");


            // wait for unblock period
            WaitForIntervalToElapse(TimeSpan.FromMinutes(1), startTime);

            await HitLoginPage(HttpStatusCode.OK, "Hit must be allowed from IP until limit is reached");

        }

        private async Task<string> HitLoginPage(HttpStatusCode expected, string msg)
        {
            var response = await _server.CreateClient().PostAsync("/Account/Login", new StringContent("body"));
            Assert.AreEqual(expected, response.StatusCode, msg);
            var responseString = await response.Content.ReadAsStringAsync();
            return responseString;
        }

        private static void WaitForIntervalToElapse(TimeSpan interval, DateTime startTime)
        {
            Thread.Sleep(interval - (DateTime.Now - startTime));
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }

    }

    public static class TestExtensions
    {
        public static void Wait(this ParallelLoopResult result)
        {
            while (!result.IsCompleted) Thread.Sleep(100);
        }
    }
}
