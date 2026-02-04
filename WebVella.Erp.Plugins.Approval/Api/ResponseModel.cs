using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace WebVella.Erp.Plugins.Approval.Api
{
    /// <summary>
    /// Standard API response envelope for the Approval plugin.
    /// Provides a consistent structure for all API responses with Success, Message, and Object properties.
    /// This is a simplified version of WebVella.Erp.Api.Models.ResponseModel tailored for approval plugin needs.
    /// </summary>
    /// <remarks>
    /// Used by ApprovalController to wrap all API responses with a consistent structure.
    /// The envelope pattern ensures that clients always receive a predictable response format
    /// regardless of the specific endpoint called.
    /// </remarks>
    public class ResponseModel
    {
        /// <summary>
        /// Indicates whether the operation completed successfully.
        /// </summary>
        /// <value>
        /// <c>true</c> if the operation was successful; otherwise, <c>false</c>.
        /// Default value is <c>false</c>.
        /// </value>
        [JsonProperty(PropertyName = "success")]
        public bool Success { get; set; }

        /// <summary>
        /// Human-readable message describing the result of the operation.
        /// </summary>
        /// <value>
        /// A string containing a descriptive message about the operation result.
        /// This may contain success messages, error details, or validation feedback.
        /// Can be <c>null</c> if no message is provided.
        /// </value>
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }

        /// <summary>
        /// The response payload data containing the actual result of the operation.
        /// </summary>
        /// <value>
        /// An object containing the response data. The type depends on the specific endpoint.
        /// For list endpoints, this typically contains a collection of DTOs.
        /// For single-item endpoints, this contains a single DTO.
        /// Default value is <c>null</c>.
        /// </value>
        [JsonProperty(PropertyName = "object")]
        public object Object { get; set; }

        /// <summary>
        /// Collection of validation errors when the operation fails due to validation issues.
        /// </summary>
        /// <value>
        /// A list of validation error objects. Can be <c>null</c> if no validation errors occurred.
        /// Typically contains WebVella.Erp.Exceptions.ValidationError objects when validation fails.
        /// </value>
        [JsonProperty(PropertyName = "errors")]
        public object Errors { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseModel"/> class
        /// with default values: Success = false, Message = null, Object = null, Errors = null.
        /// </summary>
        public ResponseModel()
        {
            Success = false;
            Message = null;
            Object = null;
            Errors = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseModel"/> class
        /// with the specified success status, message, and data object.
        /// </summary>
        /// <param name="success">Indicates whether the operation was successful.</param>
        /// <param name="message">A human-readable message describing the result.</param>
        /// <param name="data">The response payload data.</param>
        public ResponseModel(bool success, string message, object data)
        {
            Success = success;
            Message = message;
            Object = data;
        }

        /// <summary>
        /// Creates a successful response with the specified data object.
        /// </summary>
        /// <param name="data">The response payload data.</param>
        /// <param name="message">Optional success message. Defaults to "Operation completed successfully."</param>
        /// <returns>A <see cref="ResponseModel"/> instance configured for a successful response.</returns>
        public static ResponseModel CreateSuccess(object data, string message = "Operation completed successfully.")
        {
            return new ResponseModel
            {
                Success = true,
                Message = message,
                Object = data
            };
        }

        /// <summary>
        /// Creates an error response with the specified error message.
        /// </summary>
        /// <param name="message">A human-readable error message describing what went wrong.</param>
        /// <returns>A <see cref="ResponseModel"/> instance configured for an error response.</returns>
        public static ResponseModel CreateError(string message)
        {
            return new ResponseModel
            {
                Success = false,
                Message = message,
                Object = null
            };
        }

        /// <summary>
        /// Creates an error response with the specified error message and additional error data.
        /// </summary>
        /// <param name="message">A human-readable error message describing what went wrong.</param>
        /// <param name="errorData">Additional error data such as validation errors or exception details.</param>
        /// <returns>A <see cref="ResponseModel"/> instance configured for an error response with additional data.</returns>
        public static ResponseModel CreateError(string message, object errorData)
        {
            return new ResponseModel
            {
                Success = false,
                Message = message,
                Object = errorData
            };
        }
    }
}
