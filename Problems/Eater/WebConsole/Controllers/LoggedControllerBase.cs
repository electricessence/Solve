using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Eater.WebConsole.Controllers
{
	[ApiController]
	[Route("[controller]")]
	public abstract class LoggedControllerBase
	{
		protected ILogger Logger { get; }

		protected LoggedControllerBase(ILogger logger)
		{
			Logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
		}
	}
}
