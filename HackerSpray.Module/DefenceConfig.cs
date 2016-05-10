using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackerSpray.Module
{
    public class DefenceConfig
    {
        public int MaxHitsPerKey { get ; set; }
        public int MaxHitsPerOrigin { get; set; }
        public int MaxHitsPerKeyPerOrigin { get; set; }

        public TimeSpan MaxHitsPerKeyInterval { get; set; }
        public TimeSpan MaxHitsPerOriginInterval { get; set; }
        public TimeSpan MaxHitsPerKeyPerOriginInterval { get; set; }

        public TimeSpan KeyBlacklistInterval { get; set; }
        public TimeSpan OriginBlacklistInterval { get; set; }
    }
}
