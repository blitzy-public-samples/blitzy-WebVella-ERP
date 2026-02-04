using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using WebVella.Erp.Api;
using WebVella.Erp.Jobs;

namespace WebVella.Erp.Plugins.Approval
{
    /// <summary>
    /// Main plugin entry point for the WebVella ERP Approval Workflow system.
    /// This plugin provides a complete approval workflow solution including workflow 
    /// configuration, request processing, notification services, and background job scheduling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Approval plugin integrates with WebVella ERP to provide:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Entity schemas for approval_workflow, approval_step, approval_rule, approval_request, and approval_history</description></item>
    ///   <item><description>Services for workflow routing, request processing, and history tracking</description></item>
    ///   <item><description>REST API endpoints for workflow management and approval actions</description></item>
    ///   <item><description>UI components for workflow configuration and approval execution</description></item>
    /// </list>
    /// <para>
    /// The plugin registers three background jobs for automated workflow processing:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>ApprovalNotificationJob - Sends pending approval notifications every 5 minutes</description></item>
    ///   <item><description>ApprovalEscalationJob - Escalates overdue approval requests every 30 minutes</description></item>
    ///   <item><description>ApprovalCleanupJob - Archives completed requests and cleans up orphaned records daily</description></item>
    /// </list>
    /// <para>
    /// Plugin initialization occurs within a system security context to ensure proper permissions
    /// for database migrations and schedule plan registration.
    /// </para>
    /// </remarks>
    public partial class ApprovalPlugin : ErpPlugin
    {
        #region Constants

        /// <summary>
        /// Schedule plan ID for the notification job.
        /// </summary>
        private static readonly Guid NotificationSchedulePlanId = new Guid("1A2B3C4D-5E6F-7A8B-9C0D-E1F2A3B4C5D6");

        /// <summary>
        /// Job type ID for the notification job (matches [Job] attribute on ApprovalNotificationJob).
        /// </summary>
        private static readonly Guid NotificationJobTypeId = new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");

        /// <summary>
        /// Schedule plan ID for the escalation job.
        /// </summary>
        private static readonly Guid EscalationSchedulePlanId = new Guid("2B3C4D5E-6F7A-8B9C-0D1E-F2A3B4C5D6E7");

        /// <summary>
        /// Job type ID for the escalation job (matches [Job] attribute on ApprovalEscalationJob).
        /// </summary>
        private static readonly Guid EscalationJobTypeId = new Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901");

        /// <summary>
        /// Schedule plan ID for the cleanup job.
        /// </summary>
        private static readonly Guid CleanupSchedulePlanId = new Guid("3C4D5E6F-7A8B-9C0D-1E2F-A3B4C5D6E7F8");

        /// <summary>
        /// Job type ID for the cleanup job (matches [Job] attribute on ApprovalCleanupJob).
        /// </summary>
        private static readonly Guid CleanupJobTypeId = new Guid("C3D4E5F6-A7B8-9012-CDEF-123456789012");

        /// <summary>
        /// Interval in minutes for the notification job (every 5 minutes).
        /// </summary>
        private const int NotificationIntervalMinutes = 5;

        /// <summary>
        /// Interval in minutes for the escalation job (every 30 minutes).
        /// </summary>
        private const int EscalationIntervalMinutes = 30;

        /// <summary>
        /// Interval in minutes for the cleanup job (daily = 1440 minutes).
        /// </summary>
        private const int CleanupIntervalMinutes = 1440;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the unique name identifier for this plugin.
        /// This name is used by the WebVella ERP framework to identify and manage the plugin.
        /// </summary>
        /// <value>Returns "approval" as the plugin identifier.</value>
        [JsonProperty(PropertyName = "name")]
        public override string Name { get; protected set; } = "approval";

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the Approval plugin by executing database migration patches 
        /// and registering scheduled background jobs.
        /// </summary>
        /// <param name="serviceProvider">
        /// The service provider instance for dependency injection support.
        /// Passed by the WebVella ERP framework during plugin initialization.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method is called automatically by the WebVella ERP framework during 
        /// application startup when the plugin assembly is discovered and loaded.
        /// </para>
        /// <para>
        /// Initialization performs two critical operations:
        /// </para>
        /// <list type="number">
        ///   <item><description>ProcessPatches() - Executes any pending database migration patches to ensure entity schemas and relationships are properly configured</description></item>
        ///   <item><description>SetSchedulePlans() - Registers background job schedule plans for automated workflow processing</description></item>
        /// </list>
        /// <para>
        /// Both operations are executed within SecurityContext.OpenSystemScope() to ensure
        /// elevated permissions for database modifications and schedule management.
        /// </para>
        /// </remarks>
        public override void Initialize(IServiceProvider serviceProvider)
        {
            using (var ctx = SecurityContext.OpenSystemScope())
            {
                ProcessPatches();
                SetSchedulePlans();
            }
        }

        /// <summary>
        /// Registers the scheduled jobs for the Approval plugin.
        /// Creates schedule plans for notification, escalation, and cleanup jobs
        /// if they do not already exist (idempotent registration).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method implements idempotent registration of schedule plans. Each plan
        /// is first checked for existence via ScheduleManager.Current.GetSchedulePlan()
        /// before being created, ensuring safe re-execution during application restarts.
        /// </para>
        /// <para>
        /// Schedule plan configuration:
        /// </para>
        /// <list type="bullet">
        ///   <item>
        ///     <term>Notification Job</term>
        ///     <description>Runs every 5 minutes (Interval type) to send pending approval notifications to approvers</description>
        ///   </item>
        ///   <item>
        ///     <term>Escalation Job</term>
        ///     <description>Runs every 30 minutes (Interval type) to escalate approval requests that have exceeded SLA thresholds</description>
        ///   </item>
        ///   <item>
        ///     <term>Cleanup Job</term>
        ///     <description>Runs daily at midnight UTC (Daily type) to archive completed requests and clean up orphaned records</description>
        ///   </item>
        /// </list>
        /// <para>
        /// All jobs are scheduled to run every day of the week and are enabled by default.
        /// Jobs execute within the full 24-hour day window (StartTimespan=0, EndTimespan=1440).
        /// </para>
        /// </remarks>
        public void SetSchedulePlans()
        {
            DateTime utcNow = DateTime.UtcNow;

            RegisterNotificationJobSchedule(utcNow);
            RegisterEscalationJobSchedule(utcNow);
            RegisterCleanupJobSchedule(utcNow);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Registers the schedule plan for the ApprovalNotificationJob if it does not exist.
        /// This job sends pending approval notifications every 5 minutes.
        /// </summary>
        /// <param name="utcNow">Current UTC timestamp for calculating the start date.</param>
        private void RegisterNotificationJobSchedule(DateTime utcNow)
        {
            SchedulePlan schedulePlan = ScheduleManager.Current.GetSchedulePlan(NotificationSchedulePlanId);

            if (schedulePlan == null)
            {
                schedulePlan = new SchedulePlan
                {
                    Id = NotificationSchedulePlanId,
                    Name = "Send pending approval notifications",
                    Type = SchedulePlanType.Interval,
                    StartDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = null,
                    ScheduledDays = CreateAllDaysSchedule(),
                    IntervalInMinutes = NotificationIntervalMinutes,
                    StartTimespan = 0,
                    EndTimespan = 1440,
                    JobTypeId = NotificationJobTypeId,
                    JobAttributes = null,
                    Enabled = true,
                    LastModifiedBy = null
                };

                ScheduleManager.Current.CreateSchedulePlan(schedulePlan);
            }
        }

        /// <summary>
        /// Registers the schedule plan for the ApprovalEscalationJob if it does not exist.
        /// This job escalates overdue approval requests every 30 minutes.
        /// </summary>
        /// <param name="utcNow">Current UTC timestamp for calculating the start date.</param>
        private void RegisterEscalationJobSchedule(DateTime utcNow)
        {
            SchedulePlan schedulePlan = ScheduleManager.Current.GetSchedulePlan(EscalationSchedulePlanId);

            if (schedulePlan == null)
            {
                schedulePlan = new SchedulePlan
                {
                    Id = EscalationSchedulePlanId,
                    Name = "Escalate overdue approval requests",
                    Type = SchedulePlanType.Interval,
                    StartDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = null,
                    ScheduledDays = CreateAllDaysSchedule(),
                    IntervalInMinutes = EscalationIntervalMinutes,
                    StartTimespan = 0,
                    EndTimespan = 1440,
                    JobTypeId = EscalationJobTypeId,
                    JobAttributes = null,
                    Enabled = true,
                    LastModifiedBy = null
                };

                ScheduleManager.Current.CreateSchedulePlan(schedulePlan);
            }
        }

        /// <summary>
        /// Registers the schedule plan for the ApprovalCleanupJob if it does not exist.
        /// This job archives completed requests and cleans up orphaned records daily.
        /// </summary>
        /// <param name="utcNow">Current UTC timestamp for calculating the start date.</param>
        private void RegisterCleanupJobSchedule(DateTime utcNow)
        {
            SchedulePlan schedulePlan = ScheduleManager.Current.GetSchedulePlan(CleanupSchedulePlanId);

            if (schedulePlan == null)
            {
                schedulePlan = new SchedulePlan
                {
                    Id = CleanupSchedulePlanId,
                    Name = "Archive and cleanup approval records",
                    Type = SchedulePlanType.Daily,
                    StartDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = null,
                    ScheduledDays = CreateAllDaysSchedule(),
                    IntervalInMinutes = CleanupIntervalMinutes,
                    StartTimespan = 0,
                    EndTimespan = 1440,
                    JobTypeId = CleanupJobTypeId,
                    JobAttributes = null,
                    Enabled = true,
                    LastModifiedBy = null
                };

                ScheduleManager.Current.CreateSchedulePlan(schedulePlan);
            }
        }

        /// <summary>
        /// Creates a SchedulePlanDaysOfWeek instance configured to run every day of the week.
        /// </summary>
        /// <returns>A SchedulePlanDaysOfWeek with all days enabled.</returns>
        private SchedulePlanDaysOfWeek CreateAllDaysSchedule()
        {
            return new SchedulePlanDaysOfWeek
            {
                ScheduledOnMonday = true,
                ScheduledOnTuesday = true,
                ScheduledOnWednesday = true,
                ScheduledOnThursday = true,
                ScheduledOnFriday = true,
                ScheduledOnSaturday = true,
                ScheduledOnSunday = true
            };
        }

        #endregion
    }
}
