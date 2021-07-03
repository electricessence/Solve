﻿using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Solve.Dashboard.Server.Pages
{
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class ErrorModel : PageModel
	{
		public string? RequestId { get; set; }

		public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

#pragma warning disable IDE0052 // Remove unread private members
		private readonly ILogger<ErrorModel> _logger;
#pragma warning restore IDE0052 // Remove unread private members

		public ErrorModel(ILogger<ErrorModel> logger)
		{
			_logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
		}

		public void OnGet()
		{
			RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
		}
	}
}