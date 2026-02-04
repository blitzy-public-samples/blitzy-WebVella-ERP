using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using WebVella.Erp.Api;
using WebVella.Erp.Jobs;

namespace WebVella.Erp.Plugins.Approval
{
    /// <summary>
    /// Main plugin entry point for the Approval Workflow system.
    /// This plugin provides a complete approval workflow solution for WebVella ERP,
    /// including workflow configuration, request processing, and notification services.
    /// </summary>
    /// <remarks>
    /// The plugin registers three background jobs:
    /// <list type="bullet">
    ///   <item><description>ApprovalNotificationJob - Sends pending approval notifications every 5 minutes</description></item>
    ///   <item><description>ApprovalEscalationJob - Escalates overdue approval requests every 30 minutes</description></item>
    ///   <item><description>ApprovalCleanupJob - Archives completed requests and cleans up orphaned records daily</description></item>
    /// </list>
    /// </remarks>
    public partial class ApprovalPlugin : ErpPlugin
    {
        /// <summary>
        /// Gets the unique name identifier for this plugin.
        /// </summary>
        /// <value>Returns "approval" as the plugin identifier.</value>
        [JsonProperty(PropertyName = "name")]
        public override string Name { get; protected set; } = "approval";

        /// <summary>
        /// Initializes the Approval plugin by executing migration patches and registering scheduled jobs.
        /// This method is called by the WebVella ERP framework during application startup.
        /// </summary>
        /// <param name="serviceProvider">The service provider for dependency injection.</param>
        /// <remarks>
        /// Initialization is performed within a system security context to ensure
        /// all database operations have elevated permissions.
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
        /// if they do not already exist.
        /// </summary>
        /// <remarks>
        /// Schedule plan configuration:
        /// <list type="bullet">
        ///   <item><description>Notification Job: Runs every 5 minutes (Interval type)</description></item>
        ///   <item><description>Escalation Job: Runs every 30 minutes (Interval type)</description></item>
        ///   <item><description>Cleanup Job: Runs daily at midnight UTC (Daily type)</description></item>
        /// </list>
        /// </remarks>
        public void SetSchedulePlans()
        {
            DateTime utcNow = DateTime.UtcNow;

            #region << ApprovalNotificationJob - Every 5 minutes >>
            {
                Guid schedulePlanId = new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");
                string planName = "Send pending approval notifications";
                SchedulePlan schedulePlan = ScheduleManager.Current.GetSchedulePlan(schedulePlanId);

                if (schedulePlan == null)
                {
                    schedulePlan = new SchedulePlan();
                    schedulePlan.Id = schedulePlanId;
                    schedulePlan.Name = planName;
                    schedulePlan.Type = SchedulePlanType.Interval;
                    schedulePlan.StartDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);
                    schedulePlan.EndDate = null;
                    schedulePlan.ScheduledDays = new SchedulePlanDaysOfWeek()
                    {
                        ScheduledOnMonday = true,
                        ScheduledOnTuesday = true,
                        ScheduledOnWednesday = true,
                        ScheduledOnThursday = true,
                        ScheduledOnFriday = true,
                        ScheduledOnSaturday = true,
                        ScheduledOnSunday = true
                    };
                    schedulePlan.IntervalInMinutes = 5;
                    schedulePlan.StartTimespan = 0;
                    schedulePlan.EndTimespan = 1440;
                    schedulePlan.JobTypeId = new Guid("D4E5F6A7-B8C9-0123-DEFG-456789ABCDEF");
                    schedulePlan.JobAttributes = null;
                    schedulePlan.Enabled = true;
                    schedulePlan.LastModifiedBy = null;

                    ScheduleManager.Current.CreateSchedulePlan(schedulePlan);
                }
            }
            #endregion

            #region << ApprovalEscalationJob - Every 30 minutes >>
            {
                Guid schedulePlanId = new Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901");
                string planName = "Escalate overdue approval requests";
                SchedulePlan schedulePlan = ScheduleManager.Current.GetSchedulePlan(schedulePlanId);

                if (schedulePlan == null)
                {
                    schedulePlan = new SchedulePlan();
                    schedulePlan.Id = schedulePlanId;
                    schedulePlan.Name = planName;
                    schedulePlan.Type = SchedulePlanType.Interval;
                    schedulePlan.StartDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);
                    schedulePlan.EndDate = null;
                    schedulePlan.ScheduledDays = new SchedulePlanDaysOfWeek()
                    {
                        ScheduledOnMonday = true,
                        ScheduledOnTuesday = true,
                        ScheduledOnWednesday = true,
                        ScheduledOnThursday = true,
                        ScheduledOnFriday = true,
                        ScheduledOnSaturday = true,
                        ScheduledOnSunday = true
                    };
                    schedulePlan.IntervalInMinutes = 30;
                    schedulePlan.StartTimespan = 0;
                    schedulePlan.EndTimespan = 1440;
                    schedulePlan.JobTypeId = new Guid("E5F6A7B8-C9D0-1234-EFGH-56789ABCDEF0");
                    schedulePlan.JobAttributes = null;
                    schedulePlan.Enabled = true;
                    schedulePlan.LastModifiedBy = null;

                    ScheduleManager.Current.CreateSchedulePlan(schedulePlan);
                }
            }
            #endregion

            #region << ApprovalCleanupJob - Daily at midnight >>
            {
                Guid schedulePlanId = new Guid("C3D4E5F6-A7B8-9012-CDEF-123456789012");
                string planName = "Archive and cleanup approval records";
                SchedulePlan schedulePlan = ScheduleManager.Current.GetSchedulePlan(schedulePlanId);

                if (schedulePlan == null)
                {
                    schedulePlan = new SchedulePlan();
                    schedulePlan.Id = schedulePlanId;
                    schedulePlan.Name = planName;
                    schedulePlan.Type = SchedulePlanType.Daily;
                    schedulePlan.StartDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);
                    schedulePlan.EndDate = null;
                    schedulePlan.ScheduledDays = new SchedulePlanDaysOfWeek()
                    {
                        ScheduledOnMonday = true,
                        ScheduledOnTuesday = true,
                        ScheduledOnWednesday = true,
                        ScheduledOnThursday = true,
                        ScheduledOnFriday = true,
                        ScheduledOnSaturday = true,
                        ScheduledOnSunday = true
                    };
                    schedulePlan.IntervalInMinutes = 1440;
                    schedulePlan.StartTimespan = 0;
                    schedulePlan.EndTimespan = 1440;
                    schedulePlan.JobTypeId = new Guid("F6A7B8C9-D0E1-2345-GHIJ-6789ABCDEF01");
                    schedulePlan.JobAttributes = null;
                    schedulePlan.Enabled = true;
                    schedulePlan.LastModifiedBy = null;

                    ScheduleManager.Current.CreateSchedulePlan(schedulePlan);
                }
            }
            #endregion
        }
    }
}
