using System;
using WebVella.Erp.Api.Models;

namespace WebVella.Erp.Plugins.Approval.Model
{
	/// <summary>
	/// Defines the comparison operators that can be used in approval rules for field-based conditions.
	/// These operators are used by the approval_rule entity to define conditional routing logic
	/// based on record field values during workflow evaluation.
	/// </summary>
	public enum RuleOperator
	{
		/// <summary>
		/// Exact match comparison operator. Returns true when the field value exactly matches
		/// the rule value. Supports string, numeric, and date comparisons.
		/// </summary>
		[SelectOption(Label = "equals")]
		Equals = 0,

		/// <summary>
		/// Inequality comparison operator. Returns true when the field value does not match
		/// the rule value. Supports string, numeric, and date comparisons.
		/// </summary>
		[SelectOption(Label = "not equals")]
		NotEquals = 1,

		/// <summary>
		/// Greater than comparison operator. Returns true when the field value is greater than
		/// the rule value. Primarily used for numeric and date field comparisons.
		/// </summary>
		[SelectOption(Label = "greater than")]
		GreaterThan = 2,

		/// <summary>
		/// Less than comparison operator. Returns true when the field value is less than
		/// the rule value. Primarily used for numeric and date field comparisons.
		/// </summary>
		[SelectOption(Label = "less than")]
		LessThan = 3,

		/// <summary>
		/// Substring match comparison operator. Returns true when the field value contains
		/// the rule value as a substring. Used for text field partial matching.
		/// </summary>
		[SelectOption(Label = "contains")]
		Contains = 4,

		/// <summary>
		/// Prefix match comparison operator. Returns true when the field value starts with
		/// the rule value. Used for text field prefix matching scenarios.
		/// </summary>
		[SelectOption(Label = "starts with")]
		StartsWith = 5,
	}
}
