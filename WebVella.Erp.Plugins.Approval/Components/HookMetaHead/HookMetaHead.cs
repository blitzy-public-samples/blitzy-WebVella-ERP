using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebVella.Erp.Web.Hooks;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Plugins.Approval.Components
{
    /// <summary>
    /// ASP.NET Core ViewComponent for injecting the Approval plugin's CSS stylesheets into page heads.
    /// This component is registered with WebVella's render hook system at the "head-top" location
    /// with priority 10, enabling modular CSS injection without modifying shared layouts.
    /// </summary>
    /// <remarks>
    /// The component loads embedded CSS resources from the WebVella.Erp.Plugins.Approval.Theme
    /// namespace and injects them as inline styles or link tags into the page head section.
    /// This follows WebVella's plugin architecture pattern for decoupled resource management.
    /// </remarks>
    [RenderHookAttachment("head-top", 10)]
    public class HookMetaHead : ViewComponent
    {
        /// <summary>
        /// Asynchronously invokes the ViewComponent to inject CSS resources into the page head.
        /// </summary>
        /// <param name="pageModel">The base ERP page model providing context for the current page request.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// an <see cref="IViewComponentResult"/> that renders the Approval_HookMetaHead view
        /// with the CSS link tags configured in ViewBag.LinkTags.
        /// </returns>
        /// <remarks>
        /// This method:
        /// 1. Initializes the ViewBag.LinkTags collection for CSS includes
        /// 2. Loads the embedded styles.css resource from the Approval plugin's Theme namespace
        /// 3. Creates LinkTagInclude objects with the loaded CSS content
        /// 4. Returns the Approval_HookMetaHead view for rendering
        /// </remarks>
        public async Task<IViewComponentResult> InvokeAsync(BaseErpPageModel pageModel)
        {
            ViewBag.LinkTags = new List<LinkTagInclude>();

            #region === <style> ===
            {
                var linkTagsToInclude = new List<LinkTagInclude>();

                //Your includes below >>>>

                #region << styles.css >>
                {
                    //Always include the Approval plugin styles
                    linkTagsToInclude.Add(new LinkTagInclude()
                    {
                        InlineContent = FileService.GetEmbeddedTextResource("styles.css", "WebVella.Erp.Plugins.Approval.Theme", "WebVella.Erp.Plugins.Approval")
                    });
                }
                #endregion

                //<<<< Your includes up
                ViewBag.LinkTags = linkTagsToInclude;
            }
            #endregion

            return await Task.FromResult<IViewComponentResult>(View("Approval_HookMetaHead"));
        }
    }
}
