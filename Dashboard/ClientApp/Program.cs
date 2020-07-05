using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Open.TaskManager.Client;
using Solve.Dashboard.Client;

namespace Solve.Dashboard.ClientApp
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = WebAssemblyHostBuilder.CreateDefault(args);
			ConfigureServices(builder.Services, builder.HostEnvironment);
			builder.RootComponents.Add<App>("app");

			var host = builder.Build();
			host.Services
				.UseBootstrapProviders()
				.UseFontAwesomeIcons();

			await host.RunAsync();
		}

		static void ConfigureServices(IServiceCollection services, IWebAssemblyHostEnvironment env)
		{
			services
				.AddLogging()
				.AddHttpClient()
				.AddBlazorise(options =>
				{
					options.ChangeTextOnKeyPress = true;
				})
				.AddBootstrapProviders()
				.AddFontAwesomeIcons();

			var hubAddress = env.BaseAddress + Constants.SolveRunnerHubPath;
			Debug.WriteLine("Hub Address: {0}", hubAddress);
			services
				.ConfigureHubConnection<SolveRunnerRegistryProxy>(env.BaseAddress + Constants.SolveRunnerHubPath);
		}
	}
}
