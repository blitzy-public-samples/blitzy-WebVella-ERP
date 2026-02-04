/*
 * ApprovalUtils.cs
 * 
 * Purpose: Utility methods for the Approval plugin.
 * Contains helper functions for URL construction and other common operations.
 */
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace WebVella.Erp.Plugins.Approval.Utils
{
    /// <summary>
    /// Provides utility methods for the Approval plugin.
    /// </summary>
    public static class ApprovalUtils
    {
        /// <summary>
        /// Constructs the fully qualified application path from the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context from which to extract the request URL.</param>
        /// <returns>The fully qualified application path (e.g., "https://example.com").</returns>
        public static string FullyQualifiedApplicationPath(HttpContext context)
        {
            // Return variable declaration
            var appPath = string.Empty;

            // Checking the current context content
            if (context != null)
            {
                // Formatting the fully qualified website url/name
                appPath = string.Format("{0}://{1}",
                                        context.Request.Scheme,
                                        context.Request.Host);
            }

            return appPath;
        }

        /// <summary>
        /// Formats a nullable DateTime to a display string.
        /// </summary>
        /// <param name="dateTime">The DateTime value to format.</param>
        /// <param name="format">The format string to use.</param>
        /// <returns>The formatted date string or an empty string if null.</returns>
        public static string FormatDateTime(DateTime? dateTime, string format = "yyyy-MM-dd HH:mm:ss")
        {
            return dateTime?.ToString(format) ?? string.Empty;
        }

        /// <summary>
        /// Safely converts an object to a Guid, returning Guid.Empty if conversion fails.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The Guid value or Guid.Empty if conversion fails.</returns>
        public static Guid SafeGuid(object value)
        {
            if (value == null)
                return Guid.Empty;

            if (value is Guid guid)
                return guid;

            if (Guid.TryParse(value.ToString(), out var result))
                return result;

            return Guid.Empty;
        }

        /// <summary>
        /// Safely converts an object to a string, returning an empty string if null.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The string value or empty string if null.</returns>
        public static string SafeString(object value)
        {
            return value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Safely converts an object to an integer, returning 0 if conversion fails.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The integer value or 0 if conversion fails.</returns>
        public static int SafeInt(object value)
        {
            if (value == null)
                return 0;

            if (value is int intValue)
                return intValue;

            if (int.TryParse(value.ToString(), out var result))
                return result;

            return 0;
        }

        /// <summary>
        /// Safely converts an object to a boolean, returning false if conversion fails.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The boolean value or false if conversion fails.</returns>
        public static bool SafeBool(object value)
        {
            if (value == null)
                return false;

            if (value is bool boolValue)
                return boolValue;

            if (bool.TryParse(value.ToString(), out var result))
                return result;

            return false;
        }
    }
}
