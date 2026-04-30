using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace WebVella.Erp.Site
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        // QA Issue 1 (CRITICAL) — Production-mode static-asset serving fix (Layer 3 of 3).
        //
        // Layer 1: WebApiController.cs catch-all DELETE route was scoped from "{*filepath}" to
        //          "fs/{*filepath}" so it no longer claims the entire URL namespace and hides
        //          static-asset endpoints from the routing layer.
        // Layer 2: app.UseEndpoints { endpoints.MapStaticAssets(); } was added to Startup.cs so
        //          the .NET 9 static-web-assets manifest endpoints are registered ahead of
        //          MapRazorPages / MapControllerRoute.
        // Layer 3 (this call): UseStaticWebAssets() configures the IWebHostBuilder's
        //          WebRootFileProvider to materialize file streams using the
        //          {AssemblyName}.staticwebassets.runtime.json manifest emitted by the build.
        //
        // Why this is required:
        //   .NET 9's CreateDefaultBuilder invokes UseStaticWebAssets() automatically *only* when
        //   the hosting environment is "Development". When the host is started from
        //   bin/Release/net9.0 with ASPNETCORE_ENVIRONMENT=Production, MapStaticAssets() registers
        //   the route endpoints from the static-web-assets manifest, but the StaticAssetsInvoker
        //   cannot resolve the underlying file streams without the WebRootFileProvider being
        //   wired up to read from the manifest. The result is HTTP 200 with Content-Length: 0
        //   on every /_content/* request, with the diagnostic warning
        //   "StaticAssetsInvoker[17]: The application is not running against the published
        //    output and Static Web Assets are not enabled."
        //
        // Behavior preservation per AAP §0.11.1.2:
        //   - This call is a no-op when the manifest is not present (i.e. it gracefully does
        //     nothing when running from a fully published `dotnet publish` output where the
        //     wwwroot directory is co-located).
        //   - It does NOT alter any public API contract, REST endpoint signature, Razor Page
        //     route, or observable response shape.
        //   - It is the canonical Microsoft-recommended pattern for enabling static-web-assets
        //     outside Development, documented at
        //     https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files
        //
        // Same fix is applied to all 7 Site Program.cs files for cross-host consistency.
        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStaticWebAssets()
                .UseStartup<Startup>()
                .Build();
    }
}
