using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PropertyTransferManagement.Hubs
{
    /// <summary>
    /// Live in-app notifications. The server pushes to Clients.User(id);
    /// clients only listen.
    /// </summary>
    [Authorize]
    public class NotificationHub : Hub
    {
    }
}
