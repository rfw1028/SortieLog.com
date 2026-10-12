using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace FlightLogs.Hubs
{
    public class ProgressHub : Hub
    {
        public static ConcurrentDictionary<string, string> Connections = new();

        public override Task OnConnectedAsync()
        {
            string userId = Context.User.Identity.Name;
            Connections[userId] = Context.ConnectionId;
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Connections.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
