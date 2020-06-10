using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Eater.WebConsole.Hubs
{
	public class GenomeProgressionHub : Hub
	{
		public Task SendMessage(string user, string message)
		{
			return Clients.All.SendAsync("ReceiveMessage", user, message);
		}
	}
}
