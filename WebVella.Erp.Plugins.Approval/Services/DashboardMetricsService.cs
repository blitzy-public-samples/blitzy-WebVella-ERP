using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Plugins.Approval.Api;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Service for calculating and aggregating dashboard metrics for the Approval plugin.
    /// Provides real-time KPIs for the manager dashboard including pending count, average approval time,
    /// approval rate, overdue count, and recent activity count.
    /// </summary>
    public class DashboardMetricsService
    {
        private RecordManager _recordManager;

        /// <summary>
        /// Gets the RecordManager instance for database operations.
        /// Lazy-initialized to avoid constructor exceptions when ERP system is not initialized.
        /// </summary>
        protected RecordManager RecMan
        {
            get
            {
                if (_recordManager == null)
                {
                    _recordManager = new RecordManager();
                }
                return _recordManager;
            }
        }

        /// <summary>
        /// Retrieves the dashboard metrics containing real-time KPIs for the approval workflow system.
        /// </summary>
        /// <returns>A DashboardMetricsModel containing all 5 KPIs.</returns>
        public DashboardMetricsModel GetDashboardMetrics()
        {
            var result = new DashboardMetricsModel();

            try
            {
                // Calculate pending count - number of approval requests with status 'pending'
                result.PendingCount = GetPendingCount();

                // Calculate average approval time in hours
                result.AverageApprovalTimeHours = GetAverageApprovalTimeHours();

                // Calculate approval rate as percentage of approved vs total completed
                result.ApprovalRate = GetApprovalRate();

                // Calculate overdue/escalated count
                result.OverdueCount = GetOverdueCount();

                // Calculate recent activity count from last 24 hours
                result.RecentActivityCount = GetRecentActivityCount();
            }
            catch (Exception)
            {
                // On error, return empty metrics model with zeros
                // This allows dashboard to display even if entities don't exist yet
            }

            return result;
        }

        /// <summary>
        /// Gets the count of pending approval requests.
        /// </summary>
        /// <returns>Count of pending requests.</returns>
        private int GetPendingCount()
        {
            try
            {
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "pending")
                };
                var eql = "SELECT id FROM approval_request WHERE status = @status";
                var records = new EqlCommand(eql, eqlParams).Execute();
                return records?.Count ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Calculates the average approval time in hours for completed requests.
        /// </summary>
        /// <returns>Average approval time in hours.</returns>
        private decimal GetAverageApprovalTimeHours()
        {
            try
            {
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "approved")
                };
                var eql = "SELECT id, requested_on, completed_on FROM approval_request WHERE status = @status AND completed_on IS NOT NULL";
                var records = new EqlCommand(eql, eqlParams).Execute();

                if (records == null || !records.Any())
                    return 0m;

                decimal totalHours = 0m;
                int count = 0;

                foreach (var record in records)
                {
                    var requestedOn = record["requested_on"] as DateTime?;
                    var completedOn = record["completed_on"] as DateTime?;

                    if (requestedOn.HasValue && completedOn.HasValue)
                    {
                        var timeSpan = completedOn.Value - requestedOn.Value;
                        totalHours += (decimal)timeSpan.TotalHours;
                        count++;
                    }
                }

                if (count == 0)
                    return 0m;

                return Math.Round(totalHours / count, 1);
            }
            catch (Exception)
            {
                return 0m;
            }
        }

        /// <summary>
        /// Calculates the approval rate as a percentage of approved requests vs total completed requests.
        /// </summary>
        /// <returns>Approval rate percentage (0-100).</returns>
        private decimal GetApprovalRate()
        {
            try
            {
                // Get approved count
                var approvedParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "approved")
                };
                var approvedEql = "SELECT id FROM approval_request WHERE status = @status";
                var approvedRecords = new EqlCommand(approvedEql, approvedParams).Execute();
                var approvedCount = approvedRecords?.Count ?? 0;

                // Get rejected count
                var rejectedParams = new List<EqlParameter>
                {
                    new EqlParameter("status", "rejected")
                };
                var rejectedEql = "SELECT id FROM approval_request WHERE status = @status";
                var rejectedRecords = new EqlCommand(rejectedEql, rejectedParams).Execute();
                var rejectedCount = rejectedRecords?.Count ?? 0;

                var totalCompleted = approvedCount + rejectedCount;

                if (totalCompleted == 0)
                    return 0m;

                return Math.Round((decimal)approvedCount * 100 / totalCompleted, 1);
            }
            catch (Exception)
            {
                return 0m;
            }
        }

        /// <summary>
        /// Gets the count of overdue or escalated approval requests.
        /// </summary>
        /// <returns>Count of overdue/escalated requests.</returns>
        private int GetOverdueCount()
        {
            try
            {
                // Query for escalated or expired status
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("escalated", "escalated"),
                    new EqlParameter("expired", "expired")
                };
                var eql = "SELECT id FROM approval_request WHERE status = @escalated OR status = @expired";
                var records = new EqlCommand(eql, eqlParams).Execute();
                return records?.Count ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of recent approval activity within the last 24 hours.
        /// </summary>
        /// <returns>Count of recent activity items.</returns>
        private int GetRecentActivityCount()
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-24);
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("cutoff_time", cutoffTime)
                };
                var eql = "SELECT id FROM approval_history WHERE performed_on >= @cutoff_time";
                var records = new EqlCommand(eql, eqlParams).Execute();
                return records?.Count ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
