using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebVella.Erp.Site.MicrosoftCDM
{
	public class Program
	{
		public static void Main(string[] args)
		{
			BuildWebHost(args).Run();
		}

		// QA Issue 1 (CRITICAL) fix — see WebVella.Erp.Site/Program.cs for full rationale.
		// UseStaticWebAssets() is required so that MapStaticAssets() in Startup.cs can
		// materialize file streams in non-Development environments (Production / Staging)
		// when the host is started from bin/Release/net9.0 rather than `dotnet publish` output.
		public static IWebHost BuildWebHost(string[] args) =>
		   WebHost.CreateDefaultBuilder(args)
			   .UseStaticWebAssets()
			   .UseStartup<Startup>()
			   .Build();
	}
}
