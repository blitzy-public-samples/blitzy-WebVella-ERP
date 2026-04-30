using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebVella.Erp.Web;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Site.Pages
{
	// QA Issue 14 (MINOR visual) and Issue 20 (MINOR functional) fix companion:
	// ErrorModel now derives from BaseErpPageModel (matching the LoginModel
	// pattern) so it can be rendered via the platform's `_SystemMaster.cshtml`
	// layout. The _SystemMaster layout's render-hook view-component requires a
	// BaseErpPageModel-typed @Model; the prior PageModel base produced a runtime
	// RuntimeBinderException when the layout tried to bind. Per AAP §0.11.1.2,
	// the page route ("/error") and the HTTP status code emission semantics are
	// preserved unchanged.
	[AllowAnonymous]
	public class ErrorModel : BaseErpPageModel
	{
		public ErrorModel([FromServices] ErpRequestContext reqCtx)
		{
			ErpRequestContext = reqCtx;
		}

		public IActionResult OnGet()
		{
			// Preserve legacy query-string status overrides for backward compatibility:
			// some upstream callers append ?401 or ?404 to indicate the desired
			// status code rather than relying on the StatusCodePagesWithReExecute
			// pipeline that already sets HttpContext.Response.StatusCode.
			if (HttpContext.Request.Query.ContainsKey("401"))
				Request.HttpContext.Response.StatusCode = 401; //access denied;
			if (HttpContext.Request.Query.ContainsKey("404"))
				Request.HttpContext.Response.StatusCode = 404; //page not found;

			return Page();
		}
	}
}
