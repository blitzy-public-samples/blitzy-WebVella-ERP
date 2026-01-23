"use strict";
/**
 * PcApprovalDashboard Client-Side Service
 * 
 * Provides auto-refresh capability for the Approval Dashboard component.
 * Makes AJAX calls to /api/v3.0/p/approval/dashboard/metrics endpoint
 * to fetch real-time KPI data for the approval workflow system.
 * 
 * Features:
 * - Configurable auto-refresh interval (default 60 seconds)
 * - Real-time metrics display updates
 * - Visual warning indicators for overdue/critical items
 * - WebVella Page Builder lifecycle event integration
 * - Error handling with toastr notifications
 */
(function (window, $) {

    /// Approval Dashboard Auto-refresh Implementation
    ///////////////////////////////////////////////////////////////////////////////////

    /**
     * Store active refresh intervals by container ID.
     * This allows multiple dashboard instances on the same page
     * and proper cleanup when components are destroyed.
     */
    var activeRefreshIntervals = {};

    /**
     * Default refresh interval in milliseconds (60 seconds).
     * Can be overridden via the initApprovalDashboard function parameter.
     */
    var DEFAULT_REFRESH_INTERVAL = 60000;

    /**
     * Initializes the approval dashboard with auto-refresh capability.
     * This function should be called from the Display.cshtml view
     * after the component DOM is rendered.
     * 
     * @param {string} containerId - The ID of the dashboard container element.
     * @param {number} [refreshInterval] - The refresh interval in milliseconds (default 60000ms).
     *                                     Set to 0 or negative to disable auto-refresh.
     */
    function initApprovalDashboard(containerId, refreshInterval) {
        // Validate and get container element reference
        var container = document.getElementById(containerId);
        if (!container) {
            console.warn('[ApprovalDashboard] Container not found:', containerId);
            return;
        }

        // Use default refresh interval if not specified or invalid
        var interval = (typeof refreshInterval === 'number' && refreshInterval > 0)
            ? refreshInterval
            : DEFAULT_REFRESH_INTERVAL;

        // Perform initial metrics load immediately
        loadDashboardMetrics(containerId);

        // Setup auto-refresh with setInterval if interval is positive
        if (interval > 0) {
            // Clear any existing interval for this container to prevent duplicates
            if (activeRefreshIntervals[containerId]) {
                clearInterval(activeRefreshIntervals[containerId]);
                delete activeRefreshIntervals[containerId];
            }

            // Setup new interval and store reference for cleanup
            activeRefreshIntervals[containerId] = setInterval(function () {
                loadDashboardMetrics(containerId);
            }, interval);

            // Log for debugging
            console.log('[ApprovalDashboard] Auto-refresh enabled for', containerId, 'every', interval, 'ms');
        }
    }

    /**
     * Stops the auto-refresh for a specific dashboard container.
     * Should be called when the component is destroyed or hidden.
     * 
     * @param {string} containerId - The ID of the dashboard container element.
     */
    function stopApprovalDashboardRefresh(containerId) {
        if (activeRefreshIntervals[containerId]) {
            clearInterval(activeRefreshIntervals[containerId]);
            delete activeRefreshIntervals[containerId];
            console.log('[ApprovalDashboard] Auto-refresh stopped for', containerId);
        }
    }

    /**
     * Loads dashboard metrics via AJAX call to the REST API endpoint.
     * On success, updates the dashboard display with the retrieved metrics.
     * On error, displays a toastr error notification to the user.
     * 
     * @param {string} containerId - The ID of the dashboard container to update.
     */
    function loadDashboardMetrics(containerId) {
        var container = document.getElementById(containerId);
        if (!container) {
            console.warn('[ApprovalDashboard] Container not found during metrics load:', containerId);
            return;
        }

        // Add loading indicator class
        $(container).addClass('loading');

        $.ajax({
            url: '/api/v3.0/p/approval/dashboard/metrics',
            type: 'GET',
            dataType: 'json',
            cache: false,
            success: function (data) {
                // Remove loading indicator
                $(container).removeClass('loading');

                if (data && data.success && data.object) {
                    updateDashboardDisplay(containerId, data.object);
                } else if (data && !data.success && data.message) {
                    // API returned an error response
                    console.error('[ApprovalDashboard] API error:', data.message);
                    if (typeof toastr !== 'undefined') {
                        toastr.error(data.message || 'Failed to load dashboard metrics');
                    }
                }
            },
            error: function (xhr, status, error) {
                // Remove loading indicator
                $(container).removeClass('loading');

                console.error('[ApprovalDashboard] AJAX error loading metrics:', status, error);
                if (typeof toastr !== 'undefined') {
                    toastr.error('Failed to load dashboard metrics');
                }
            }
        });
    }

    /**
     * Updates the dashboard display with new metrics data.
     * Updates all KPI elements with their corresponding values and applies
     * conditional CSS classes for visual warning states.
     * 
     * @param {string} containerId - The ID of the dashboard container.
     * @param {object} metrics - The metrics object containing KPIs:
     *   - pendingCount: Number of pending approval requests
     *   - averageApprovalTimeHours: Average time to complete approvals in hours
     *   - approvalRate: Percentage of approved vs total completed requests
     *   - overdueCount: Number of overdue/expired approval requests
     *   - recentActivityCount: Number of recent activities (last 24h)
     */
    function updateDashboardDisplay(containerId, metrics) {
        var container = document.getElementById(containerId);
        if (!container) {
            console.warn('[ApprovalDashboard] Container not found during display update:', containerId);
            return;
        }

        // Safely get metric values with defaults
        var pendingCount = metrics.pendingCount || 0;
        var avgTime = metrics.averageApprovalTimeHours || 0;
        var approvalRate = metrics.approvalRate || 0;
        var overdueCount = metrics.overdueCount || 0;
        var recentActivityCount = metrics.recentActivityCount || 0;

        // Update pending count element
        // Shows the number of approval requests currently awaiting action
        var pendingEl = container.querySelector('.pending-count');
        if (pendingEl) {
            pendingEl.textContent = pendingCount;
            // Apply warning class if pending count is high
            if (pendingCount > 20) {
                pendingEl.parentElement && $(pendingEl.parentElement).addClass('go-red').removeClass('go-orange');
            } else if (pendingCount > 10) {
                pendingEl.parentElement && $(pendingEl.parentElement).addClass('go-orange').removeClass('go-red');
            } else {
                pendingEl.parentElement && $(pendingEl.parentElement).removeClass('go-red go-orange');
            }
        }

        // Update average time element with ' hrs' suffix
        // Shows the average processing time for completed approvals
        var avgTimeEl = container.querySelector('.avg-time');
        if (avgTimeEl) {
            avgTimeEl.textContent = avgTime.toFixed(1) + ' hrs';
            // Apply warning if average time is too long
            if (avgTime > 48) {
                avgTimeEl.parentElement && $(avgTimeEl.parentElement).addClass('go-red').removeClass('go-orange');
            } else if (avgTime > 24) {
                avgTimeEl.parentElement && $(avgTimeEl.parentElement).addClass('go-orange').removeClass('go-red');
            } else {
                avgTimeEl.parentElement && $(avgTimeEl.parentElement).removeClass('go-red go-orange');
            }
        }

        // Update approval rate element with '%' suffix
        // Shows the percentage of requests that were approved vs rejected
        var approvalRateEl = container.querySelector('.approval-rate');
        if (approvalRateEl) {
            approvalRateEl.textContent = approvalRate.toFixed(1) + '%';
            // Apply color classes based on rate thresholds
            var rateParent = approvalRateEl.closest('.metric-card') || approvalRateEl.parentElement;
            if (rateParent) {
                $(rateParent).removeClass('go-green go-orange go-red');
                if (approvalRate >= 80) {
                    $(rateParent).addClass('go-green');
                } else if (approvalRate >= 50) {
                    $(rateParent).addClass('go-orange');
                } else {
                    $(rateParent).addClass('go-red');
                }
            }
        }

        // Update overdue count element
        // Shows the number of approval requests that have exceeded their timeout
        var overdueEl = container.querySelector('.overdue-count');
        if (overdueEl) {
            overdueEl.textContent = overdueCount;
            // Apply go-red class if there are any overdue items (critical warning)
            var overdueParent = overdueEl.closest('.metric-card') || overdueEl.parentElement;
            if (overdueParent) {
                $(overdueParent).removeClass('go-red go-gray');
                if (overdueCount > 0) {
                    $(overdueParent).addClass('go-red');
                } else {
                    $(overdueParent).addClass('go-gray');
                }
            }
        }

        // Update recent activity count element
        // Shows the number of approval actions taken in the last 24 hours
        var recentEl = container.querySelector('.recent-activity');
        if (recentEl) {
            recentEl.textContent = recentActivityCount;
        }

        // Update last updated timestamp display
        // Shows when the metrics were last refreshed
        var timestampEl = document.getElementById(containerId + '-last-updated');
        if (timestampEl) {
            var now = new Date();
            var hours = now.getHours().toString().padStart(2, '0');
            var minutes = now.getMinutes().toString().padStart(2, '0');
            var seconds = now.getSeconds().toString().padStart(2, '0');
            timestampEl.textContent = 'Last updated: ' + hours + ':' + minutes + ':' + seconds;
        }

        // Trigger custom event for external listeners
        $(container).trigger('approvalDashboardUpdated', [metrics]);
    }

    /**
     * Manually triggers a refresh of the dashboard metrics.
     * Can be called from other components or user actions.
     * 
     * @param {string} containerId - The ID of the dashboard container to refresh.
     */
    function refreshApprovalDashboard(containerId) {
        loadDashboardMetrics(containerId);
    }

    // Expose functions globally for external access
    window.initApprovalDashboard = initApprovalDashboard;
    window.stopApprovalDashboardRefresh = stopApprovalDashboardRefresh;
    window.refreshApprovalDashboard = refreshApprovalDashboard;

    /**
     * DOM Ready Handler
     * Sets up WebVella Page Builder lifecycle event listeners.
     */
    $(function () {

        // WvPbManager_Design_Loaded: Fired when a component enters design mode in the page builder.
        // Use this to initialize any design-time specific functionality.
        //	document.addEventListener("WvPbManager_Design_Loaded", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard"){
        //			console.log("[ApprovalDashboard] Design mode loaded");
        //		}
        //	});

        // WvPbManager_Design_Unloaded: Fired when a component exits design mode.
        // Use this to clean up any design-time resources.
        //	document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard"){
        //			console.log("[ApprovalDashboard] Design mode unloaded");
        //		}
        //	});

        // WvPbManager_Options_Loaded: Fired when the component options panel is displayed.
        // Use this to set up any options-specific UI behavior.
        //	document.addEventListener("WvPbManager_Options_Loaded", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard") {
        //			console.log("[ApprovalDashboard] Options panel loaded");
        //		}
        //	});

        // WvPbManager_Options_Unloaded: Fired when the component options panel is closed.
        // Use this to save any pending changes or clean up resources.
        //	document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard"){
        //			console.log("[ApprovalDashboard] Options panel unloaded");
        //		}
        //	});

        // WvPbManager_Node_Moved: Fired when a component is moved within the page builder.
        // Handle any re-initialization needed after the component moves.
        document.addEventListener("WvPbManager_Node_Moved", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalDashboard") {
                console.log("[ApprovalDashboard] Component moved in page builder");
            }
        });

    });

    //////////////////////////////////////////////////////////////////////////////////
    /// Your code is above

})(window, jQuery);
