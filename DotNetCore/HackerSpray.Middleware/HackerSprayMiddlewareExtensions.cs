using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HackerSpray.Middleware
{
    public static class HackerSprayMiddlewareExtensions
    {
        public static IApplicationBuilder UseHackerSpray(this IApplicationBuilder builder)
        {            
            return builder.UseMiddleware<HackerSprayerMiddleware>();
        }

        public static void AddHackerSpray(this IServiceCollection services, IConfigurationSection section)
        {
            var prefix = section["Prefix"];
            if (string.IsNullOrWhiteSpace(prefix))
                throw new Exception("Invalid prefix configured in section: " + section.Key);

            var redis = section["Redis"];
            if (string.IsNullOrWhiteSpace(redis))
                throw new Exception("Invalid redis connection string configured in section: " + section.Key);

            IEnumerable<string> keys = section.GetSection("Keys").GetChildren().Select(x => x.Value);
            var option = new HackerSprayOption
            {
                Keys = keys.ToList(),
                Prefix = prefix,
                Redis = redis
            };
            services.AddSingleton<HackerSprayOption>(option);
        }
    }
}
