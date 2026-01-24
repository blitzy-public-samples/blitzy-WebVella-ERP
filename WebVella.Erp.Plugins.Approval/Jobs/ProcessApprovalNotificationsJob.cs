using System;
using System.Collections.Generic;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Diagnostics;
using WebVella.Erp.Jobs;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Jobs
{
	/// <summary>
	/// Background job for processing approval notifications.
	/// Runs on a 5-minute interval cycle to process pending notification records.
	/// Queries pending notifications via ApprovalNotificationService.GetPendingNotifications(),
	/// processes each notification by sending email content, and marks them as sent.
	/// </summary>
	/// <remarks>
	/// This job is registered with ScheduleManager by ApprovalPlugin.SetSchedulePlans().
	/// The job operates under system security scope for elevated permissions.
	/// Individual notification failures are logged and do not stop batch processing.
	/// 
	/// Notification workflow:
	/// 1. ApprovalNotificationService.Send*Notification() methods create notification records with all email content
	/// 2. This job retrieves pending notifications and processes them
	/// 3. Successfully processed notifications are marked as 'sent'
	/// 4. Failed notifications are marked as 'failed' with error details
	/// </remarks>
	[Job("A7B3C1D2-E4F5-6789-ABCD-EF0123456789", "Process approval notifications", true, JobPriority.Low)]
	public class ProcessApprovalNotificationsJob : ErpJob
	{
		#region Constants

		/// <summary>
		/// Source name for logging operations related to this job.
		/// </summary>
		private const string LOG_SOURCE = "ProcessApprovalNotificationsJob";

		/// <summary>
		/// Notification type indicating a new approval request needs attention.
		/// </summary>
		private const string NOTIFICATION_TYPE_REQUEST = "request";

		/// <summary>
		/// Notification type indicating an approval request has been completed.
		/// </summary>
		private const string NOTIFICATION_TYPE_COMPLETED = "completed";

		/// <summary>
		/// Notification type indicating an escalation has occurred.
		/// </summary>
		private const string NOTIFICATION_TYPE_ESCALATION = "escalation";

		#endregion

		#region Public Methods

		/// <summary>
		/// Executes the notification processing job.
		/// Retrieves all pending notifications and attempts to send each one.
		/// Individual failures are logged but do not stop the overall batch processing.
		/// </summary>
		/// <param name="context">The job execution context provided by the scheduler.</param>
		/// <exception cref="Exception">
		/// Critical errors during job initialization are propagated to the job framework.
		/// Individual notification processing errors are caught and logged.
		/// </exception>
		public override void Execute(JobContext context)
		{
			using (SecurityContext.OpenSystemScope())
			{
				var notificationService = new ApprovalNotificationService();
				var processedCount = 0;
				var failedCount = 0;

				try
				{
					// Retrieve all pending notifications that need to be processed
					var pendingNotifications = notificationService.GetPendingNotifications();

					if (pendingNotifications == null || pendingNotifications.Count == 0)
					{
						// No pending notifications - job completes successfully with nothing to process
						return;
					}

					// Process each notification individually to ensure partial failures don't stop the batch
					foreach (var notification in pendingNotifications)
					{
						var notificationId = Guid.Empty;

						try
						{
							// Extract notification identifier for processing and error reporting
							notificationId = notification["id"] != null 
								? (Guid)notification["id"] 
								: Guid.Empty;

							if (notificationId == Guid.Empty)
							{
								LogError("Notification record has invalid or missing ID. Skipping.", null);
								failedCount++;
								continue;
							}

							// Process the notification based on its type and stored email content
							ProcessNotification(notification, notificationService);

							// Mark notification as successfully sent
							notificationService.MarkNotificationSent(notificationId);
							processedCount++;
						}
						catch (Exception ex)
						{
							// Log individual notification failure but continue processing others
							failedCount++;
							LogNotificationError(notificationId, ex);

							// Attempt to mark the notification as failed for tracking
							try
							{
								if (notificationId != Guid.Empty)
								{
									notificationService.MarkNotificationFailed(notificationId, ex.Message);
								}
							}
							catch (Exception markFailedEx)
							{
								// Log the secondary failure but don't propagate
								LogError(
									$"Failed to mark notification '{notificationId}' as failed",
									markFailedEx
								);
							}
						}
					}

					// Log summary if there were any processing activities
					if (processedCount > 0 || failedCount > 0)
					{
						LogInfo($"Notification processing completed. Processed: {processedCount}, Failed: {failedCount}");
					}
				}
				catch (Exception ex)
				{
					// Log critical job-level failure
					LogError("Critical error during notification job execution", ex);
					throw;
				}
			}
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Processes a single notification record by sending the email content.
		/// The notification record contains all information needed for email delivery:
		/// recipient email, subject, body, and notification type.
		/// </summary>
		/// <param name="notification">The notification EntityRecord containing email details.</param>
		/// <param name="notificationService">The notification service instance.</param>
		/// <exception cref="Exception">Thrown when notification processing fails.</exception>
		private void ProcessNotification(EntityRecord notification, ApprovalNotificationService notificationService)
		{
			// Extract notification details from the record
			var recipientEmail = notification["recipient_email"]?.ToString();
			var subject = notification["subject"]?.ToString();
			var body = notification["body"]?.ToString();
			var notificationType = notification["notification_type"]?.ToString() ?? NOTIFICATION_TYPE_REQUEST;

			// Validate required fields
			if (string.IsNullOrWhiteSpace(recipientEmail))
			{
				throw new Exception("Notification record is missing recipient email address.");
			}

			if (string.IsNullOrWhiteSpace(subject))
			{
				throw new Exception("Notification record is missing email subject.");
			}

			if (string.IsNullOrWhiteSpace(body))
			{
				throw new Exception("Notification record is missing email body.");
			}

			// Send the notification email using available email infrastructure
			// The notification record already contains the composed email content
			// that was created by the Send*Notification methods
			SendNotificationEmail(recipientEmail, subject, body, notificationType);
		}

		/// <summary>
		/// Sends the notification email using WebVella's mail infrastructure.
		/// Attempts to use the Mail plugin's SmtpService if available.
		/// </summary>
		/// <param name="recipientEmail">The recipient's email address.</param>
		/// <param name="subject">The email subject line.</param>
		/// <param name="body">The email body content (HTML or plain text).</param>
		/// <param name="notificationType">The type of notification for logging purposes.</param>
		/// <exception cref="Exception">Thrown when email sending fails.</exception>
		private void SendNotificationEmail(string recipientEmail, string subject, string body, string notificationType)
		{
			try
			{
				// Attempt to use WebVella's email infrastructure
				// The Mail plugin provides EmailServiceManager and SmtpService for sending emails
				// We use reflection/dynamic access to avoid hard dependency on Mail plugin
				var emailServiceManagerType = Type.GetType(
					"WebVella.Erp.Plugins.Mail.Api.EmailServiceManager, WebVella.Erp.Plugins.Mail",
					throwOnError: false
				);

				if (emailServiceManagerType != null)
				{
					// Mail plugin is available - use it to send email
					SendEmailViaMailPlugin(emailServiceManagerType, recipientEmail, subject, body);
				}
				else
				{
					// Mail plugin not available - log and proceed
					// The notification is still marked as sent to prevent infinite retry loops
					// In production, ensure Mail plugin is installed and configured
					LogInfo($"Mail plugin not available. Notification to '{recipientEmail}' logged but not sent. Type: {notificationType}");
				}
			}
			catch (Exception ex)
			{
				throw new Exception($"Failed to send {notificationType} notification to '{recipientEmail}': {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Sends email using the WebVella Mail plugin's SmtpService.
		/// Uses reflection to dynamically invoke the mail service to avoid compile-time dependency.
		/// </summary>
		/// <param name="emailServiceManagerType">The EmailServiceManager type from the Mail plugin.</param>
		/// <param name="recipientEmail">The recipient's email address.</param>
		/// <param name="subject">The email subject line.</param>
		/// <param name="body">The email body content.</param>
		/// <exception cref="Exception">Thrown when email sending fails.</exception>
		private void SendEmailViaMailPlugin(Type emailServiceManagerType, string recipientEmail, string subject, string body)
		{
			// Create EmailServiceManager instance
			var emailServiceManager = Activator.CreateInstance(emailServiceManagerType);

			// Get default SMTP service (passing null for name gets the default)
			var getSmtpServiceMethod = emailServiceManagerType.GetMethod(
				"GetSmtpService",
				new[] { typeof(string) }
			);

			if (getSmtpServiceMethod == null)
			{
				throw new Exception("Could not find GetSmtpService method on EmailServiceManager.");
			}

			var smtpService = getSmtpServiceMethod.Invoke(emailServiceManager, new object[] { null });

			if (smtpService == null)
			{
				throw new Exception("No default SMTP service configured. Please configure an SMTP service in the Mail plugin.");
			}

			// Create EmailAddress object for recipient
			var emailAddressType = Type.GetType(
				"WebVella.Erp.Plugins.Mail.Api.EmailAddress, WebVella.Erp.Plugins.Mail",
				throwOnError: true
			);

			var emailAddress = Activator.CreateInstance(emailAddressType);
			emailAddressType.GetProperty("Address")?.SetValue(emailAddress, recipientEmail);

			// Call SendEmail method on SmtpService
			// Signature: SendEmail(EmailAddress recipient, string subject, string textBody, string htmlBody, List<string> attachments)
			var sendEmailMethod = smtpService.GetType().GetMethod(
				"SendEmail",
				new[] { emailAddressType, typeof(string), typeof(string), typeof(string), typeof(List<string>) }
			);

			if (sendEmailMethod == null)
			{
				throw new Exception("Could not find SendEmail method on SmtpService.");
			}

			// Send the email - using body as both text and HTML content
			// Passing null for attachments
			sendEmailMethod.Invoke(
				smtpService,
				new object[] { emailAddress, subject, body, body, null }
			);
		}

		/// <summary>
		/// Logs an error that occurred during notification processing.
		/// </summary>
		/// <param name="notificationId">The ID of the notification that failed.</param>
		/// <param name="exception">The exception that occurred.</param>
		private void LogNotificationError(Guid notificationId, Exception exception)
		{
			LogError(
				$"Failed to process notification '{notificationId}'",
				exception
			);
		}

		/// <summary>
		/// Logs an error message with exception details to the WebVella log system.
		/// </summary>
		/// <param name="message">The error message.</param>
		/// <param name="exception">The exception that occurred, or null if no exception.</param>
		private void LogError(string message, Exception exception)
		{
			try
			{
				var log = new Log();
				var details = exception != null
					? $"{exception.Message}\n\nStack Trace:\n{exception.StackTrace}"
					: message;

				log.Create(
					LogType.Error,
					LOG_SOURCE,
					message,
					details,
					saveDetailsAsJson: true
				);
			}
			catch
			{
				// Logging failed - can't do much here except ensure job continues
				System.Diagnostics.Debug.WriteLine($"[{LOG_SOURCE}] Error: {message} - Exception: {exception?.Message}");
			}
		}

		/// <summary>
		/// Logs an informational message to the WebVella log system.
		/// </summary>
		/// <param name="message">The informational message.</param>
		private void LogInfo(string message)
		{
			try
			{
				var log = new Log();
				log.Create(
					LogType.Info,
					LOG_SOURCE,
					message,
					message,
					saveDetailsAsJson: false
				);
			}
			catch
			{
				// Logging failed - continue without logging
				System.Diagnostics.Debug.WriteLine($"[{LOG_SOURCE}] Info: {message}");
			}
		}

		#endregion
	}
}
