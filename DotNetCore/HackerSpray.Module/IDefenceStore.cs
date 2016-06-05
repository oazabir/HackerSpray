using System;
using System.Net;
using System.Threading.Tasks;

namespace HackerSpray.Module
{
    public interface IDefenceStore : IDisposable
    {
        Task<HitStats> IncrementHit(string key, IPAddress origin);
        Task<HitStats> IncrementHit(string key, IPAddress origin, TimeSpan keyInterval, TimeSpan originInterval, TimeSpan keyOriginInterval);
        Task<long> GetHitsForKey(string key);
        Task<long> GetHitsFromOrigin(IPAddress origin);
        Task<bool> WhitelistKey(string key);
        Task<bool> BlacklistKey(string key, TimeSpan expiry);
        Task<string[]> GetKeyBlacklists();
        Task<string[]> GetOriginBlacklists(IPAddress origin);
        Task<bool> WhitelistOrigin(IPAddress origin);
        Task<bool> WhitelistOrigin(IPAddress start, IPAddress end);
        Task<bool> BlacklistOrigin(IPAddress origin, TimeSpan expiry);
        Task<bool> BlacklistOrigin(IPAddress start, IPAddress end);
        Task<bool> IsKeyBlacklisted(string key);
        Task<bool> IsOriginBlacklisted(IPAddress origin);

        Task<bool> ClearBlacklists();
        Task<bool> ClearAllHits();
    }
}
