using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Chronos.MainApi.Schedule.Messaging;

[Authorize]
public class SchedulingNotificationsHub : Hub
{
}
