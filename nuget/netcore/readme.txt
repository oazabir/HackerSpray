# Getting started with HackerSpray

## Step 1: Add hackerspray.json in the configuration

On your Startup.cs:

var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
Add this line--> .AddJsonFile("hackerspray.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);


## Step 2: Add the Hacker Spray service in Startup.cs

public void ConfigureServices(IServiceCollection services)
{
    .
    .
    .
    .
    services.AddHackerSpray(Configuration.GetSection("HackerSpray"));
}


## Step 3: Add HackerSpray middleware in Startup.service

Add app.UseXForwardedFor(); and app.UseHackerSpray(); right after UseStaticFiles() and before UseMvc();

public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
{
    .
    .
    .

    app.UseStaticFiles();
    
    app.UseXForwardedFor();
    app.UseHackerSpray();

    .
    .
    .
    
    app.UseMvc(routes =>
    {
        routes.MapRoute(
            name: "default",
            template: "{controller=Home}/{action=Index}/{id?}");
    });
    
}

## Step 4: Configure hackerspray.json

{
  "HackerSpray": {
    "Redis": "127.0.0.1",
    "Prefix": "AuthTest-Core:",
    "Keys": [
      "POST /Account/Login 100 00:10:00 key+origin",
      "GET /Account/Login 100 00:10:00 key+origin",
      "GET /Home/ 10000 00:10:00 key+origin"
    ]
  }
}