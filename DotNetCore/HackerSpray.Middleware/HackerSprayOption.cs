using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HackerSpray.Middleware
{
    public class HackerSprayOption
    {
        public string Redis { get; set; } = "127.0.0.1";
        public string Prefix { get; set; } = "AuthTestCore:";

        public List<string> Keys { get; set; }
    }
}
