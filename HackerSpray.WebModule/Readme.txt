HackerSpray HttpModule for .NET 4.5.2

In order to register the HttpModule, add these in your Global.asax Application_Start:

if (Convert.ToBoolean(ConfigurationManager.AppSettings["HackerSprayEnabled"]))
{
    Hacker.Store = new RedisDefenceStore(ConfigurationManager.AppSettings["RedisConnection"], ConfigurationManager.AppSettings["RedisPrefix"], Hacker.Config);
    HackerSprayModule.PathToDefend = ConfigurationManager.AppSettings["PathToDefend"] ?? "/";
}

This expects you have the following keys in web.config:

<appSettings>    
    <add key="RedisConnection" value="localhost" />
    <add key="RedisPrefix" value="AuthTest:" />
    <add key="PathToDefend" value="/Account/LogOn" />
    <add key="HackerSprayEnabled" value="true" />
</appSettings>


RedisConnection: This is the connection string for Redis server. 
RedisPrefix: All keys will be prefixed with this.
PathToDefend: Which URL the HttpModule will defend against attack.
HackreSprayEnabled: Whether to enable or disable HackerSpray defence.