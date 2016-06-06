using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;

namespace HackerSpray.Middleware.Test
{
    public class TestClientIPFeature : IHttpConnectionFeature
    {
        public string ConnectionId { get; set; } = Guid.NewGuid().ToString();

        public IPAddress LocalIpAddress { get; set; } = IPAddress.Loopback;

        public int LocalPort { get; set; } = 80;

        public IPAddress RemoteIpAddress { get; set; } = IPAddress.Loopback;

        public int RemotePort { get; set; } = 80;
    }
}
