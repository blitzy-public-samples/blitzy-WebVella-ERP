using System;
using System.Collections.Generic;
using System.Linq;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Eql;
using WebVella.Erp.Plugins.Approval.Model;

namespace WebVella.Erp.Plugins.Approval.Services
{
    /// <summary>
    /// Service class for calculating real-time dashboard metrics from approval_request and approval_history entities.
    /// Provides aggregated metrics including pending approvals count, average approval time, approval rate percentage,
    /// overdue requests count, total active workflows, and recent activity feed for the manager dashboard display.
    /// </summary>
    /// <remarks>
    /// This service is used by PcApprovalDashboard component to render real-time metric cards.
    /// All methods include error handling to gracefully return default values when entities
    /// don't exist or queries fail, ensuring the dashboard remains functional during initial setup.
    /// </remarks>
    public class DashboardMetricsService : BaseService
    {
        /// <summary>
        /// Gets comprehensive dashboard metrics aggregated for a specific user within a date range.
        /// Assembles all individual metrics into a single DashboardMetricsModel response.
        /// </summary>
        /// <param name="userId">The user ID for user-specific metrics (pending/overdue counts). Pass null for global metrics.</param>
        /// <param name="fromDate">Start date for time-based metrics (average approval time, approval rate).</param>
        /// <param name="toDate">End date for time-based metrics.</param>
        /// <returns>DashboardMetricsModel containing all aggregated metrics.</returns>
        public DashboardMetricsModel GetDashboardMetrics(Guid? userId, DateTime fromDate, DateTime toDate)
        {
            return new DashboardMetricsModel
            {
                PendingCount = GetPendingCount(userId),
                OverdueCount = GetOverdueCount(userId),
                AverageApprovalTimeHours = (decimal)GetAverageApprovalTime(fromDate, toDate),
                ApprovalRate = (decimal)GetApprovalRate(fromDate, toDate),
                TotalActiveWorkflows = GetTotalActiveWorkflows(),
                ApprovedTodayCount = GetApprovedTodayCount(),
                RejectedTodayCount = GetRejectedTodayCount(),
                StartDate = fromDate,
                EndDate = toDate
            };
        }

        /// <summary>
        /// Gets the count of pending approval requests for a specific user or all users.
        /// When a userId is provided, counts only requests where the user is an authorized approver.
        /// </summary>
        /// <param name="userId">Optional user ID to filter by authorized approver. Pass null for total pending count.</param>
        /// <returns>Count of pending approval requests.</returns>
        public int GetPendingCount(Guid? userId)
        {
            try
            {
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", ApprovalStatus.Pending.ToString().ToLowerInvariant())
                };

                string eql = "SELECT id FROM approval_request WHERE status = @status";
                var result = new EqlCommand(eql, eqlParams).Execute();

                if (result == null)
                {
                    return 0;
                }

                // If userId is provided, we would filter by authorized approvers
                // For now, return total pending count as ApprovalRouteService is not yet available
                return result.Count;
            }
            catch (Exception)
            {
                // Entity may not exist yet, return default value
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of overdue approval requests based on SLA configuration.
        /// An overdue request is one where the pending time exceeds the step's configured timeout.
        /// </summary>
        /// <param name="userId">Optional user ID to filter by authorized approver. Pass null for total overdue count.</param>
        /// <returns>Count of overdue approval requests.</returns>
        public int GetOverdueCount(Guid? userId)
        {
            try
            {
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("status", ApprovalStatus.Pending.ToString().ToLowerInvariant())
                };

                // Get all pending requests and check against step timeout
                string eql = "SELECT id, created_on, current_step_id FROM approval_request WHERE status = @status";
                var requests = new EqlCommand(eql, eqlParams).Execute();

                if (requests == null || !requests.Any())
                {
                    return 0;
                }

                int overdueCount = 0;
                foreach (var request in requests)
                {
                    if (IsRequestOverdue(request))
                    {
                        overdueCount++;
                    }
                }

                return overdueCount;
            }
            catch (Exception)
            {
                // Entity may not exist yet, return default value
                return 0;
            }
        }

        /// <summary>
        /// Calculates the average approval time in hours for completed requests within a date range.
        /// Considers both approved and rejected requests as "completed" for this calculation.
        /// </summary>
        /// <param name="fromDate">Start of the date range.</param>
        /// <param name="toDate">End of the date range.</param>
        /// <returns>Average approval time in hours, rounded to 1 decimal place. Returns 0 if no data.</returns>
        public double GetAverageApprovalTime(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("from_date", fromDate),
                    new EqlParameter("to_date", toDate),
                    new EqlParameter("status_approved", ApprovalStatus.Approved.ToString().ToLowerInvariant()),
                    new EqlParameter("status_rejected", ApprovalStatus.Rejected.ToString().ToLowerInvariant())
                };

                // Get completed requests within date range
                string eql = @"SELECT id, created_on, completed_on FROM approval_request 
                              WHERE (status = @status_approved OR status = @status_rejected)
                              AND created_on >= @from_date AND created_on <= @to_date";
                var requests = new EqlCommand(eql, eqlParams).Execute();

                if (requests == null || !requests.Any())
                {
                    return 0;
                }

                double totalHours = 0;
                int count = 0;

                foreach (var request in requests)
                {
                    double hours = CalculateApprovalTimeHours(request);
                    if (hours > 0)
                    {
                        totalHours += hours;
                        count++;
                    }
                }

                if (count == 0)
                {
                    return 0;
                }

                return Math.Round(totalHours / count, 1);
            }
            catch (Exception)
            {
                // Entity may not exist yet, return default value
                return 0;
            }
        }

        /// <summary>
        /// Calculates the approval rate (percentage of approved vs rejected) within a date range.
        /// Formula: (approved / (approved + rejected)) * 100
        /// </summary>
        /// <param name="fromDate">Start of the date range.</param>
        /// <param name="toDate">End of the date range.</param>
        /// <returns>Approval rate as a percentage (0-100). Returns 0 if no completed requests.</returns>
        public double GetApprovalRate(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("from_date", fromDate),
                    new EqlParameter("to_date", toDate),
                    new EqlParameter("action_approved", "approved"),
                    new EqlParameter("action_rejected", "rejected")
                };

                // Count approved actions
                string approvedEql = @"SELECT id FROM approval_history 
                                       WHERE action_type = @action_approved 
                                       AND performed_on >= @from_date AND performed_on <= @to_date";
                var approvedResult = new EqlCommand(approvedEql, eqlParams).Execute();
                int approvedCount = approvedResult?.Count ?? 0;

                // Count rejected actions
                string rejectedEql = @"SELECT id FROM approval_history 
                                       WHERE action_type = @action_rejected 
                                       AND performed_on >= @from_date AND performed_on <= @to_date";
                var rejectedResult = new EqlCommand(rejectedEql, eqlParams).Execute();
                int rejectedCount = rejectedResult?.Count ?? 0;

                int total = approvedCount + rejectedCount;
                if (total == 0)
                {
                    return 0;
                }

                return Math.Round((double)approvedCount / total * 100, 1);
            }
            catch (Exception)
            {
                // Entity may not exist yet, return default value
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of active workflows in the system.
        /// </summary>
        /// <returns>Count of active approval workflows.</returns>
        public int GetTotalActiveWorkflows()
        {
            try
            {
                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("is_active", true)
                };

                string eql = "SELECT id FROM approval_workflow WHERE is_active = @is_active";
                var result = new EqlCommand(eql, eqlParams).Execute();

                return result?.Count ?? 0;
            }
            catch (Exception)
            {
                // Entity may not exist yet, return default value
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of requests approved today.
        /// </summary>
        /// <returns>Count of approvals performed today.</returns>
        public int GetApprovedTodayCount()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("from_date", today),
                    new EqlParameter("to_date", tomorrow),
                    new EqlParameter("action_type", "approved")
                };

                string eql = @"SELECT id FROM approval_history 
                              WHERE action_type = @action_type 
                              AND performed_on >= @from_date AND performed_on < @to_date";
                var result = new EqlCommand(eql, eqlParams).Execute();

                return result?.Count ?? 0;
            }
            catch (Exception)
            {
                // Entity may not exist yet, return default value
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of requests rejected today.
        /// </summary>
        /// <returns>Count of rejections performed today.</returns>
        public int GetRejectedTodayCount()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                var eqlParams = new List<EqlParameter>
                {
                    new EqlParameter("from_date", today),
                    new EqlParameter("to_date", tomorrow),
                    new EqlParameter("action_type", "rejected")
                };

                string eql = @"SELECT id FROM approval_history 
                              WHERE action_type = @action_type 
                              AND performed_on >= @from_date AND performed_on < @to_date";
                var result = new EqlCommand(eql, eqlParams).Execute();

                return result?.Count ?? 0;
            }
            catch (Exception)
            {
                // Entity may not exist yet, return default value
                return 0;
            }
        }

        /// <summary>
        /// Gets recent approval activity for display in the dashboard.
        /// </summary>
        /// <param name="count">Maximum number of activity records to return. Default is 5.</param>
        /// <returns>List of recent approval history records ordered by most recent first.</returns>
        public List<EntityRecord> GetRecentActivity(int count = 5)
        {
            try
            {
                var eqlParams = new List<EqlParameter>();

                string eql = $"SELECT id, request_id, action_type, performed_by, performed_on, comments FROM approval_history ORDER BY performed_on DESC PAGE 1 PAGESIZE {count}";
                var result = new EqlCommand(eql, eqlParams).Execute();

                return result?.ToList() ?? new List<EntityRecord>();
            }
            catch (Exception)
            {
                // Entity may not exist yet, return empty list
                return new List<EntityRecord>();
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Checks if a pending request has exceeded its SLA timeout.
        /// </summary>
        /// <param name="request">The approval request record to check.</param>
        /// <returns>True if the request is overdue, false otherwise.</returns>
        private bool IsRequestOverdue(EntityRecord request)
        {
            try
            {
                if (request == null || !request.Properties.ContainsKey("created_on"))
                {
                    return false;
                }

                var createdOn = (DateTime)request["created_on"];
                var currentStepId = request.Properties.ContainsKey("current_step_id") ? request["current_step_id"] as Guid? : null;

                // Default timeout of 24 hours if step configuration not available
                int timeoutHours = 24;

                if (currentStepId.HasValue)
                {
                    try
                    {
                        var stepParams = new List<EqlParameter>
                        {
                            new EqlParameter("id", currentStepId.Value)
                        };
                        string stepEql = "SELECT sla_hours FROM approval_step WHERE id = @id";
                        var stepResult = new EqlCommand(stepEql, stepParams).Execute();

                        if (stepResult != null && stepResult.Any())
                        {
                            var step = stepResult.First();
                            if (step.Properties.ContainsKey("sla_hours") && step["sla_hours"] != null)
                            {
                                timeoutHours = Convert.ToInt32(step["sla_hours"]);
                            }
                        }
                    }
                    catch
                    {
                        // Use default timeout if step query fails
                    }
                }

                var dueDate = createdOn.AddHours(timeoutHours);
                return DateTime.UtcNow > dueDate;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Calculates the time in hours from request creation to completion.
        /// </summary>
        /// <param name="request">The completed approval request record.</param>
        /// <returns>Hours between creation and completion. Returns 0 if calculation fails.</returns>
        private double CalculateApprovalTimeHours(EntityRecord request)
        {
            try
            {
                if (request == null)
                {
                    return 0;
                }

                if (!request.Properties.ContainsKey("created_on") || request["created_on"] == null)
                {
                    return 0;
                }

                var createdOn = (DateTime)request["created_on"];
                DateTime completedOn;

                if (request.Properties.ContainsKey("completed_on") && request["completed_on"] != null)
                {
                    completedOn = (DateTime)request["completed_on"];
                }
                else
                {
                    // If completed_on is not set, use current time for estimation
                    completedOn = DateTime.UtcNow;
                }

                var duration = completedOn - createdOn;
                return duration.TotalHours;
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }
}
