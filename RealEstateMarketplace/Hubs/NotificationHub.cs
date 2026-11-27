using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RealEstateMarketplace.Hubs
{
    /// <summary>Live in-app notifications (server pushes to Clients.User).</summary>
    [Authorize]
    public class NotificationHub : Hub
    {
    }
}
