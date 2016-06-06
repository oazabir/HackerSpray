using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HackerSpray.Middleware
{
    public class HackerSprayOptionKey
    {
        public enum HitCountMode
        {
            PerKey,
            PerOrigin,
            PerKeyOrigin
        }
        public string Key { get; set; }
        public string Method { get; set; }
        public long MaxAttempts { get; set; }
        public TimeSpan Interval { get; set; }
        public HitCountMode Mode{ get; set; }
    }
}
