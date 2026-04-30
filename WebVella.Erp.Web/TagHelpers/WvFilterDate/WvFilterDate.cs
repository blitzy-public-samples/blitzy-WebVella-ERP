using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Services;

namespace WebVella.Erp.Web.TagHelpers
{
	[HtmlTargetElement("wv-filter-date")]
	public class WvFilterDate : WvFilterBase
	{
		public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
		{
			if (!isVisible)
			{
				output.SuppressOutput();
				return Task.CompletedTask;
			}
			#region << Init >>
			var initSuccess = InitFilter(context, output);

			if (!initSuccess)
			{
				return Task.CompletedTask;
			}
			#endregion

			var inputGroupEl = new TagBuilder("div");
			inputGroupEl.AddCssClass("input-group");

			inputGroupEl.InnerHtml.AppendHtml(FilterTypeSelect);

			#region << valueDateControl >>
			{
				var valueDateControl = new TagBuilder("input");
				valueDateControl.AddCssClass("form-control value");
				if (QueryType != FilterType.BETWEEN && QueryType != FilterType.NOTBETWEEN)
				{
					valueDateControl.AddCssClass("rounded-right");
				}
				valueDateControl.Attributes.Add("value", (Value != null ? Value.ToString("yyyy-MM-dd") : ""));
				valueDateControl.Attributes.Add("type", "date");
				valueDateControl.Attributes.Add("name", UrlQueryOfValue);
				// QA Issue 7 (MAJOR a11y) fix: id matches the label `for=` rendered
				// by WvFilterBase so screen readers announce the label and
				// click-on-label focuses this date input.
				valueDateControl.Attributes.Add("id", $"erp-filter-input-{FilterId}");
				inputGroupEl.InnerHtml.AppendHtml(valueDateControl);
			}
			#endregion


			inputGroupEl.InnerHtml.AppendHtml(AndDivider);

			#region << value2DateControl >>
			{
				var value2DateControl = new TagBuilder("input");
				value2DateControl.Attributes.Add("value", (Value2 != null ? Value2.ToString("yyyy-MM-dd") : ""));
				value2DateControl.AddCssClass("form-control value2");
				value2DateControl.Attributes.Add("type", "date");
				if (QueryType == FilterType.BETWEEN || QueryType == FilterType.NOTBETWEEN)
				{
					value2DateControl.Attributes.Add("name", UrlQueryOfValue2);
				}
				else
				{
					value2DateControl.AddCssClass("d-none");
				}
				// QA Issue 7 (MAJOR a11y) fix: secondary date input gets a derived
				// id (no shared id collisions) and an aria-label describing it as
				// the upper bound of a BETWEEN/NOTBETWEEN range.
				value2DateControl.Attributes.Add("id", $"erp-filter-input2-{FilterId}");
				value2DateControl.Attributes.Add("aria-label", $"{(string.IsNullOrWhiteSpace(Label) ? Name : Label)} (upper bound)");
				inputGroupEl.InnerHtml.AppendHtml(value2DateControl);
			}
			#endregion


			output.Content.AppendHtml(inputGroupEl);

			return Task.CompletedTask;
		}


	}
}
