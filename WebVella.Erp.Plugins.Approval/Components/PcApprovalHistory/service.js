"use strict";
/**
 * service.js - Client-side JavaScript for PcApprovalHistory component
 * 
 * Contains IIFE with jQuery-based event handlers for page-builder lifecycle hooks
 * (design loaded/unloaded, options loaded/unloaded). Provides DOM-ready handlers
 * and AJAX utilities for the approval history timeline component.
 * 
 * Part of the WebVella ERP Approval Plugin (STORY-008)
 */
(function (window, $) {

    /// Your code goes below
    ///////////////////////////////////////////////////////////////////////////////////

    /**
     * Component name constant for event filtering
     */
    var COMPONENT_NAME = "WebVella.Erp.Plugins.Approval.Components.PcApprovalHistory";

    /**
     * API endpoint base path for approval history operations
     */
    var API_BASE_PATH = "/api/v3.0/p/approval";

    /**
     * Refreshes the approval history timeline for a given request
     * @param {string} requestId - The approval request ID
     * @param {string} containerId - The DOM container ID to update
     * @param {function} successCallback - Optional callback on success
     * @param {function} errorCallback - Optional callback on error
     */
    function refreshHistoryTimeline(requestId, containerId, successCallback, errorCallback) {
        if (!requestId) {
            console.error("PcApprovalHistory: requestId is required for refreshHistoryTimeline");
            return;
        }

        var $container = containerId ? $("#" + containerId) : $(".approval-history-timeline");
        if ($container.length === 0) {
            console.warn("PcApprovalHistory: Timeline container not found");
            return;
        }

        // Show loading state
        $container.addClass("loading");

        $.ajax({
            url: API_BASE_PATH + "/requests/" + requestId + "/history",
            type: "GET",
            dataType: "json",
            success: function (response) {
                $container.removeClass("loading");
                if (response && response.success) {
                    renderHistoryTimeline($container, response.object);
                    if (typeof successCallback === "function") {
                        successCallback(response);
                    }
                } else {
                    var errorMessage = response && response.message ? response.message : "Failed to load history";
                    showNotification(errorMessage, "error");
                    if (typeof errorCallback === "function") {
                        errorCallback(response);
                    }
                }
            },
            error: function (xhr, status, error) {
                $container.removeClass("loading");
                console.error("PcApprovalHistory: AJAX error - " + error);
                showNotification("Error loading approval history", "error");
                if (typeof errorCallback === "function") {
                    errorCallback({ success: false, message: error });
                }
            }
        });
    }

    /**
     * Renders the history timeline from data
     * @param {jQuery} $container - The jQuery container element
     * @param {Array} historyItems - Array of history items to render
     */
    function renderHistoryTimeline($container, historyItems) {
        if (!historyItems || historyItems.length === 0) {
            $container.html('<div class="no-history-items">No approval history found</div>');
            return;
        }

        var html = '<div class="timeline">';
        for (var i = 0; i < historyItems.length; i++) {
            var item = historyItems[i];
            html += renderHistoryItem(item);
        }
        html += '</div>';

        $container.html(html);
    }

    /**
     * Renders a single history item for the timeline
     * @param {Object} item - The history item object
     * @returns {string} HTML string for the history item
     */
    function renderHistoryItem(item) {
        var actionClass = getActionClass(item.action);
        var formattedDate = formatDateTime(item.performed_on);
        var performerName = item.performed_by_name || "System";

        var html = '<div class="timeline-item ' + actionClass + '">';
        html += '<div class="timeline-marker"></div>';
        html += '<div class="timeline-content">';
        html += '<div class="timeline-header">';
        html += '<span class="timeline-action">' + escapeHtml(item.action) + '</span>';
        html += '<span class="timeline-date">' + formattedDate + '</span>';
        html += '</div>';
        html += '<div class="timeline-body">';
        html += '<span class="timeline-performer">' + escapeHtml(performerName) + '</span>';
        if (item.step_name) {
            html += ' at <span class="timeline-step">' + escapeHtml(item.step_name) + '</span>';
        }
        if (item.comments) {
            html += '<div class="timeline-comments">' + escapeHtml(item.comments) + '</div>';
        }
        html += '</div>';
        html += '</div>';
        html += '</div>';

        return html;
    }

    /**
     * Gets CSS class based on action type
     * @param {string} action - The action type
     * @returns {string} CSS class name
     */
    function getActionClass(action) {
        if (!action) return "";
        var actionLower = action.toLowerCase();
        switch (actionLower) {
            case "approve":
            case "approved":
                return "action-approved";
            case "reject":
            case "rejected":
                return "action-rejected";
            case "delegate":
            case "delegated":
                return "action-delegated";
            case "escalate":
            case "escalated":
                return "action-escalated";
            case "comment":
                return "action-comment";
            case "submit":
            case "submitted":
                return "action-submitted";
            default:
                return "action-default";
        }
    }

    /**
     * Formats a date/time value for display
     * @param {string} dateValue - ISO date string
     * @returns {string} Formatted date string
     */
    function formatDateTime(dateValue) {
        if (!dateValue) return "";
        try {
            var date = new Date(dateValue);
            return date.toLocaleDateString() + " " + date.toLocaleTimeString();
        } catch (e) {
            return dateValue;
        }
    }

    /**
     * Escapes HTML special characters to prevent XSS
     * @param {string} text - Text to escape
     * @returns {string} Escaped text
     */
    function escapeHtml(text) {
        if (!text) return "";
        var div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Shows a notification to the user
     * @param {string} message - Message to display
     * @param {string} type - Notification type (success, error, warning, info)
     */
    function showNotification(message, type) {
        // Use toastr if available (WebVella standard)
        if (typeof toastr !== "undefined") {
            switch (type) {
                case "success":
                    toastr.success(message);
                    break;
                case "error":
                    toastr.error(message);
                    break;
                case "warning":
                    toastr.warning(message);
                    break;
                default:
                    toastr.info(message);
            }
        } else {
            console.log("PcApprovalHistory [" + type + "]: " + message);
        }
    }

    /**
     * Initializes event handlers for history timeline interactions
     */
    function initializeTimelineHandlers() {
        // Delegate click handlers for timeline expand/collapse
        $(document).on("click", ".approval-history-timeline .timeline-item", function (e) {
            var $item = $(this);
            // Toggle expanded state for items with long comments
            if ($item.find(".timeline-comments").length > 0) {
                $item.toggleClass("expanded");
            }
        });

        // Handle refresh button clicks
        $(document).on("click", ".approval-history-refresh", function (e) {
            e.preventDefault();
            var $button = $(this);
            var requestId = $button.data("request-id");
            var containerId = $button.data("container-id");
            if (requestId) {
                refreshHistoryTimeline(requestId, containerId);
            }
        });
    }

    /**
     * DOM ready handler - initializes the component
     */
    $(function () {
        // Initialize timeline interaction handlers
        initializeTimelineHandlers();

        // Page-builder event listeners (commented as templates)
        // Uncomment and customize as needed for design-time behavior

        //document.addEventListener("WvPbManager_Design_Loaded", function (event) {
        //    if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
        //        console.log(COMPONENT_NAME + " Design loaded");
        //    }
        //});

        //document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
        //    if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
        //        console.log(COMPONENT_NAME + " Design unloaded");
        //    }
        //});

        //document.addEventListener("WvPbManager_Options_Loaded", function (event) {
        //    if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
        //        console.log(COMPONENT_NAME + " Options loaded");
        //    }
        //});

        //document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
        //    if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
        //        console.log(COMPONENT_NAME + " Options unloaded");
        //    }
        //});

        //document.addEventListener("WvPbManager_Node_Moved", function (event) {
        //    if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
        //        console.log(COMPONENT_NAME + " Node moved");
        //    }
        //});
    });

    // Expose public API for external use
    window.PcApprovalHistory = {
        refreshTimeline: refreshHistoryTimeline,
        showNotification: showNotification
    };

    //////////////////////////////////////////////////////////////////////////////////
    /// Your code is above

})(window, jQuery);
