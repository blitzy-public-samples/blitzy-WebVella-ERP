"use strict";

/**
 * Approval Dashboard Component Service
 * 
 * Provides client-side functionality for the Approval Dashboard PageComponent including:
 * - AJAX-based metrics retrieval from the dashboard API
 * - Auto-refresh timer management
 * - Date range filter handling
 * - UI update functions
 * 
 * @module PcApprovalDashboard/service
 */
(function (window, $) {

    /**
     * Dashboard API endpoint URL
     * @constant {string}
     */
    var API_ENDPOINT = '/api/v3.0/p/approval/dashboard/metrics';

    /**
     * Minimum refresh interval in milliseconds (30 seconds)
     * @constant {number}
     */
    var MIN_REFRESH_INTERVAL = 30000;

    /**
     * Initializes the approval dashboard component
     * @param {string} nodeId - Unique identifier for the dashboard instance
     * @param {Object} options - Configuration options
     * @param {number} options.refreshInterval - Refresh interval in seconds
     * @param {string} options.dateRangeDefault - Default date range ('7d', '30d', '90d')
     */
    function initDashboard(nodeId, options) {
        var container = document.getElementById('approval-dashboard-' + nodeId);
        if (!container) {
            console.warn('Approval Dashboard: Container not found for nodeId:', nodeId);
            return;
        }

        var refreshInterval = Math.max((options.refreshInterval || 60) * 1000, MIN_REFRESH_INTERVAL);
        var refreshTimer = null;

        /**
         * Calculates the date range based on the selected option
         * @param {string} range - Date range option ('7d', '30d', '90d')
         * @returns {Object} Object with from and to ISO date strings
         */
        function getDateRange(range) {
            var to = new Date();
            var from = new Date();

            switch (range) {
                case '7d':
                    from.setDate(from.getDate() - 7);
                    break;
                case '90d':
                    from.setDate(from.getDate() - 90);
                    break;
                case '30d':
                default:
                    from.setDate(from.getDate() - 30);
                    break;
            }

            return {
                from: from.toISOString(),
                to: to.toISOString()
            };
        }

        /**
         * Formats a Date object to HH:MM:SS string
         * @param {Date} date - Date to format
         * @returns {string} Formatted time string
         */
        function formatTime(date) {
            var hours = date.getHours().toString().padStart(2, '0');
            var minutes = date.getMinutes().toString().padStart(2, '0');
            var seconds = date.getSeconds().toString().padStart(2, '0');
            return hours + ':' + minutes + ':' + seconds;
        }

        /**
         * Fetches dashboard metrics from the API
         */
        function refreshMetrics() {
            var dateRangeSelector = document.getElementById('date-range-' + nodeId);
            var selectedRange = dateRangeSelector ? dateRangeSelector.value : '30d';
            var dateRange = getDateRange(selectedRange);

            var url = API_ENDPOINT +
                '?from=' + encodeURIComponent(dateRange.from) +
                '&to=' + encodeURIComponent(dateRange.to);

            fetch(url, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                credentials: 'same-origin'
            })
            .then(function (response) {
                if (!response.ok) {
                    if (response.status === 403) {
                        throw new Error('Access denied. Manager role required.');
                    }
                    throw new Error('Network response was not ok: ' + response.status);
                }
                return response.json();
            })
            .then(function (data) {
                if (data.success && data.object) {
                    updateDisplay(data.object);
                } else {
                    console.warn('Approval Dashboard: API returned unsuccessful response', data);
                }
            })
            .catch(function (error) {
                console.error('Approval Dashboard: Error refreshing metrics:', error);
            });
        }

        /**
         * Updates the dashboard UI with new metrics data
         * @param {Object} metrics - Metrics data from API
         */
        function updateDisplay(metrics) {
            // Update pending approvals count
            var pendingEl = document.getElementById('pending-count-' + nodeId);
            if (pendingEl) {
                pendingEl.textContent = metrics.pending_approvals_count || 0;
            }

            // Update average approval time
            var avgTimeEl = document.getElementById('avg-time-' + nodeId);
            if (avgTimeEl) {
                var avgTime = metrics.average_approval_time_hours || 0;
                avgTimeEl.textContent = avgTime.toFixed(1) + ' hrs';
            }

            // Update approval rate
            var approvalRateEl = document.getElementById('approval-rate-' + nodeId);
            if (approvalRateEl) {
                var approvalRate = metrics.approval_rate_percent || 0;
                approvalRateEl.textContent = approvalRate.toFixed(1) + '%';

                // Update progress bar if present
                var progressBar = approvalRateEl.closest('.card-body')?.querySelector('.progress-bar');
                if (progressBar) {
                    progressBar.style.width = approvalRate + '%';
                    progressBar.setAttribute('aria-valuenow', approvalRate);
                }
            }

            // Update overdue count
            var overdueEl = document.getElementById('overdue-count-' + nodeId);
            if (overdueEl) {
                var overdueCount = metrics.overdue_requests_count || 0;
                overdueEl.innerHTML = overdueCount;

                if (overdueCount > 0) {
                    overdueEl.innerHTML += ' <i class="fas fa-exclamation-triangle ml-1"></i>';
                    overdueEl.classList.add('text-danger');
                    overdueEl.classList.remove('text-gray-800');
                } else {
                    overdueEl.classList.remove('text-danger');
                    overdueEl.classList.add('text-gray-800');
                }
            }

            // Update last updated timestamp
            var lastUpdatedEl = document.getElementById('last-updated-' + nodeId);
            if (lastUpdatedEl) {
                lastUpdatedEl.innerHTML =
                    '<i class="fas fa-sync-alt mr-1"></i>Updated: ' + formatTime(new Date());
            }

            // Update recent activity list
            updateRecentActivity(metrics.recent_activity || []);
        }

        /**
         * Updates the recent activity list UI
         * @param {Array} activities - Array of recent activity items
         */
        function updateRecentActivity(activities) {
            var activityContainer = document.getElementById('recent-activity-' + nodeId);
            if (!activityContainer) return;

            if (!activities || activities.length === 0) {
                activityContainer.innerHTML =
                    '<div class="text-center text-muted py-4">' +
                    '<i class="fas fa-inbox fa-3x mb-3"></i>' +
                    '<p>No recent activity to display.</p>' +
                    '</div>';
                return;
            }

            var html = '<div class="list-group list-group-flush">';

            activities.forEach(function (activity) {
                var actionLower = (activity.action || 'unknown').toLowerCase();
                var badgeClass, iconClass;

                switch (actionLower) {
                    case 'approved':
                        badgeClass = 'badge-success';
                        iconClass = 'fa-check';
                        break;
                    case 'rejected':
                        badgeClass = 'badge-danger';
                        iconClass = 'fa-times';
                        break;
                    case 'escalated':
                        badgeClass = 'badge-warning';
                        iconClass = 'fa-arrow-up';
                        break;
                    default:
                        badgeClass = 'badge-secondary';
                        iconClass = 'fa-info';
                }

                var performedOn = new Date(activity.performed_on);
                var formattedDate = performedOn.toLocaleDateString('en-US', {
                    month: 'short',
                    day: 'numeric'
                }) + ', ' + performedOn.toLocaleTimeString('en-US', {
                    hour: '2-digit',
                    minute: '2-digit',
                    hour12: false
                });

                html +=
                    '<div class="list-group-item d-flex justify-content-between align-items-center px-0">' +
                    '<div>' +
                    '<span class="badge ' + badgeClass + ' mr-2">' +
                    '<i class="fas ' + iconClass + ' mr-1"></i>' + (activity.action || 'Unknown') +
                    '</span>' +
                    '<span class="font-weight-bold">' + (activity.performed_by || 'Unknown User') + '</span>' +
                    '<span class="text-muted ml-2">' + (activity.request_title || 'Approval Request') + '</span>' +
                    '</div>' +
                    '<small class="text-muted">' +
                    '<i class="far fa-clock mr-1"></i>' + formattedDate +
                    '</small>' +
                    '</div>';
            });

            html += '</div>';
            activityContainer.innerHTML = html;
        }

        /**
         * Starts the auto-refresh timer
         */
        function startAutoRefresh() {
            if (refreshTimer) {
                clearInterval(refreshTimer);
            }
            refreshTimer = setInterval(refreshMetrics, refreshInterval);
        }

        /**
         * Stops the auto-refresh timer
         */
        function stopAutoRefresh() {
            if (refreshTimer) {
                clearInterval(refreshTimer);
                refreshTimer = null;
            }
        }

        // Set up date range change handler
        var dateRangeSelector = document.getElementById('date-range-' + nodeId);
        if (dateRangeSelector) {
            dateRangeSelector.addEventListener('change', function () {
                refreshMetrics();
            });
        }

        // Start auto-refresh
        startAutoRefresh();

        // Handle page visibility changes (pause refresh when tab is hidden)
        document.addEventListener('visibilitychange', function () {
            if (document.hidden) {
                stopAutoRefresh();
            } else {
                refreshMetrics(); // Refresh immediately when tab becomes visible
                startAutoRefresh();
            }
        });

        // Clean up on page unload
        window.addEventListener('beforeunload', function () {
            stopAutoRefresh();
        });

        // Handle Page Builder events
        document.addEventListener('WvPbManager_Design_Loaded', function (event) {
            if (event && event.payload &&
                event.payload.component_name === 'WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard') {
                console.log('Approval Dashboard: Design mode loaded');
            }
        });

        document.addEventListener('WvPbManager_Design_Unloaded', function (event) {
            if (event && event.payload &&
                event.payload.component_name === 'WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard') {
                stopAutoRefresh();
                console.log('Approval Dashboard: Design mode unloaded');
            }
        });

        document.addEventListener('WvPbManager_Node_Moved', function (event) {
            if (event && event.payload &&
                event.payload.component_name === 'WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard') {
                console.log('Approval Dashboard: Component moved');
            }
        });
    }

    // Export initialization function to global scope
    window.PcApprovalDashboard = window.PcApprovalDashboard || {};
    window.PcApprovalDashboard.init = initDashboard;

    // Auto-initialize dashboards on DOM ready
    $(function () {
        // Find all dashboard containers and initialize them
        var dashboards = document.querySelectorAll('.approval-dashboard-container');
        dashboards.forEach(function (container) {
            var nodeId = container.getAttribute('data-node-id');
            var refreshInterval = parseInt(container.getAttribute('data-refresh-interval'), 10) || 60;

            if (nodeId) {
                initDashboard(nodeId, { refreshInterval: refreshInterval });
            }
        });
    });

})(window, jQuery);
