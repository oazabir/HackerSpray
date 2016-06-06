using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace HackerSpray.Middleware
{
    public static class HackerSprayMiddlewareExtensions
    {
        public static IApplicationBuilder UseHackerSpray(this IApplicationBuilder builder)
        {            
            return builder.UseMiddleware<HackerSprayerMiddleware>();
        }

        public static HackerSprayOption GetHackerSprayConfiguration(this IConfiguration options)
        {
            var option = options.GetSection("HackerSpray");
            IEnumerable<string> keys = option.GetSection("Keys").GetChildren().Select(x => x.Value);
            return new HackerSprayOption
            {
                Keys = keys.ToList(),
                Prefix = option["Prefix"],
                Redis = option["Redis"]
            };            
        }
    }
}
