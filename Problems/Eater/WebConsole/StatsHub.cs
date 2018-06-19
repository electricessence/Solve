using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Eater.WebConsole
{
	public class StatsHub : Hub
	{
		public async Task SendMessage(string user, string message)
		{
			await Clients.All.SendAsync("ReceiveMessage", user, message);
		}
	}
}
