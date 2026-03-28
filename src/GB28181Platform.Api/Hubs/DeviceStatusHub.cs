using Microsoft.AspNetCore.SignalR;

namespace GB28181Platform.Api.Hubs;

public class DeviceStatusHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}
