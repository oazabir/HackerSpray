# Hacker Spray
A .NET library to defend websites against brute force attacks and malicious attempts. 

# Getting Started
## Nuget
Get the Hacker Spray library and HTTP Module to defend your website using:

    Install-Package HackerSpray.WebModule

## Source code
Download the source code. The HackerSpray.Module contains the library to implement your own defence. 

HackerSpray.WebModule contains HackerSprayModule, which you can use to defend certain URL against brute force attack.

## Using Hacker Spray
### Step 1
In the web.config you need to specify the following:

```xml
<add key="RedisConnection" value="localhost" />
<add key="RedisPrefix" value="AuthTest:" />
<add key="PathToDefend" value="/Account/LogOn" />
<add key="HackerSprayEnabled" value="true" />
```
 - RedisConnection - This is the connection string to Redis server.
 - RedisPrefix - All keys created in redis is prefixed with this.
 - PathToDefend - If you are using the HttpModule, specify the Path which it will defend against brute force
 - HackerSprayEnabled - A Master switch to turn on/off

### Step 2
In Global.asax, you need to enable the HackerSpray functionality:

```c#
if (Convert.ToBoolean(ConfigurationManager.AppSettings["HackerSprayEnabled"]))
{
    HackerSprayer.Store = new RedisDefenceStore(ConfigurationManager.AppSettings["RedisConnection"], ConfigurationManager.AppSettings["RedisPrefix"], HackerSprayer.Config);
    HackerSprayModule.PathToDefend = ConfigurationManager.AppSettings["PathToDefend"] ?? "/";
}
```