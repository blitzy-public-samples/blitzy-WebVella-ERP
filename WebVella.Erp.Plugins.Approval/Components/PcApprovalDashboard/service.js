/**
 * Approval Dashboard Client-Side Service
 * Handles auto-refresh, date range filtering, and DOM updates for dashboard metrics.
 * 
 * @requires jQuery
 * @requires toastr (for notifications)
 */
'use strict';

var ApprovalDashboard = (function() {
    
    // Store timer references keyed by node ID
    var timers = {};
    
    // Store configuration options keyed by node ID
    var configs = {};
    
    /**
     * Initialize the dashboard with auto-refresh capability.
     * @param {string} nodeId - The unique node identifier
     * @param {object} options - Configuration options
     * @param {number} options.refreshInterval - Auto-refresh interval in seconds
     * @param {string} options.dateRange - Default date range (7d, 30d, 90d)
     * @param {boolean} options.showOverdueAlert - Whether to show overdue alert styling
     */
    function init(nodeId, options) {
        // Store configuration
        configs[nodeId] = {
            refreshInterval: options.refreshInterval || 60,
            dateRange: options.dateRange || '30d',
            showOverdueAlert: options.showOverdueAlert !== false
        };
        
        // Start auto-refresh if interval > 0
        if (configs[nodeId].refreshInterval > 0) {
            startAutoRefresh(nodeId);
        }
        
        console.log('ApprovalDashboard initialized for node: ' + nodeId);
    }
    
    /**
     * Start the auto-refresh timer for a dashboard instance.
     * @param {string} nodeId - The node identifier
     */
    function startAutoRefresh(nodeId) {
        // Clear existing timer if any
        if (timers[nodeId]) {
            clearInterval(timers[nodeId]);
        }
        
        var config = configs[nodeId] || {};
        var interval = (config.refreshInterval || 60) * 1000;
        
        timers[nodeId] = setInterval(function() {
            refresh(nodeId);
        }, interval);
        
        console.log('ApprovalDashboard auto-refresh started (' + (interval/1000) + 's) for node: ' + nodeId);
    }
    
    /**
     * Stop the auto-refresh timer for a dashboard instance.
     * @param {string} nodeId - The node identifier
     */
    function stop(nodeId) {
        if (timers[nodeId]) {
            clearInterval(timers[nodeId]);
            delete timers[nodeId];
            console.log('ApprovalDashboard auto-refresh stopped for node: ' + nodeId);
        }
    }
    
    /**
     * Manually trigger a metrics refresh.
     * @param {string} nodeId - The node identifier
     */
    function refresh(nodeId) {
        var config = configs[nodeId] || {};
        var dateRange = config.dateRange || '30d';
        
        // Calculate date range
        var toDate = new Date();
        var fromDate = new Date();
        
        switch (dateRange) {
            case '7d':
                fromDate.setDate(fromDate.getDate() - 7);
                break;
            case '90d':
                fromDate.setDate(fromDate.getDate() - 90);
                break;
            case '30d':
            default:
                fromDate.setDate(fromDate.getDate() - 30);
                break;
        }
        
        // Show refresh indicator
        showRefreshIndicator(nodeId);
        
        // Make AJAX request to metrics endpoint
        $.ajax({
            url: '/api/v3.0/p/approval/dashboard/metrics',
            method: 'GET',
            data: {
                from: fromDate.toISOString(),
                to: toDate.toISOString()
            },
            success: function(response) {
                if (response.success) {
                    updateMetrics(nodeId, response.object);
                    hideRefreshIndicator(nodeId);
                } else {
                    console.error('ApprovalDashboard refresh failed: ' + response.message);
                    if (typeof toastr !== 'undefined') {
                        toastr.error(response.message, 'Dashboard Error');
                    }
                    hideRefreshIndicator(nodeId);
                }
            },
            error: function(xhr, status, error) {
                console.error('ApprovalDashboard AJAX error:', error);
                if (typeof toastr !== 'undefined') {
                    toastr.error('Failed to refresh dashboard metrics', 'Connection Error');
                }
                hideRefreshIndicator(nodeId);
            }
        });
    }
    
    /**
     * Change the date range filter and refresh metrics.
     * @param {string} nodeId - The node identifier
     * @param {string} range - New date range (7d, 30d, 90d)
     */
    function changeDateRange(nodeId, range) {
        var config = configs[nodeId];
        if (config) {
            config.dateRange = range;
        }
        
        // Trigger immediate refresh with new range
        refresh(nodeId);
    }
    
    /**
     * Update the DOM with new metrics values.
     * @param {string} nodeId - The node identifier
     * @param {object} metrics - Metrics data from API
     */
    function updateMetrics(nodeId, metrics) {
        if (!metrics) return;
        
        var config = configs[nodeId] || {};
        
        // Update Pending Approvals Count
        var pendingEl = document.getElementById('pending-count-' + nodeId);
        if (pendingEl) {
            pendingEl.textContent = metrics.pending_approvals_count || 0;
        }
        
        // Update Average Approval Time
        var avgTimeEl = document.getElementById('avg-time-' + nodeId);
        if (avgTimeEl) {
            var avgTime = metrics.average_approval_time_hours || 0;
            avgTimeEl.textContent = avgTime.toFixed(1) + 'h';
        }
        
        // Update Approval Rate
        var approvalRateEl = document.getElementById('approval-rate-' + nodeId);
        if (approvalRateEl) {
            var rate = metrics.approval_rate_percent || 0;
            approvalRateEl.textContent = rate.toFixed(1) + '%';
        }
        
        // Update Overdue Count with alert styling
        var overdueEl = document.getElementById('overdue-count-' + nodeId);
        if (overdueEl) {
            var overdueCount = metrics.overdue_requests_count || 0;
            var overdueHtml = overdueCount.toString();
            
            if (overdueCount > 0 && config.showOverdueAlert) {
                overdueHtml += ' <i class="fas fa-exclamation-triangle ms-2 fa-sm"></i>';
                overdueEl.classList.remove('text-warning');
                overdueEl.classList.add('text-danger');
            } else {
                overdueEl.classList.remove('text-danger');
                overdueEl.classList.add('text-warning');
            }
            overdueEl.innerHTML = overdueHtml;
        }
        
        // Update Recent Activity Table
        updateRecentActivity(nodeId, metrics.recent_activity || []);
        
        // Update last updated timestamp
        var lastUpdatedEl = document.getElementById('last-updated-' + nodeId);
        if (lastUpdatedEl) {
            var now = new Date();
            lastUpdatedEl.textContent = 'Updated ' + formatTime(now);
        }
    }
    
    /**
     * Update the recent activity table.
     * @param {string} nodeId - The node identifier
     * @param {array} activities - Array of recent activity items
     */
    function updateRecentActivity(nodeId, activities) {
        var tableEl = document.getElementById('recent-activity-' + nodeId);
        if (!tableEl) return;
        
        var tbody = tableEl.querySelector('tbody');
        if (!tbody) return;
        
        // Clear existing rows
        tbody.innerHTML = '';
        
        if (!activities || activities.length === 0) {
            var emptyRow = document.createElement('tr');
            emptyRow.innerHTML = '<td colspan="4" class="text-center text-muted py-3">' +
                '<i class="fas fa-inbox fa-2x mb-2"></i><br/>No recent activity</td>';
            tbody.appendChild(emptyRow);
            return;
        }
        
        activities.forEach(function(activity) {
            var row = document.createElement('tr');
            
            var actionClass = getActionClass(activity.action);
            var actionIcon = getActionIcon(activity.action);
            
            row.innerHTML = 
                '<td><span class="' + actionClass + '">' +
                    '<i class="fas ' + actionIcon + ' me-1"></i>' +
                    activity.action +
                '</span></td>' +
                '<td>' + escapeHtml(activity.performed_by || 'Unknown') + '</td>' +
                '<td>' + escapeHtml(activity.request_subject || 'Approval Request') + '</td>' +
                '<td><small class="text-muted">' + formatTimeAgo(activity.performed_on) + '</small></td>';
            
            tbody.appendChild(row);
        });
    }
    
    /**
     * Show the refresh indicator (spinning icon).
     * @param {string} nodeId - The node identifier
     */
    function showRefreshIndicator(nodeId) {
        var iconEl = document.getElementById('refresh-icon-' + nodeId);
        if (iconEl) {
            iconEl.classList.add('fa-spin');
        }
    }
    
    /**
     * Hide the refresh indicator.
     * @param {string} nodeId - The node identifier
     */
    function hideRefreshIndicator(nodeId) {
        var iconEl = document.getElementById('refresh-icon-' + nodeId);
        if (iconEl) {
            iconEl.classList.remove('fa-spin');
        }
    }
    
    /**
     * Get the CSS class for an action type.
     * @param {string} action - Action name
     * @returns {string} CSS class
     */
    function getActionClass(action) {
        switch ((action || '').toLowerCase()) {
            case 'approved': return 'text-success';
            case 'rejected': return 'text-danger';
            case 'delegated': return 'text-info';
            default: return 'text-secondary';
        }
    }
    
    /**
     * Get the FontAwesome icon class for an action type.
     * @param {string} action - Action name
     * @returns {string} Icon class
     */
    function getActionIcon(action) {
        switch ((action || '').toLowerCase()) {
            case 'approved': return 'fa-check';
            case 'rejected': return 'fa-times';
            case 'delegated': return 'fa-share';
            default: return 'fa-circle';
        }
    }
    
    /**
     * Format a Date object as HH:MM:SS.
     * @param {Date} date - Date to format
     * @returns {string} Formatted time string
     */
    function formatTime(date) {
        return date.toLocaleTimeString('en-US', {
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
            hour12: false
        });
    }
    
    /**
     * Format a date string as relative time (e.g., "2h ago").
     * @param {string} dateString - ISO date string
     * @returns {string} Relative time string
     */
    function formatTimeAgo(dateString) {
        if (!dateString) return 'unknown';
        
        var date = new Date(dateString);
        var now = new Date();
        var diffMs = now - date;
        var diffMins = Math.floor(diffMs / 60000);
        var diffHours = Math.floor(diffMs / 3600000);
        var diffDays = Math.floor(diffMs / 86400000);
        
        if (diffMins < 1) return 'just now';
        if (diffMins < 60) return diffMins + 'm ago';
        if (diffHours < 24) return diffHours + 'h ago';
        if (diffDays < 7) return diffDays + 'd ago';
        
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    }
    
    /**
     * Escape HTML to prevent XSS.
     * @param {string} text - Text to escape
     * @returns {string} Escaped text
     */
    function escapeHtml(text) {
        if (!text) return '';
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
    
    // Public API
    return {
        init: init,
        refresh: refresh,
        changeDateRange: changeDateRange,
        stop: stop
    };
    
})();
