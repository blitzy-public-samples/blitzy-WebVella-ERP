"use strict";
(function (window, $) {

    /// Auto-refresh dashboard functionality
    ///////////////////////////////////////////////////////////////////////////////////

    // Store active refresh intervals by container ID
    var activeRefreshIntervals = {};

    /**
     * Initializes the approval dashboard with auto-refresh capability.
     * @param {string} containerId - The ID of the dashboard container element.
     * @param {number} refreshIntervalSeconds - The refresh interval in seconds (0 to disable).
     */
    function initApprovalDashboard(containerId, refreshIntervalSeconds) {
        var container = document.getElementById(containerId);
        if (!container) {
            console.warn('Approval Dashboard container not found:', containerId);
            return;
        }

        // Initial load
        loadDashboardMetrics(containerId);

        // Setup auto-refresh if interval is greater than 0
        if (refreshIntervalSeconds > 0) {
            var intervalMs = refreshIntervalSeconds * 1000;

            // Clear any existing interval for this container
            if (activeRefreshIntervals[containerId]) {
                clearInterval(activeRefreshIntervals[containerId]);
            }

            // Setup new interval
            activeRefreshIntervals[containerId] = setInterval(function () {
                loadDashboardMetrics(containerId);
            }, intervalMs);
        }
    }

    /**
     * Loads dashboard metrics via AJAX call.
     * @param {string} containerId - The ID of the dashboard container to update.
     */
    function loadDashboardMetrics(containerId) {
        var container = document.getElementById(containerId);
        if (!container) return;

        $.ajax({
            url: '/api/v3.0/p/approval/dashboard/metrics',
            type: 'GET',
            dataType: 'json',
            success: function (data) {
                if (data && data.success && data.object) {
                    updateDashboardDisplay(containerId, data.object);
                }
            },
            error: function (xhr, status, error) {
                console.error('Error loading dashboard metrics:', error);
                if (typeof toastr !== 'undefined') {
                    toastr.error('Failed to load dashboard metrics');
                }
            }
        });
    }

    /**
     * Updates the dashboard display with new metrics.
     * @param {string} containerId - The ID of the dashboard container.
     * @param {object} metrics - The metrics object containing KPIs.
     */
    function updateDashboardDisplay(containerId, metrics) {
        var container = document.getElementById(containerId);
        if (!container) return;

        // Update pending count
        var pendingEl = container.querySelector('.pending-count');
        if (pendingEl) {
            pendingEl.textContent = metrics.pendingCount || 0;
            pendingEl.className = 'pending-count' + (metrics.pendingCount > 10 ? ' go-orange' : '');
        }

        // Update average time
        var avgTimeEl = container.querySelector('.avg-time');
        if (avgTimeEl) {
            avgTimeEl.textContent = (metrics.averageApprovalTimeHours || 0).toFixed(1);
        }

        // Update approval rate
        var approvalRateEl = container.querySelector('.approval-rate');
        if (approvalRateEl) {
            var rate = metrics.approvalRate || 0;
            approvalRateEl.textContent = rate.toFixed(1);
            var rateParent = approvalRateEl.closest('h3');
            if (rateParent) {
                rateParent.className = 'mb-1 ' + (rate >= 80 ? 'go-green' : rate >= 50 ? 'go-orange' : 'go-red');
            }
        }

        // Update overdue count
        var overdueEl = container.querySelector('.overdue-count');
        if (overdueEl) {
            overdueEl.textContent = metrics.overdueCount || 0;
            var overdueParent = overdueEl.closest('h3');
            if (overdueParent) {
                overdueParent.className = 'mb-1 ' + (metrics.overdueCount > 0 ? 'go-red' : 'go-gray');
            }
        }

        // Update recent activity
        var recentEl = container.querySelector('.recent-activity');
        if (recentEl) {
            recentEl.textContent = metrics.recentActivityCount || 0;
        }

        // Update last updated timestamp
        var timestampEl = document.getElementById(containerId + '-last-updated');
        if (timestampEl) {
            var now = new Date();
            var timeStr = now.getUTCHours().toString().padStart(2, '0') + ':' +
                now.getUTCMinutes().toString().padStart(2, '0') + ':' +
                now.getUTCSeconds().toString().padStart(2, '0');
            timestampEl.textContent = 'Last updated: ' + timeStr + ' UTC';
        }
    }

    // Expose initApprovalDashboard globally
    window.initApprovalDashboard = initApprovalDashboard;

    $(function () {

        //	document.addEventListener("WvPbManager_Design_Loaded", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard"){
        //			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard Design loaded");
        //		}
        //	});

        //	document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard"){
        //			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard Design unloaded");
        //		}
        //	});

        //	document.addEventListener("WvPbManager_Options_Loaded", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard") {
        //		}
        //	});

        //	document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard"){
        //			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard Options unloaded");
        //		}
        //	});

        document.addEventListener("WvPbManager_Node_Moved", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard") {
                console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard Moved");
            }
        });
    });

    //////////////////////////////////////////////////////////////////////////////////
    /// Your code is above

})(window, jQuery);
