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
            if (_server != null)
                _server.Dispose();
        }

        private TestServer GetServer()
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            builder.AddEnvironmentVariables();
            var config = builder.Build();
            var bldr = new WebHostBuilder()
               .Configure(app =>
               {
                   var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
                   loggerFactory.AddConsole(config.GetSection("Logging"));
                   loggerFactory.AddDebug();

                   app.UseXForwardFor();
                   app.UseHackerSpray();

                   app.Run((async (context) =>
                   {                       
                       if (context.Request.Path == "/Account/LogOn")
                       {
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
            var response = await _server.CreateClient().PostAsync("/Account/LogOn",
                new StringContent("Email=user1@user.com&Password=Password1!"));
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.AreEqual("Hello World!",
                responseString);
        }
    }
}
