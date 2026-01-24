using System;
using Newtonsoft.Json;
using WebVella.Erp.Api;
using WebVella.Erp.Jobs;

namespace WebVella.Erp.Plugins.Approval
{
    /// <summary>
    /// Main entry point for the Approval Workflow plugin.
    /// Extends the ErpPlugin base class to integrate with WebVella ERP's plugin system.
    /// Provides approval workflow functionality including multi-step approvals, 
    /// rule-based routing, and configurable notification/escalation processes.
    /// </summary>
    /// <remarks>
    /// This plugin is auto-discovered by WebVella ERP during startup.
    /// The Initialize method is called to set up database migrations and scheduled jobs.
    /// 
    /// Plugin responsibilities:
    /// - Register and run database migration patches via ProcessPatches()
    /// - Configure background job schedules via SetSchedulePlans()
    /// - Provide approval workflow entities, services, and UI components
    /// 
    /// Background jobs registered:
    /// - ProcessApprovalNotificationsJob: 5-minute interval for sending approval notifications
    /// - ProcessApprovalEscalationsJob: 30-minute interval for handling timed-out approvals
    /// - CleanupExpiredApprovalsJob: Daily at 00:10 UTC for expiring stale requests
    /// </remarks>
    public partial class ApprovalPlugin : ErpPlugin
    {
        #region << Properties >>

        /// <summary>
        /// Gets the unique name identifier for this plugin.
        /// This name is used for plugin discovery, configuration, and API routing.
        /// </summary>
        /// <value>Returns "approval" as the plugin identifier.</value>
        [JsonProperty(PropertyName = "name")]
        public override string Name { get; protected set; } = "approval";

        #endregion

        #region << Schedule Plan GUIDs >>

        /// <summary>
        /// Unique identifier for the notifications schedule plan.
        /// Used to check existence and create the 5-minute notification processing schedule.
        /// </summary>
        private static readonly Guid NOTIFICATIONS_SCHEDULE_PLAN_ID = new Guid("B1A2C3D4-E5F6-7890-ABCD-1234567890AB");

        /// <summary>
        /// Unique identifier for the escalations schedule plan.
        /// Used to check existence and create the 30-minute escalation processing schedule.
        /// </summary>
        private static readonly Guid ESCALATIONS_SCHEDULE_PLAN_ID = new Guid("C2B3D4E5-F6A7-8901-BCDE-2345678901BC");

        /// <summary>
        /// Unique identifier for the cleanup schedule plan.
        /// Used to check existence and create the daily expired approvals cleanup schedule.
        /// </summary>
        private static readonly Guid CLEANUP_SCHEDULE_PLAN_ID = new Guid("D3C4E5F6-A7B8-9012-CDEF-3456789012CD");

        #endregion

        #region << Job Type GUIDs >>

        /// <summary>
        /// Job type identifier for ProcessApprovalNotificationsJob.
        /// Must match the GUID in the [Job] attribute on the job class.
        /// </summary>
        private static readonly Guid NOTIFICATIONS_JOB_TYPE_ID = new Guid("A7B3C1D2-E4F5-6789-ABCD-EF0123456789");

        /// <summary>
        /// Job type identifier for ProcessApprovalEscalationsJob.
        /// Must match the GUID in the [Job] attribute on the job class.
        /// </summary>
        private static readonly Guid ESCALATIONS_JOB_TYPE_ID = new Guid("A7B3C9D1-E4F5-4A6B-8C7D-9E0F1A2B3C4D");

        /// <summary>
        /// Job type identifier for CleanupExpiredApprovalsJob.
        /// Must match the GUID in the [Job] attribute on the job class.
        /// </summary>
        private static readonly Guid CLEANUP_JOB_TYPE_ID = new Guid("E8C7B4A3-5D6F-4E2A-9B1C-8D7E6F5A4B3C");

        #endregion

        #region << Public Methods >>

        /// <summary>
        /// Initializes the Approval plugin during application startup.
        /// This method is called by the WebVella ERP framework when the plugin is loaded.
        /// Executes within a system security scope to ensure elevated permissions for
        /// database migrations and schedule plan registration.
        /// </summary>
        /// <param name="serviceProvider">
        /// The application's service provider for dependency resolution.
        /// Can be used to access registered services if needed during initialization.
        /// </param>
        /// <remarks>
        /// Initialization sequence:
        /// 1. Opens a system security scope for elevated permissions
        /// 2. Calls ProcessPatches() to apply database migrations
        /// 3. Calls SetSchedulePlans() to register background job schedules
        /// 
        /// Both ProcessPatches and SetSchedulePlans handle their own error scenarios
        /// and are designed to be idempotent (safe to run multiple times).
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
        /// Registers background job schedules for the approval workflow system.
        /// Creates three schedule plans if they don't already exist:
        /// - Notification processing every 5 minutes
        /// - Escalation processing every 30 minutes
        /// - Expired approval cleanup daily at 00:10 UTC
        /// </summary>
        /// <remarks>
        /// This method is idempotent - it checks for existing schedule plans before creating new ones.
        /// Each schedule plan is associated with its corresponding ErpJob implementation via JobTypeId.
        /// 
        /// Schedule plans are persisted in the database and managed by the ScheduleManager.
        /// The job framework will automatically execute jobs according to their configured schedules.
        /// </remarks>
        public void SetSchedulePlans()
        {
            DateTime utcNow = DateTime.UtcNow;

            #region << ProcessApprovalNotificationsJob - 5 minute interval >>
            {
                SchedulePlan notificationsSchedulePlan = ScheduleManager.Current.GetSchedulePlan(NOTIFICATIONS_SCHEDULE_PLAN_ID);

                if (notificationsSchedulePlan == null)
                {
                    notificationsSchedulePlan = new SchedulePlan();
                    notificationsSchedulePlan.Id = NOTIFICATIONS_SCHEDULE_PLAN_ID;
                    notificationsSchedulePlan.Name = "Process approval notifications";
                    notificationsSchedulePlan.Type = SchedulePlanType.Interval;
                    notificationsSchedulePlan.StartDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);
                    notificationsSchedulePlan.EndDate = null;
                    notificationsSchedulePlan.ScheduledDays = new SchedulePlanDaysOfWeek()
                    {
                        ScheduledOnMonday = true,
                        ScheduledOnTuesday = true,
                        ScheduledOnWednesday = true,
                        ScheduledOnThursday = true,
                        ScheduledOnFriday = true,
                        ScheduledOnSaturday = true,
                        ScheduledOnSunday = true
                    };
                    notificationsSchedulePlan.IntervalInMinutes = 5;
                    notificationsSchedulePlan.StartTimespan = 0;
                    notificationsSchedulePlan.EndTimespan = 1440;
                    notificationsSchedulePlan.JobTypeId = NOTIFICATIONS_JOB_TYPE_ID;
                    notificationsSchedulePlan.JobAttributes = null;
                    notificationsSchedulePlan.Enabled = true;
                    notificationsSchedulePlan.LastModifiedBy = null;

                    ScheduleManager.Current.CreateSchedulePlan(notificationsSchedulePlan);
                }
            }
            #endregion

            #region << ProcessApprovalEscalationsJob - 30 minute interval >>
            {
                SchedulePlan escalationsSchedulePlan = ScheduleManager.Current.GetSchedulePlan(ESCALATIONS_SCHEDULE_PLAN_ID);

                if (escalationsSchedulePlan == null)
                {
                    escalationsSchedulePlan = new SchedulePlan();
                    escalationsSchedulePlan.Id = ESCALATIONS_SCHEDULE_PLAN_ID;
                    escalationsSchedulePlan.Name = "Process approval escalations";
                    escalationsSchedulePlan.Type = SchedulePlanType.Interval;
                    escalationsSchedulePlan.StartDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);
                    escalationsSchedulePlan.EndDate = null;
                    escalationsSchedulePlan.ScheduledDays = new SchedulePlanDaysOfWeek()
                    {
                        ScheduledOnMonday = true,
                        ScheduledOnTuesday = true,
                        ScheduledOnWednesday = true,
                        ScheduledOnThursday = true,
                        ScheduledOnFriday = true,
                        ScheduledOnSaturday = true,
                        ScheduledOnSunday = true
                    };
                    escalationsSchedulePlan.IntervalInMinutes = 30;
                    escalationsSchedulePlan.StartTimespan = 0;
                    escalationsSchedulePlan.EndTimespan = 1440;
                    escalationsSchedulePlan.JobTypeId = ESCALATIONS_JOB_TYPE_ID;
                    escalationsSchedulePlan.JobAttributes = null;
                    escalationsSchedulePlan.Enabled = true;
                    escalationsSchedulePlan.LastModifiedBy = null;

                    ScheduleManager.Current.CreateSchedulePlan(escalationsSchedulePlan);
                }
            }
            #endregion

            #region << CleanupExpiredApprovalsJob - Daily at 00:10 UTC >>
            {
                SchedulePlan cleanupSchedulePlan = ScheduleManager.Current.GetSchedulePlan(CLEANUP_SCHEDULE_PLAN_ID);

                if (cleanupSchedulePlan == null)
                {
                    cleanupSchedulePlan = new SchedulePlan();
                    cleanupSchedulePlan.Id = CLEANUP_SCHEDULE_PLAN_ID;
                    cleanupSchedulePlan.Name = "Cleanup expired approvals";
                    cleanupSchedulePlan.Type = SchedulePlanType.Daily;
                    cleanupSchedulePlan.StartDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 10, 0, DateTimeKind.Utc);
                    cleanupSchedulePlan.EndDate = null;
                    cleanupSchedulePlan.ScheduledDays = new SchedulePlanDaysOfWeek()
                    {
                        ScheduledOnMonday = true,
                        ScheduledOnTuesday = true,
                        ScheduledOnWednesday = true,
                        ScheduledOnThursday = true,
                        ScheduledOnFriday = true,
                        ScheduledOnSaturday = true,
                        ScheduledOnSunday = true
                    };
                    cleanupSchedulePlan.IntervalInMinutes = 1440;
                    cleanupSchedulePlan.StartTimespan = 0;
                    cleanupSchedulePlan.EndTimespan = 1440;
                    cleanupSchedulePlan.JobTypeId = CLEANUP_JOB_TYPE_ID;
                    cleanupSchedulePlan.JobAttributes = null;
                    cleanupSchedulePlan.Enabled = true;
                    cleanupSchedulePlan.LastModifiedBy = null;

                    ScheduleManager.Current.CreateSchedulePlan(cleanupSchedulePlan);
                }
            }
            #endregion
        }

        #endregion
    }
}
