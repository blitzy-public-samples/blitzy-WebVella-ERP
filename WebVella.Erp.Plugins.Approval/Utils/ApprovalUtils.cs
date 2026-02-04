using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Plugins.Approval.Model;

namespace WebVella.Erp.Plugins.Approval.Utils
{
    /// <summary>
    /// Public static utility class providing helper methods for approval processing across the plugin.
    /// Contains status validation helpers, SLA time calculation methods, rule evaluation helpers,
    /// record utility methods, and common string/formatting utilities used by services and components.
    /// All methods are static and thread-safe.
    /// </summary>
    public static class ApprovalUtils
    {
        #region Status Validation Helpers

        /// <summary>
        /// Validates whether a status transition from the current status to the new status is allowed.
        /// Enforces the approval workflow state machine rules.
        /// </summary>
        /// <param name="currentStatus">The current status of the approval request.</param>
        /// <param name="newStatus">The proposed new status for the approval request.</param>
        /// <returns>True if the transition is valid; otherwise, false.</returns>
        /// <remarks>
        /// Valid transitions:
        /// - Pending -> Approved, Rejected, Delegated, Escalated, Cancelled
        /// - Delegated -> Approved, Rejected, Escalated
        /// - Escalated -> Approved, Rejected
        /// - Approved, Rejected, Cancelled -> No transitions (terminal states)
        /// </remarks>
        public static bool IsValidStatusTransition(ApprovalStatus currentStatus, ApprovalStatus newStatus)
        {
            // Same status is not a valid transition
            if (currentStatus == newStatus)
            {
                return false;
            }

            // Terminal statuses cannot transition to any other status
            if (IsTerminalStatus(currentStatus))
            {
                return false;
            }

            // Define valid transitions based on current status
            switch (currentStatus)
            {
                case ApprovalStatus.Pending:
                    // Pending can transition to: Approved, Rejected, Delegated, Escalated, Cancelled
                    return newStatus == ApprovalStatus.Approved ||
                           newStatus == ApprovalStatus.Rejected ||
                           newStatus == ApprovalStatus.Delegated ||
                           newStatus == ApprovalStatus.Escalated ||
                           newStatus == ApprovalStatus.Cancelled;

                case ApprovalStatus.Delegated:
                    // Delegated can transition to: Approved, Rejected, Escalated
                    return newStatus == ApprovalStatus.Approved ||
                           newStatus == ApprovalStatus.Rejected ||
                           newStatus == ApprovalStatus.Escalated;

                case ApprovalStatus.Escalated:
                    // Escalated can only transition to: Approved, Rejected
                    return newStatus == ApprovalStatus.Approved ||
                           newStatus == ApprovalStatus.Rejected;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines whether the specified status is a terminal (final) status.
        /// Terminal statuses indicate the end of the approval process.
        /// </summary>
        /// <param name="status">The status to check.</param>
        /// <returns>True if the status is terminal (Approved, Rejected, or Cancelled); otherwise, false.</returns>
        public static bool IsTerminalStatus(ApprovalStatus status)
        {
            return status == ApprovalStatus.Approved ||
                   status == ApprovalStatus.Rejected ||
                   status == ApprovalStatus.Cancelled;
        }

        /// <summary>
        /// Gets a human-readable label for the specified approval status.
        /// </summary>
        /// <param name="status">The approval status to get a label for.</param>
        /// <returns>A human-readable string representation of the status.</returns>
        public static string GetStatusLabel(ApprovalStatus status)
        {
            switch (status)
            {
                case ApprovalStatus.Pending:
                    return "Pending";
                case ApprovalStatus.Approved:
                    return "Approved";
                case ApprovalStatus.Rejected:
                    return "Rejected";
                case ApprovalStatus.Delegated:
                    return "Delegated";
                case ApprovalStatus.Escalated:
                    return "Escalated";
                case ApprovalStatus.Cancelled:
                    return "Cancelled";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// Gets the CSS color class for the specified approval status for UI display.
        /// </summary>
        /// <param name="status">The approval status to get a color class for.</param>
        /// <returns>A CSS color class string suitable for styling status indicators.</returns>
        public static string GetStatusColor(ApprovalStatus status)
        {
            switch (status)
            {
                case ApprovalStatus.Pending:
                    return "warning";
                case ApprovalStatus.Approved:
                    return "success";
                case ApprovalStatus.Rejected:
                    return "danger";
                case ApprovalStatus.Delegated:
                    return "info";
                case ApprovalStatus.Escalated:
                    return "warning";
                case ApprovalStatus.Cancelled:
                    return "secondary";
                default:
                    return "secondary";
            }
        }

        #endregion

        #region SLA Time Calculation Helpers

        /// <summary>
        /// Calculates the due date based on the creation date and SLA hours.
        /// </summary>
        /// <param name="createdOn">The date and time when the approval request was created (UTC).</param>
        /// <param name="slaHours">The number of hours allowed for the approval process.</param>
        /// <returns>The calculated due date (UTC).</returns>
        public static DateTime CalculateDueDate(DateTime createdOn, int slaHours)
        {
            if (slaHours <= 0)
            {
                return createdOn;
            }

            return createdOn.AddHours(slaHours);
        }

        /// <summary>
        /// Determines whether the specified due date has passed.
        /// </summary>
        /// <param name="dueDate">The due date to check (UTC).</param>
        /// <returns>True if the current UTC time is past the due date; otherwise, false.</returns>
        public static bool IsOverdue(DateTime dueDate)
        {
            return DateTime.UtcNow > dueDate;
        }

        /// <summary>
        /// Calculates the time remaining until or elapsed since the due date.
        /// </summary>
        /// <param name="dueDate">The due date to calculate time to (UTC).</param>
        /// <returns>
        /// A TimeSpan representing the time remaining (positive) or time overdue (negative).
        /// </returns>
        public static TimeSpan CalculateTimeToSLA(DateTime dueDate)
        {
            return dueDate - DateTime.UtcNow;
        }

        /// <summary>
        /// Gets a human-readable string describing the time remaining or overdue for an SLA.
        /// </summary>
        /// <param name="dueDate">The due date to calculate the remaining time from (UTC).</param>
        /// <returns>
        /// A human-readable string like "2 hours 30 minutes remaining" or "3 hours overdue".
        /// </returns>
        public static string GetPrettyTimeRemaining(DateTime dueDate)
        {
            TimeSpan timeRemaining = CalculateTimeToSLA(dueDate);
            bool isOverdue = timeRemaining.TotalSeconds < 0;

            // Get absolute value for display
            TimeSpan absoluteTime = isOverdue ? timeRemaining.Negate() : timeRemaining;

            int days = (int)Math.Floor(absoluteTime.TotalDays);
            int hours = absoluteTime.Hours;
            int minutes = absoluteTime.Minutes;

            string timeString;

            if (days > 0)
            {
                if (hours > 0)
                {
                    timeString = $"{days} day{(days != 1 ? "s" : "")} {hours} hour{(hours != 1 ? "s" : "")}";
                }
                else
                {
                    timeString = $"{days} day{(days != 1 ? "s" : "")}";
                }
            }
            else if (hours > 0)
            {
                if (minutes > 0)
                {
                    timeString = $"{hours} hour{(hours != 1 ? "s" : "")} {minutes} minute{(minutes != 1 ? "s" : "")}";
                }
                else
                {
                    timeString = $"{hours} hour{(hours != 1 ? "s" : "")}";
                }
            }
            else if (minutes > 0)
            {
                timeString = $"{minutes} minute{(minutes != 1 ? "s" : "")}";
            }
            else
            {
                // Less than a minute
                int seconds = absoluteTime.Seconds;
                if (isOverdue)
                {
                    timeString = "just now";
                }
                else
                {
                    timeString = seconds > 0 ? $"{seconds} second{(seconds != 1 ? "s" : "")}" : "due now";
                }
            }

            return isOverdue ? $"{timeString} overdue" : $"{timeString} remaining";
        }

        /// <summary>
        /// Calculates the duration in hours between the creation and completion of an approval request.
        /// </summary>
        /// <param name="createdOn">The date and time when the request was created (UTC).</param>
        /// <param name="completedOn">The date and time when the request was completed (UTC).</param>
        /// <returns>The duration in hours as a decimal value with two decimal places precision.</returns>
        public static decimal CalculateApprovalDurationHours(DateTime createdOn, DateTime completedOn)
        {
            if (completedOn < createdOn)
            {
                return 0m;
            }

            TimeSpan duration = completedOn - createdOn;
            return Math.Round((decimal)duration.TotalHours, 2);
        }

        #endregion

        #region Rule Evaluation Helpers

        /// <summary>
        /// Evaluates a single condition by comparing a field value against a rule value using the specified operator.
        /// </summary>
        /// <param name="fieldValue">The actual field value from the record.</param>
        /// <param name="op">The comparison operator to apply.</param>
        /// <param name="ruleValue">The expected value defined in the rule.</param>
        /// <returns>True if the condition is met; otherwise, false.</returns>
        public static bool EvaluateCondition(string fieldValue, RuleOperator op, string ruleValue)
        {
            // Handle null values
            if (fieldValue == null)
            {
                fieldValue = string.Empty;
            }
            if (ruleValue == null)
            {
                ruleValue = string.Empty;
            }

            switch (op)
            {
                case RuleOperator.Equals:
                    return string.Equals(fieldValue, ruleValue, StringComparison.OrdinalIgnoreCase);

                case RuleOperator.NotEquals:
                    return !string.Equals(fieldValue, ruleValue, StringComparison.OrdinalIgnoreCase);

                case RuleOperator.Contains:
                    return fieldValue.IndexOf(ruleValue, StringComparison.OrdinalIgnoreCase) >= 0;

                case RuleOperator.StartsWith:
                    return fieldValue.StartsWith(ruleValue, StringComparison.OrdinalIgnoreCase);

                case RuleOperator.GreaterThan:
                    if (TryParseDecimal(fieldValue, out decimal fieldDecimal1) &&
                        TryParseDecimal(ruleValue, out decimal ruleDecimal1))
                    {
                        return fieldDecimal1 > ruleDecimal1;
                    }
                    // Fall back to string comparison
                    return string.Compare(fieldValue, ruleValue, StringComparison.OrdinalIgnoreCase) > 0;

                case RuleOperator.LessThan:
                    if (TryParseDecimal(fieldValue, out decimal fieldDecimal2) &&
                        TryParseDecimal(ruleValue, out decimal ruleDecimal2))
                    {
                        return fieldDecimal2 < ruleDecimal2;
                    }
                    // Fall back to string comparison
                    return string.Compare(fieldValue, ruleValue, StringComparison.OrdinalIgnoreCase) < 0;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Compares two values using the specified comparison operator.
        /// Supports numeric, date, and string comparisons.
        /// </summary>
        /// <param name="value1">The first value to compare.</param>
        /// <param name="value2">The second value to compare.</param>
        /// <param name="op">The comparison operator to apply.</param>
        /// <returns>True if the comparison condition is met; otherwise, false.</returns>
        public static bool CompareValues(object value1, object value2, RuleOperator op)
        {
            // Convert values to strings for comparison
            string stringValue1 = SafeStringCast(value1) ?? string.Empty;
            string stringValue2 = SafeStringCast(value2) ?? string.Empty;

            // Try numeric comparison first
            if (TryParseDecimal(stringValue1, out decimal decimalValue1) &&
                TryParseDecimal(stringValue2, out decimal decimalValue2))
            {
                switch (op)
                {
                    case RuleOperator.Equals:
                        return decimalValue1 == decimalValue2;
                    case RuleOperator.NotEquals:
                        return decimalValue1 != decimalValue2;
                    case RuleOperator.GreaterThan:
                        return decimalValue1 > decimalValue2;
                    case RuleOperator.LessThan:
                        return decimalValue1 < decimalValue2;
                    case RuleOperator.Contains:
                    case RuleOperator.StartsWith:
                        // These don't make sense for numeric values, use string comparison
                        break;
                }
            }

            // Try DateTime comparison
            if (DateTime.TryParse(stringValue1, out DateTime dateValue1) &&
                DateTime.TryParse(stringValue2, out DateTime dateValue2))
            {
                switch (op)
                {
                    case RuleOperator.Equals:
                        return dateValue1 == dateValue2;
                    case RuleOperator.NotEquals:
                        return dateValue1 != dateValue2;
                    case RuleOperator.GreaterThan:
                        return dateValue1 > dateValue2;
                    case RuleOperator.LessThan:
                        return dateValue1 < dateValue2;
                    case RuleOperator.Contains:
                    case RuleOperator.StartsWith:
                        // These don't make sense for date values, use string comparison
                        break;
                }
            }

            // Fall back to string comparison using EvaluateCondition
            return EvaluateCondition(stringValue1, op, stringValue2);
        }

        /// <summary>
        /// Attempts to parse a string value to a decimal number.
        /// </summary>
        /// <param name="value">The string value to parse.</param>
        /// <param name="result">When this method returns, contains the parsed decimal value if successful; otherwise, 0.</param>
        /// <returns>True if the parsing was successful; otherwise, false.</returns>
        public static bool TryParseDecimal(string value, out decimal result)
        {
            result = 0m;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            // Try parsing with current culture
            if (decimal.TryParse(value, out result))
            {
                return true;
            }

            // Try parsing with invariant culture (handles decimal point)
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            return false;
        }

        #endregion

        #region Record Utility Helpers

        /// <summary>
        /// Safely retrieves a field value from an EntityRecord.
        /// </summary>
        /// <param name="record">The EntityRecord to retrieve the value from.</param>
        /// <param name="fieldName">The name of the field to retrieve.</param>
        /// <returns>The field value if found; otherwise, null.</returns>
        public static object GetFieldValue(EntityRecord record, string fieldName)
        {
            if (record == null || string.IsNullOrWhiteSpace(fieldName))
            {
                return null;
            }

            if (record.Properties.ContainsKey(fieldName))
            {
                return record[fieldName];
            }

            return null;
        }

        /// <summary>
        /// Safely casts an object value to a nullable Guid.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The Guid value if conversion is successful; otherwise, null.</returns>
        public static Guid? SafeGuidCast(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is Guid guidValue)
            {
                return guidValue;
            }

            string stringValue = value.ToString();
            if (Guid.TryParse(stringValue, out Guid result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Safely casts an object value to a nullable DateTime.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The DateTime value if conversion is successful; otherwise, null.</returns>
        public static DateTime? SafeDateTimeCast(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is DateTime dateTimeValue)
            {
                return dateTimeValue;
            }

            string stringValue = value.ToString();
            if (DateTime.TryParse(stringValue, out DateTime result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Safely casts an object value to a nullable int.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The int value if conversion is successful; otherwise, null.</returns>
        public static int? SafeIntCast(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return (int)longValue;
            }

            if (value is short shortValue)
            {
                return shortValue;
            }

            if (value is decimal decimalValue)
            {
                return (int)decimalValue;
            }

            if (value is double doubleValue)
            {
                return (int)doubleValue;
            }

            string stringValue = value.ToString();
            if (int.TryParse(stringValue, out int result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Safely casts an object value to a string.
        /// </summary>
        /// <param name="value">The value to cast.</param>
        /// <returns>The string representation of the value; or null if the value is null.</returns>
        public static string SafeStringCast(object value)
        {
            if (value == null)
            {
                return null;
            }

            return value.ToString();
        }

        #endregion

        #region String/Formatting Helpers

        /// <summary>
        /// Creates a brief summary string for an approval request, suitable for notifications and displays.
        /// </summary>
        /// <param name="request">The approval request EntityRecord to summarize.</param>
        /// <returns>A formatted summary string containing key request information.</returns>
        public static string FormatApprovalSummary(EntityRecord request)
        {
            if (request == null)
            {
                return "No request data available";
            }

            // Extract relevant fields from the request record
            string entityName = SafeStringCast(GetFieldValue(request, "entity_name")) ?? "Unknown Entity";
            Guid? recordId = SafeGuidCast(GetFieldValue(request, "record_id"));
            string status = SafeStringCast(GetFieldValue(request, "status")) ?? "Unknown";
            DateTime? createdOn = SafeDateTimeCast(GetFieldValue(request, "created_on"));

            // Build the summary string
            string recordIdDisplay = recordId.HasValue ? recordId.Value.ToString().Substring(0, 8) + "..." : "Unknown";
            string dateDisplay = createdOn.HasValue ? createdOn.Value.ToString("yyyy-MM-dd HH:mm") : "Unknown date";

            return $"Approval for {entityName} (Record: {recordIdDisplay}) - Status: {status} - Created: {dateDisplay}";
        }

        /// <summary>
        /// Truncates a string to the specified maximum length and appends an ellipsis if truncation occurred.
        /// </summary>
        /// <param name="text">The text to truncate.</param>
        /// <param name="maxLength">The maximum length of the resulting string, including the ellipsis.</param>
        /// <returns>The truncated string with ellipsis if truncation occurred; otherwise, the original string.</returns>
        public static string TruncateWithEllipsis(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            if (maxLength <= 0)
            {
                return string.Empty;
            }

            if (maxLength <= 3)
            {
                return text.Length > maxLength ? text.Substring(0, maxLength) : text;
            }

            if (text.Length <= maxLength)
            {
                return text;
            }

            // Truncate and add ellipsis
            return text.Substring(0, maxLength - 3) + "...";
        }

        /// <summary>
        /// Constructs a URL to an approval request detail page.
        /// </summary>
        /// <param name="baseUrl">The base URL of the application (e.g., "https://example.com").</param>
        /// <param name="requestId">The unique identifier of the approval request.</param>
        /// <returns>The fully constructed URL to the approval request detail page.</returns>
        public static string BuildApprovalUrl(string baseUrl, Guid requestId)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return $"/approval/requests/{requestId}";
            }

            // Ensure base URL doesn't end with a slash
            baseUrl = baseUrl.TrimEnd('/');

            return $"{baseUrl}/approval/requests/{requestId}";
        }

        #endregion
    }
}
