"use strict";
/**
 * PcApprovalDashboard Client-Side Service
 * 
 * Handles auto-refresh, date range filtering, and AJAX metric loading
 * for the Manager Approval Dashboard component.
 */
(function (window, $) {

    /// Dashboard service implementation
    ///////////////////////////////////////////////////////////////////////////////////

    // Store refresh timers by node ID to support multiple dashboard instances
    var refreshTimers = {};

    /**
     * Initializes the dashboard for a given node.
     * @param {string} nodeId - The unique identifier of the dashboard node
     * @param {Object} config - Configuration object with refreshInterval, dateRange, activityCount
     */
    window.initApprovalDashboard = function (nodeId, config) {
        config = config || {};
        var refreshInterval = config.refreshInterval || 60;
        var dateRange = config.dateRange || 30;
        var activityCount = config.activityCount || 5;

        // Set up date range change handler
        var dateRangeSelect = document.getElementById('dateRange-' + nodeId);
        if (dateRangeSelect) {
            dateRangeSelect.addEventListener('change', function () {
                dateRange = parseInt(this.value) || 30;
                window.refreshDashboardMetrics(nodeId, dateRange, activityCount);
            });
        }

        // Set up auto-refresh if enabled
        if (refreshInterval > 0) {
            // Clear any existing timer for this node
            if (refreshTimers[nodeId]) {
                clearInterval(refreshTimers[nodeId]);
            }

            // Start new refresh timer
            refreshTimers[nodeId] = setInterval(function () {
                window.refreshDashboardMetrics(nodeId, dateRange, activityCount);
            }, refreshInterval * 1000);
        }
    };

    /**
     * Refreshes dashboard metrics via AJAX call.
     * @param {string} nodeId - The unique identifier of the dashboard node
     * @param {number} dateRange - Number of days for date range calculation
     * @param {number} activityCount - Number of activities to fetch
     */
    window.refreshDashboardMetrics = function (nodeId, dateRange, activityCount) {
        var refreshIcon = document.getElementById('refresh-icon-' + nodeId);

        // Show refresh animation
        if (refreshIcon) {
            refreshIcon.classList.add('refreshing');
        }

        $.ajax({
            url: '/api/v3.0/p/approval/dashboard/metrics',
            type: 'GET',
            data: {
                days: dateRange || 30,
                activityCount: activityCount || 5
            },
            success: function (response) {
                if (response.success && response.object) {
                    updateDashboardUI(nodeId, response.object);
                } else {
                    showDashboardError(response.message || 'Failed to refresh metrics');
                }
            },
            error: function (xhr, status, error) {
                handleDashboardError(xhr, status, error);
            },
            complete: function () {
                // Stop refresh animation
                if (refreshIcon) {
                    refreshIcon.classList.remove('refreshing');
                }
            }
        });
    };

    /**
     * Updates the dashboard UI with new metrics data.
     * @param {string} nodeId - The unique identifier of the dashboard node
     * @param {Object} metrics - Metrics object from API response
     */
    function updateDashboardUI(nodeId, metrics) {
        // Update pending count
        var pendingEl = document.getElementById('pending-count-' + nodeId);
        if (pendingEl) {
            pendingEl.textContent = metrics.pending_approvals_count || 0;
        }

        // Update average time
        var avgTimeEl = document.getElementById('avg-time-' + nodeId);
        if (avgTimeEl) {
            var avgTime = metrics.average_approval_time_hours;
            avgTimeEl.textContent = avgTime < 0 ? 'N/A' : avgTime.toFixed(1) + ' hrs';
        }

        // Update approval rate
        var rateEl = document.getElementById('approval-rate-' + nodeId);
        if (rateEl) {
            var rate = metrics.approval_rate_percent || 0;
            rateEl.textContent = rate.toFixed(1) + '%';

            // Update color based on rate
            rateEl.className = 'mb-1';
            if (rate >= 80) {
                rateEl.classList.add('text-success');
            } else if (rate >= 50) {
                rateEl.classList.add('text-warning');
            } else {
                rateEl.classList.add('text-danger');
            }
        }

        // Update overdue count
        var overdueEl = document.getElementById('overdue-count-' + nodeId);
        if (overdueEl) {
            var overdue = metrics.overdue_requests_count || 0;
            overdueEl.textContent = overdue;

            // Update color based on count
            overdueEl.className = 'mb-1';
            if (overdue > 0) {
                overdueEl.classList.add('text-danger');
            } else {
                overdueEl.classList.add('text-success');
            }
        }

        // Update last updated timestamp
        var lastUpdatedEl = document.getElementById('last-updated-' + nodeId);
        if (lastUpdatedEl) {
            var now = new Date();
            lastUpdatedEl.textContent = 'Last updated: ' + now.toUTCString();
        }

        // Update activity feed
        updateActivityFeed(nodeId, metrics.recent_activity || []);
    }

    /**
     * Updates the activity feed section with new activities.
     * @param {string} nodeId - The unique identifier of the dashboard node
     * @param {Array} activities - Array of activity objects
     */
    function updateActivityFeed(nodeId, activities) {
        var feedEl = document.getElementById('activity-feed-' + nodeId);
        if (!feedEl) return;

        if (!activities || activities.length === 0) {
            feedEl.innerHTML = '<div class="text-center text-muted py-4">' +
                '<i class="fas fa-inbox fa-2x mb-2"></i>' +
                '<p class="mb-0">No recent activity</p></div>';
            return;
        }

        var html = '<ul class="list-group list-group-flush">';

        activities.forEach(function (activity) {
            var iconClass = getActivityIconClass(activity.action_type);

            html += '<li class="list-group-item d-flex justify-content-between align-items-center">';
            html += '<div>';
            html += '<i class="' + iconClass + ' mr-2"></i>';
            html += '<span class="text-capitalize">' + escapeHtml(activity.action_type || '') + '</span> ';
            html += 'by <strong>' + escapeHtml(activity.performed_by_name || 'Unknown') + '</strong>';

            if (activity.comments) {
                html += '<span class="text-muted"> - "' + escapeHtml(activity.comments) + '"</span>';
            }

            html += '</div>';
            html += '<small class="text-muted">' + escapeHtml(activity.relative_time || '') + '</small>';
            html += '</li>';
        });

        html += '</ul>';
        feedEl.innerHTML = html;
    }

    /**
     * Gets the appropriate icon class for an action type.
     * @param {string} actionType - The type of action
     * @returns {string} Font Awesome icon class string
     */
    function getActivityIconClass(actionType) {
        switch (actionType) {
            case 'approved':
                return 'fas fa-check text-success';
            case 'rejected':
                return 'fas fa-times text-danger';
            case 'delegated':
                return 'fas fa-share text-info';
            case 'escalated':
                return 'fas fa-arrow-up text-warning';
            case 'submitted':
                return 'fas fa-paper-plane text-primary';
            case 'cancelled':
                return 'fas fa-ban text-secondary';
            default:
                return 'fas fa-circle text-muted';
        }
    }

    /**
     * Displays an error toast notification.
     * @param {string} message - Error message to display
     */
    function showDashboardError(message) {
        if (typeof toastr !== 'undefined') {
            toastr.error(message);
        } else {
            console.error('Approval Dashboard Error:', message);
        }
    }

    /**
     * Handles AJAX error responses.
     * @param {Object} xhr - XMLHttpRequest object
     * @param {string} status - Status string
     * @param {string} error - Error message
     */
    function handleDashboardError(xhr, status, error) {
        var message = 'Failed to refresh dashboard metrics';

        if (xhr.status === 401) {
            message = 'Session expired. Please refresh the page.';
        } else if (xhr.status === 403) {
            message = 'Access denied. Manager role required.';
        } else if (xhr.responseJSON && xhr.responseJSON.message) {
            message = xhr.responseJSON.message;
        }

        showDashboardError(message);
    }

    /**
     * Escapes HTML special characters to prevent XSS.
     * @param {string} text - Text to escape
     * @returns {string} Escaped text
     */
    function escapeHtml(text) {
        if (!text) return '';
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Cleans up the dashboard for a given node (clears timers, etc).
     * @param {string} nodeId - The unique identifier of the dashboard node
     */
    window.destroyApprovalDashboard = function (nodeId) {
        if (refreshTimers[nodeId]) {
            clearInterval(refreshTimers[nodeId]);
            delete refreshTimers[nodeId];
        }
    };

    // Page builder event handlers
    $(function () {

        // Handle component design mode loaded
        document.addEventListener("WvPbManager_Design_Loaded", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard") {
                console.log("PcApprovalDashboard Design mode loaded");
            }
        });

        // Handle component design mode unloaded
        document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard") {
                console.log("PcApprovalDashboard Design mode unloaded");
            }
        });

        // Handle component options loaded
        document.addEventListener("WvPbManager_Options_Loaded", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard") {
                console.log("PcApprovalDashboard Options panel loaded");
            }
        });

        // Handle component options unloaded
        document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard") {
                console.log("PcApprovalDashboard Options panel unloaded");
            }
        });

        // Handle component node moved
        document.addEventListener("WvPbManager_Node_Moved", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard") {
                console.log("PcApprovalDashboard node moved");
            }
        });

    });

    // Cleanup on page unload
    window.addEventListener('beforeunload', function () {
        for (var nodeId in refreshTimers) {
            if (refreshTimers.hasOwnProperty(nodeId)) {
                clearInterval(refreshTimers[nodeId]);
            }
        }
        refreshTimers = {};
    });

    //////////////////////////////////////////////////////////////////////////////////
    /// End of dashboard service

})(window, jQuery);
