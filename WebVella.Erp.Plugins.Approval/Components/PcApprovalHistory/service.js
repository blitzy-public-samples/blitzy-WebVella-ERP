"use strict";

/**
 * PcApprovalHistory Component Service
 * 
 * Client-side JavaScript for the Approval History component providing:
 * - Timeline rendering for approval audit trail
 * - AJAX loading of history data from REST API
 * - Page builder lifecycle integration
 * 
 * @module PcApprovalHistory/service
 * @requires jQuery
 */
(function (window, $) {

	///////////////////////////////////////////////////////////////////////////////////
	/// Approval History Component Client-Side Logic
	///////////////////////////////////////////////////////////////////////////////////

	// Component configuration constants
	var COMPONENT_NAME = "WebVella.Erp.Plugins.Approval.Components.PcApprovalHistory";
	var API_BASE_URL = "/api/v3.0/p/approval/request";

	// Action type to icon mapping for timeline rendering
	var ACTION_ICONS = {
		"submitted": "fa-paper-plane",
		"approved": "fa-check",
		"rejected": "fa-times",
		"delegated": "fa-share",
		"escalated": "fa-arrow-up"
	};

	// Action type to color class mapping for timeline styling
	var ACTION_COLORS = {
		"submitted": "text-primary",
		"approved": "text-success",
		"rejected": "text-danger",
		"delegated": "text-info",
		"escalated": "text-warning"
	};

	// Action type to background color mapping for timeline dots
	var ACTION_BG_COLORS = {
		"submitted": "bg-primary",
		"approved": "bg-success",
		"rejected": "bg-danger",
		"delegated": "bg-info",
		"escalated": "bg-warning"
	};

	/**
	 * Loads approval history data from the REST API
	 * 
	 * @param {string} requestId - The GUID of the approval request
	 * @param {function} callback - Optional callback function(success, data/error)
	 * @returns {jQuery.Promise} AJAX promise object
	 */
	function loadApprovalHistory(requestId, callback) {
		if (!requestId) {
			var error = "Request ID is required to load approval history";
			console.error("[PcApprovalHistory] " + error);
			if (typeof callback === "function") {
				callback(false, error);
			}
			return $.Deferred().reject(error).promise();
		}

		var url = API_BASE_URL + "/" + encodeURIComponent(requestId) + "/history";

		return $.ajax({
			url: url,
			type: "GET",
			dataType: "json",
			contentType: "application/json"
		})
		.done(function (response) {
			if (response && response.success) {
				if (typeof callback === "function") {
					callback(true, response.object || []);
				}
			} else {
				var errorMsg = (response && response.message) ? response.message : "Failed to load approval history";
				console.error("[PcApprovalHistory] API Error: " + errorMsg);
				if (typeof callback === "function") {
					callback(false, errorMsg);
				}
			}
		})
		.fail(function (jqXHR, textStatus, errorThrown) {
			var errorMsg = "AJAX Error: " + textStatus + " - " + errorThrown;
			console.error("[PcApprovalHistory] " + errorMsg);
			
			// Show toast notification if available
			if (window.toastr && typeof window.toastr.error === "function") {
				window.toastr.error("Failed to load approval history", "Error");
			}
			
			if (typeof callback === "function") {
				callback(false, errorMsg);
			}
		});
	}

	/**
	 * Formats a date/time value for display in the timeline
	 * 
	 * @param {string|Date} dateValue - The date value to format
	 * @returns {string} Formatted date string (dd MMM yyyy HH:mm)
	 */
	function formatTimestamp(dateValue) {
		if (!dateValue) {
			return "";
		}

		var date;
		if (typeof dateValue === "string") {
			date = new Date(dateValue);
		} else if (dateValue instanceof Date) {
			date = dateValue;
		} else {
			return "";
		}

		if (isNaN(date.getTime())) {
			return "";
		}

		var months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
		var day = ("0" + date.getDate()).slice(-2);
		var month = months[date.getMonth()];
		var year = date.getFullYear();
		var hours = ("0" + date.getHours()).slice(-2);
		var minutes = ("0" + date.getMinutes()).slice(-2);

		return day + " " + month + " " + year + " " + hours + ":" + minutes;
	}

	/**
	 * Escapes HTML entities to prevent XSS attacks
	 * 
	 * @param {string} text - The text to escape
	 * @returns {string} Escaped text safe for HTML insertion
	 */
	function escapeHtml(text) {
		if (!text) {
			return "";
		}
		var div = document.createElement("div");
		div.textContent = text;
		return div.innerHTML;
	}

	/**
	 * Gets the display name for a user from a history record
	 * 
	 * @param {object} record - The history record containing user information
	 * @returns {string} User display name or 'Unknown User'
	 */
	function getUserDisplayName(record) {
		if (!record) {
			return "Unknown User";
		}

		// Try various possible user name properties
		if (record.performed_by_name) {
			return escapeHtml(record.performed_by_name);
		}
		if (record.user_name) {
			return escapeHtml(record.user_name);
		}
		if (record.performed_by_username) {
			return escapeHtml(record.performed_by_username);
		}
		if (record["$user_performed_by"] && record["$user_performed_by"].username) {
			return escapeHtml(record["$user_performed_by"].username);
		}
		if (record.performed_by) {
			// Just show the GUID abbreviated
			return "User " + String(record.performed_by).substring(0, 8) + "...";
		}
		
		return "Unknown User";
	}

	/**
	 * Renders the timeline HTML from history data
	 * 
	 * @param {Array} historyData - Array of history record objects
	 * @param {jQuery|HTMLElement|string} container - The container element or selector
	 */
	function renderTimeline(historyData, container) {
		var $container = $(container);
		
		if (!$container.length) {
			console.error("[PcApprovalHistory] Timeline container not found");
			return;
		}

		// Clear existing content
		$container.empty();

		// Handle empty history
		if (!historyData || !historyData.length) {
			var emptyHtml = '<div class="text-muted text-center py-4">' +
				'<i class="fas fa-history fa-2x mb-2 d-block"></i>' +
				'<span>No approval history records found</span>' +
				'</div>';
			$container.html(emptyHtml);
			return;
		}

		// Build timeline HTML
		var timelineHtml = '<div class="approval-history-timeline">';

		for (var i = 0; i < historyData.length; i++) {
			var record = historyData[i];
			var action = (record.action || "submitted").toLowerCase();
			var iconClass = ACTION_ICONS[action] || "fa-circle";
			var colorClass = ACTION_COLORS[action] || "text-secondary";
			var bgColorClass = ACTION_BG_COLORS[action] || "bg-secondary";
			var userName = getUserDisplayName(record);
			var timestamp = formatTimestamp(record.performed_on);
			var comments = record.comments ? escapeHtml(record.comments) : "";
			var actionLabel = action.charAt(0).toUpperCase() + action.slice(1);
			var stepName = record.step_name ? escapeHtml(record.step_name) : "";

			timelineHtml += '<div class="timeline-item d-flex mb-3">';
			
			// Timeline dot/icon
			timelineHtml += '<div class="timeline-icon me-3">';
			timelineHtml += '<div class="rounded-circle d-flex align-items-center justify-content-center ' + bgColorClass + '" style="width: 40px; height: 40px;">';
			timelineHtml += '<i class="fas ' + iconClass + ' text-white"></i>';
			timelineHtml += '</div>';
			timelineHtml += '</div>';

			// Timeline content
			timelineHtml += '<div class="timeline-content flex-grow-1">';
			timelineHtml += '<div class="d-flex justify-content-between align-items-start">';
			timelineHtml += '<div>';
			timelineHtml += '<span class="fw-bold ' + colorClass + '">' + actionLabel + '</span>';
			if (stepName) {
				timelineHtml += '<span class="text-muted ms-2">(' + stepName + ')</span>';
			}
			timelineHtml += '</div>';
			timelineHtml += '<small class="text-muted">' + timestamp + '</small>';
			timelineHtml += '</div>';
			timelineHtml += '<div class="text-secondary mb-1">';
			timelineHtml += '<i class="fas fa-user me-1"></i>' + userName;
			timelineHtml += '</div>';
			
			if (comments) {
				timelineHtml += '<div class="timeline-comments mt-2 p-2 bg-light rounded">';
				timelineHtml += '<i class="fas fa-comment me-1 text-muted"></i>';
				timelineHtml += '<span class="text-secondary">' + comments + '</span>';
				timelineHtml += '</div>';
			}
			
			timelineHtml += '</div>'; // .timeline-content
			timelineHtml += '</div>'; // .timeline-item

			// Add connecting line between items (except last item)
			if (i < historyData.length - 1) {
				timelineHtml += '<div class="timeline-connector ms-3 mb-3" style="border-left: 2px solid #dee2e6; height: 20px; margin-left: 19px;"></div>';
			}
		}

		timelineHtml += '</div>'; // .approval-history-timeline

		$container.html(timelineHtml);
	}

	/**
	 * Refreshes the history timeline from the current request context
	 * Finds the container with data-request-id attribute and reloads data
	 */
	function refreshHistory() {
		var $containers = $("[data-request-id]");
		
		$containers.each(function () {
			var $container = $(this);
			var requestId = $container.data("request-id");
			
			if (requestId) {
				// Show loading state
				$container.find(".approval-history-timeline").addClass("opacity-50");
				
				loadApprovalHistory(requestId, function (success, data) {
					if (success) {
						renderTimeline(data, $container);
						
						// Show success toast if available
						if (window.toastr && typeof window.toastr.success === "function") {
							window.toastr.success("History refreshed", "Success");
						}
					} else {
						// Remove loading state on error
						$container.find(".approval-history-timeline").removeClass("opacity-50");
					}
				});
			}
		});
	}

	/**
	 * Initializes the approval history component for a given container
	 * 
	 * @param {jQuery|HTMLElement|string} container - The container element or selector
	 */
	function initializeComponent(container) {
		var $container = $(container);
		var requestId = $container.data("request-id");
		
		if (requestId) {
			loadApprovalHistory(requestId, function (success, data) {
				if (success) {
					renderTimeline(data, $container);
				}
			});
		}
	}

	///////////////////////////////////////////////////////////////////////////////////
	/// DOM Ready Initialization
	///////////////////////////////////////////////////////////////////////////////////

	$(function () {
		// Auto-initialize any approval history containers on page load
		$("[data-approval-history-component]").each(function () {
			initializeComponent(this);
		});

		// Set up refresh button click handlers
		$(document).on("click", "[data-refresh-approval-history]", function (e) {
			e.preventDefault();
			var $button = $(this);
			var targetSelector = $button.data("target") || "[data-request-id]";
			var $container = $(targetSelector);
			
			if ($container.length) {
				var requestId = $container.data("request-id");
				if (requestId) {
					$button.prop("disabled", true);
					loadApprovalHistory(requestId, function (success, data) {
						$button.prop("disabled", false);
						if (success) {
							renderTimeline(data, $container);
						}
					});
				}
			}
		});

		///////////////////////////////////////////////////////////////////////////////////
		/// Page Builder Integration - WvPbManager Event Listeners
		///////////////////////////////////////////////////////////////////////////////////

		// Uncomment these event listeners when page builder integration is needed
		
		//	document.addEventListener("WvPbManager_Design_Loaded", function (event) {
		//		if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
		//			console.log("[PcApprovalHistory] Design view loaded");
		//		}
		//	});

		//	document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
		//		if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
		//			console.log("[PcApprovalHistory] Design view unloaded");
		//		}
		//	});

		//	document.addEventListener("WvPbManager_Options_Loaded", function (event) {
		//		if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
		//			console.log("[PcApprovalHistory] Options panel loaded");
		//		}
		//	});

		//	document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
		//		if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
		//			console.log("[PcApprovalHistory] Options panel unloaded");
		//		}
		//	});

		//	document.addEventListener("WvPbManager_Node_Moved", function (event) {
		//		if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
		//			console.log("[PcApprovalHistory] Component node moved");
		//		}
		//	});

	});

	///////////////////////////////////////////////////////////////////////////////////
	/// Expose Public API on window object for external access
	///////////////////////////////////////////////////////////////////////////////////

	window.PcApprovalHistory = {
		loadHistory: loadApprovalHistory,
		renderTimeline: renderTimeline,
		refreshHistory: refreshHistory,
		initializeComponent: initializeComponent,
		formatTimestamp: formatTimestamp
	};

	///////////////////////////////////////////////////////////////////////////////////
	/// End of Component Logic
	///////////////////////////////////////////////////////////////////////////////////

})(window, jQuery);
