"use strict";
/**
 * PcApprovalRequestList Component Client-Side JavaScript
 * 
 * Provides AJAX logic for loading and filtering approval request lists,
 * handling pagination, and integrating with page builder lifecycle events.
 * 
 * @component WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList
 * @requires jQuery
 * @author WebVella ERP Approval Plugin
 */
(function (window, $) {

	/// Constants and Configuration
	///////////////////////////////////////////////////////////////////////////////////
	
	var API_BASE_URL = "/api/v3.0/p/approval";
	var PENDING_ENDPOINT = API_BASE_URL + "/pending";
	var REQUEST_DETAIL_ENDPOINT = API_BASE_URL + "/request";
	var COMPONENT_NAME = "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList";
	var DEFAULT_PAGE_SIZE = 10;
	var AUTO_REFRESH_INTERVAL = 60000; // 60 seconds
	
	/// Private Variables
	///////////////////////////////////////////////////////////////////////////////////
	
	var currentPage = 1;
	var totalPages = 1;
	var isLoading = false;
	var autoRefreshTimer = null;
	var currentFilters = {
		status: "",
		searchText: "",
		dateFrom: "",
		dateTo: "",
		pageSize: DEFAULT_PAGE_SIZE
	};

	/// Private Functions
	///////////////////////////////////////////////////////////////////////////////////
	
	/**
	 * Shows a toast notification message
	 * @param {string} message - The message to display
	 * @param {string} type - The notification type: 'success', 'error', 'warning', 'info'
	 */
	function showToast(message, type) {
		type = type || "info";
		
		// Use WebVella's toast notification system if available
		if (window.WvToast && typeof window.WvToast.show === "function") {
			window.WvToast.show(message, type);
		} else if (window.toastr && typeof window.toastr[type] === "function") {
			// Fallback to toastr if available
			window.toastr[type](message);
		} else {
			// Console fallback for development
			console.log("[" + type.toUpperCase() + "] " + message);
		}
	}

	/**
	 * Shows loading indicator on the request list container
	 * @param {jQuery} $container - The container element
	 * @param {boolean} show - Whether to show or hide the loader
	 */
	function showLoadingIndicator($container, show) {
		var $loader = $container.find(".approval-request-loader");
		var $content = $container.find(".approval-request-content");
		
		if (show) {
			isLoading = true;
			if ($loader.length === 0) {
				$container.prepend('<div class="approval-request-loader text-center p-3"><i class="fas fa-spinner fa-spin fa-2x"></i><p class="mt-2">Loading approval requests...</p></div>');
				$loader = $container.find(".approval-request-loader");
			}
			$loader.show();
			$content.addClass("loading-opacity");
		} else {
			isLoading = false;
			$loader.hide();
			$content.removeClass("loading-opacity");
		}
	}

	/**
	 * Builds the query string from current filters
	 * @returns {string} The query string for API request
	 */
	function buildQueryString() {
		var params = [];
		
		if (currentFilters.status && currentFilters.status !== "") {
			params.push("status=" + encodeURIComponent(currentFilters.status));
		}
		if (currentFilters.searchText && currentFilters.searchText !== "") {
			params.push("search=" + encodeURIComponent(currentFilters.searchText));
		}
		if (currentFilters.dateFrom && currentFilters.dateFrom !== "") {
			params.push("dateFrom=" + encodeURIComponent(currentFilters.dateFrom));
		}
		if (currentFilters.dateTo && currentFilters.dateTo !== "") {
			params.push("dateTo=" + encodeURIComponent(currentFilters.dateTo));
		}
		params.push("page=" + currentPage);
		params.push("pageSize=" + currentFilters.pageSize);
		
		return params.length > 0 ? "?" + params.join("&") : "";
	}

	/**
	 * Loads approval requests from the API
	 * @param {jQuery} $container - The component container element
	 * @param {function} callback - Optional callback after load completes
	 */
	function loadApprovalRequests($container, callback) {
		if (isLoading) {
			return;
		}
		
		var url = PENDING_ENDPOINT + buildQueryString();
		showLoadingIndicator($container, true);
		
		$.ajax({
			url: url,
			type: "GET",
			dataType: "json",
			headers: {
				"Accept": "application/json"
			},
			success: function (response) {
				showLoadingIndicator($container, false);
				
				if (response && response.success) {
					renderRequestList($container, response.object);
					updatePagination($container, response.object);
					
					if (typeof callback === "function") {
						callback(null, response.object);
					}
				} else {
					var errorMessage = response && response.message ? response.message : "Failed to load approval requests";
					showToast(errorMessage, "error");
					renderEmptyState($container, errorMessage);
					
					if (typeof callback === "function") {
						callback(new Error(errorMessage), null);
					}
				}
			},
			error: function (xhr, status, error) {
				showLoadingIndicator($container, false);
				
				var errorMessage = "Error loading approval requests";
				if (xhr.responseJSON && xhr.responseJSON.message) {
					errorMessage = xhr.responseJSON.message;
				} else if (xhr.status === 401) {
					errorMessage = "You are not authorized to view approval requests. Please log in.";
				} else if (xhr.status === 403) {
					errorMessage = "You do not have permission to view these approval requests.";
				} else if (xhr.status === 404) {
					errorMessage = "Approval endpoint not found. Please contact system administrator.";
				} else if (xhr.status === 500) {
					errorMessage = "Server error occurred while loading approval requests.";
				} else if (status === "timeout") {
					errorMessage = "Request timed out. Please try again.";
				}
				
				showToast(errorMessage, "error");
				renderEmptyState($container, errorMessage);
				
				if (typeof callback === "function") {
					callback(new Error(errorMessage), null);
				}
			}
		});
	}

	/**
	 * Renders the approval request list table
	 * @param {jQuery} $container - The component container element
	 * @param {object} data - The response data containing request list
	 */
	function renderRequestList($container, data) {
		var $tbody = $container.find(".approval-request-table tbody");
		if ($tbody.length === 0) {
			return;
		}
		
		$tbody.empty();
		
		var requests = data && data.requests ? data.requests : (data && Array.isArray(data) ? data : []);
		
		if (requests.length === 0) {
			renderEmptyState($container, "No approval requests found matching your criteria.");
			return;
		}
		
		requests.forEach(function (request) {
			var statusClass = getStatusClass(request.status);
			var formattedDate = formatDate(request.requestedOn || request.requested_on);
			var requesterName = request.requestedByName || request.requested_by_name || "Unknown";
			var workflowName = request.workflowName || request.workflow_name || "N/A";
			var entityName = request.source_entity || "N/A";
			
			var $row = $('<tr class="approval-request-row" data-request-id="' + request.id + '">' +
				'<td class="request-id-cell">' + formatRequestId(request.id) + '</td>' +
				'<td class="workflow-cell">' + escapeHtml(workflowName) + '</td>' +
				'<td class="entity-cell">' + escapeHtml(entityName) + '</td>' +
				'<td class="requester-cell">' + escapeHtml(requesterName) + '</td>' +
				'<td class="date-cell">' + formattedDate + '</td>' +
				'<td class="status-cell"><span class="badge ' + statusClass + '">' + escapeHtml(capitalizeFirst(request.status)) + '</span></td>' +
				'<td class="actions-cell">' +
					'<button type="button" class="btn btn-sm btn-outline-primary view-request-btn" title="View Details">' +
						'<i class="fas fa-eye"></i>' +
					'</button>' +
				'</td>' +
			'</tr>');
			
			$tbody.append($row);
		});
		
		// Hide empty state if it was shown
		$container.find(".approval-request-empty").hide();
		$container.find(".approval-request-content").show();
	}

	/**
	 * Renders an empty state message
	 * @param {jQuery} $container - The component container element
	 * @param {string} message - The message to display
	 */
	function renderEmptyState($container, message) {
		var $tbody = $container.find(".approval-request-table tbody");
		var $emptyState = $container.find(".approval-request-empty");
		
		if ($tbody.length > 0) {
			$tbody.empty();
			$tbody.append('<tr><td colspan="7" class="text-center text-muted py-4">' + 
				'<i class="fas fa-inbox fa-3x mb-3"></i><br>' + 
				escapeHtml(message) + '</td></tr>');
		}
		
		if ($emptyState.length > 0) {
			$emptyState.find(".empty-message").text(message);
			$emptyState.show();
		}
	}

	/**
	 * Updates the pagination controls
	 * @param {jQuery} $container - The component container element
	 * @param {object} data - The response data containing pagination info
	 */
	function updatePagination($container, data) {
		var $pagination = $container.find(".approval-request-pagination");
		if ($pagination.length === 0) {
			return;
		}
		
		var total = data && data.totalCount !== undefined ? data.totalCount : (data && data.total_count !== undefined ? data.total_count : 0);
		var pageSize = currentFilters.pageSize;
		totalPages = Math.ceil(total / pageSize) || 1;
		
		// Update page info display
		var $pageInfo = $container.find(".pagination-info");
		if ($pageInfo.length > 0) {
			var startItem = total > 0 ? ((currentPage - 1) * pageSize) + 1 : 0;
			var endItem = Math.min(currentPage * pageSize, total);
			$pageInfo.text("Showing " + startItem + " - " + endItem + " of " + total + " requests");
		}
		
		// Update pagination buttons
		var $prevBtn = $pagination.find(".pagination-prev");
		var $nextBtn = $pagination.find(".pagination-next");
		var $pageNumbers = $pagination.find(".pagination-numbers");
		
		// Enable/disable previous button
		if ($prevBtn.length > 0) {
			$prevBtn.prop("disabled", currentPage <= 1);
			$prevBtn.toggleClass("disabled", currentPage <= 1);
		}
		
		// Enable/disable next button
		if ($nextBtn.length > 0) {
			$nextBtn.prop("disabled", currentPage >= totalPages);
			$nextBtn.toggleClass("disabled", currentPage >= totalPages);
		}
		
		// Render page numbers
		if ($pageNumbers.length > 0) {
			$pageNumbers.empty();
			
			var startPage = Math.max(1, currentPage - 2);
			var endPage = Math.min(totalPages, currentPage + 2);
			
			// First page
			if (startPage > 1) {
				$pageNumbers.append('<button type="button" class="btn btn-sm btn-outline-secondary pagination-page" data-page="1">1</button>');
				if (startPage > 2) {
					$pageNumbers.append('<span class="pagination-ellipsis mx-1">...</span>');
				}
			}
			
			// Page number buttons
			for (var i = startPage; i <= endPage; i++) {
				var activeClass = i === currentPage ? "btn-primary" : "btn-outline-secondary";
				$pageNumbers.append('<button type="button" class="btn btn-sm ' + activeClass + ' pagination-page" data-page="' + i + '">' + i + '</button>');
			}
			
			// Last page
			if (endPage < totalPages) {
				if (endPage < totalPages - 1) {
					$pageNumbers.append('<span class="pagination-ellipsis mx-1">...</span>');
				}
				$pageNumbers.append('<button type="button" class="btn btn-sm btn-outline-secondary pagination-page" data-page="' + totalPages + '">' + totalPages + '</button>');
			}
		}
	}

	/**
	 * Navigates to a specific page
	 * @param {jQuery} $container - The component container element
	 * @param {number} page - The page number to navigate to
	 */
	function goToPage($container, page) {
		if (page < 1 || page > totalPages || page === currentPage) {
			return;
		}
		
		currentPage = page;
		loadApprovalRequests($container);
	}

	/**
	 * Applies the current filters and reloads the list
	 * @param {jQuery} $container - The component container element
	 */
	function applyFilters($container) {
		// Reset to first page when filters change
		currentPage = 1;
		loadApprovalRequests($container);
	}

	/**
	 * Resets all filters to default values
	 * @param {jQuery} $container - The component container element
	 */
	function resetFilters($container) {
		currentFilters = {
			status: "",
			searchText: "",
			dateFrom: "",
			dateTo: "",
			pageSize: DEFAULT_PAGE_SIZE
		};
		currentPage = 1;
		
		// Reset form inputs
		$container.find(".filter-status").val("");
		$container.find(".filter-search").val("");
		$container.find(".filter-date-from").val("");
		$container.find(".filter-date-to").val("");
		
		loadApprovalRequests($container);
	}

	/**
	 * Refreshes the current list data
	 * @param {jQuery} $container - The component container element
	 */
	function refreshList($container) {
		loadApprovalRequests($container);
	}

	/**
	 * Starts auto-refresh timer
	 * @param {jQuery} $container - The component container element
	 * @param {number} interval - Refresh interval in milliseconds
	 */
	function startAutoRefresh($container, interval) {
		stopAutoRefresh();
		interval = interval || AUTO_REFRESH_INTERVAL;
		
		autoRefreshTimer = setInterval(function () {
			if (!isLoading) {
				refreshList($container);
			}
		}, interval);
	}

	/**
	 * Stops auto-refresh timer
	 */
	function stopAutoRefresh() {
		if (autoRefreshTimer) {
			clearInterval(autoRefreshTimer);
			autoRefreshTimer = null;
		}
	}

	/**
	 * Navigates to the request detail page
	 * @param {string} requestId - The request ID to view
	 */
	function viewRequestDetails(requestId) {
		if (!requestId) {
			showToast("Invalid request ID", "error");
			return;
		}
		
		// Navigate to request detail page
		var detailUrl = "/approval/request/" + requestId;
		window.location.href = detailUrl;
	}

	/// Utility Functions
	///////////////////////////////////////////////////////////////////////////////////

	/**
	 * Escapes HTML special characters to prevent XSS
	 * @param {string} text - The text to escape
	 * @returns {string} The escaped text
	 */
	function escapeHtml(text) {
		if (text === null || text === undefined) {
			return "";
		}
		var div = document.createElement("div");
		div.textContent = text;
		return div.innerHTML;
	}

	/**
	 * Formats a date string for display
	 * @param {string} dateString - The date string to format
	 * @returns {string} The formatted date
	 */
	function formatDate(dateString) {
		if (!dateString) {
			return "N/A";
		}
		
		try {
			var date = new Date(dateString);
			if (isNaN(date.getTime())) {
				return dateString;
			}
			
			var options = {
				year: "numeric",
				month: "short",
				day: "numeric",
				hour: "2-digit",
				minute: "2-digit"
			};
			
			return date.toLocaleDateString(undefined, options);
		} catch (e) {
			return dateString;
		}
	}

	/**
	 * Formats a request ID for display (shows abbreviated version)
	 * @param {string} id - The full GUID
	 * @returns {string} The abbreviated ID
	 */
	function formatRequestId(id) {
		if (!id) {
			return "N/A";
		}
		// Show first 8 characters of GUID
		return id.substring(0, 8).toUpperCase();
	}

	/**
	 * Gets the CSS class for a status badge
	 * @param {string} status - The status value
	 * @returns {string} The CSS class
	 */
	function getStatusClass(status) {
		switch ((status || "").toLowerCase()) {
			case "pending":
				return "badge-warning";
			case "approved":
				return "badge-success";
			case "rejected":
				return "badge-danger";
			case "escalated":
				return "badge-info";
			case "expired":
				return "badge-secondary";
			default:
				return "badge-light";
		}
	}

	/**
	 * Capitalizes the first letter of a string
	 * @param {string} text - The text to capitalize
	 * @returns {string} The capitalized text
	 */
	function capitalizeFirst(text) {
		if (!text) {
			return "";
		}
		return text.charAt(0).toUpperCase() + text.slice(1).toLowerCase();
	}

	/// Event Handlers Initialization
	///////////////////////////////////////////////////////////////////////////////////

	$(function () {
		// Initialize each approval request list component on the page
		$(".pc-approval-request-list").each(function () {
			var $container = $(this);
			var componentOptions = $container.data("options") || {};
			
			// Set page size from component options if available
			if (componentOptions.pageSize) {
				currentFilters.pageSize = parseInt(componentOptions.pageSize, 10) || DEFAULT_PAGE_SIZE;
			}
			
			// Initial load
			loadApprovalRequests($container);
			
			// Start auto-refresh if enabled
			if (componentOptions.autoRefresh !== false) {
				var refreshInterval = componentOptions.refreshInterval || AUTO_REFRESH_INTERVAL;
				startAutoRefresh($container, refreshInterval);
			}
		});

		// Filter form submission handler
		$(document).on("submit", ".approval-request-filter-form", function (e) {
			e.preventDefault();
			var $form = $(this);
			var $container = $form.closest(".pc-approval-request-list");
			
			// Collect filter values
			currentFilters.status = $form.find(".filter-status").val() || "";
			currentFilters.searchText = $form.find(".filter-search").val() || "";
			currentFilters.dateFrom = $form.find(".filter-date-from").val() || "";
			currentFilters.dateTo = $form.find(".filter-date-to").val() || "";
			
			applyFilters($container);
		});

		// Status filter change handler
		$(document).on("change", ".pc-approval-request-list .filter-status", function () {
			var $container = $(this).closest(".pc-approval-request-list");
			currentFilters.status = $(this).val() || "";
			applyFilters($container);
		});

		// Search input handler (with debounce)
		var searchTimeout = null;
		$(document).on("input", ".pc-approval-request-list .filter-search", function () {
			var $input = $(this);
			var $container = $input.closest(".pc-approval-request-list");
			
			clearTimeout(searchTimeout);
			searchTimeout = setTimeout(function () {
				currentFilters.searchText = $input.val() || "";
				applyFilters($container);
			}, 500); // 500ms debounce
		});

		// Date range filter handlers
		$(document).on("change", ".pc-approval-request-list .filter-date-from", function () {
			var $container = $(this).closest(".pc-approval-request-list");
			currentFilters.dateFrom = $(this).val() || "";
			applyFilters($container);
		});

		$(document).on("change", ".pc-approval-request-list .filter-date-to", function () {
			var $container = $(this).closest(".pc-approval-request-list");
			currentFilters.dateTo = $(this).val() || "";
			applyFilters($container);
		});

		// Reset filters button handler
		$(document).on("click", ".pc-approval-request-list .filter-reset-btn", function (e) {
			e.preventDefault();
			var $container = $(this).closest(".pc-approval-request-list");
			resetFilters($container);
		});

		// Refresh button handler
		$(document).on("click", ".pc-approval-request-list .refresh-btn", function (e) {
			e.preventDefault();
			var $container = $(this).closest(".pc-approval-request-list");
			refreshList($container);
		});

		// Pagination - Previous page handler
		$(document).on("click", ".pc-approval-request-list .pagination-prev:not(.disabled)", function (e) {
			e.preventDefault();
			var $container = $(this).closest(".pc-approval-request-list");
			goToPage($container, currentPage - 1);
		});

		// Pagination - Next page handler
		$(document).on("click", ".pc-approval-request-list .pagination-next:not(.disabled)", function (e) {
			e.preventDefault();
			var $container = $(this).closest(".pc-approval-request-list");
			goToPage($container, currentPage + 1);
		});

		// Pagination - Page number click handler
		$(document).on("click", ".pc-approval-request-list .pagination-page", function (e) {
			e.preventDefault();
			var $container = $(this).closest(".pc-approval-request-list");
			var page = parseInt($(this).data("page"), 10);
			if (!isNaN(page)) {
				goToPage($container, page);
			}
		});

		// Table row click handler - Navigate to request details
		$(document).on("click", ".pc-approval-request-list .approval-request-row", function (e) {
			// Don't navigate if clicking on action buttons
			if ($(e.target).closest(".actions-cell").length > 0) {
				return;
			}
			
			var requestId = $(this).data("request-id");
			if (requestId) {
				viewRequestDetails(requestId);
			}
		});

		// View request button handler
		$(document).on("click", ".pc-approval-request-list .view-request-btn", function (e) {
			e.preventDefault();
			e.stopPropagation();
			
			var requestId = $(this).closest(".approval-request-row").data("request-id");
			if (requestId) {
				viewRequestDetails(requestId);
			}
		});

		// Page size change handler
		$(document).on("change", ".pc-approval-request-list .page-size-select", function () {
			var $container = $(this).closest(".pc-approval-request-list");
			var newPageSize = parseInt($(this).val(), 10);
			
			if (!isNaN(newPageSize) && newPageSize > 0) {
				currentFilters.pageSize = newPageSize;
				currentPage = 1; // Reset to first page when page size changes
				loadApprovalRequests($container);
			}
		});

		// Stop auto-refresh when page becomes hidden (browser tab switch)
		$(document).on("visibilitychange", function () {
			if (document.hidden) {
				stopAutoRefresh();
			} else {
				$(".pc-approval-request-list").each(function () {
					var $container = $(this);
					var componentOptions = $container.data("options") || {};
					
					if (componentOptions.autoRefresh !== false) {
						var refreshInterval = componentOptions.refreshInterval || AUTO_REFRESH_INTERVAL;
						startAutoRefresh($container, refreshInterval);
						// Also do an immediate refresh when tab becomes visible
						refreshList($container);
					}
				});
			}
		});

		/// Page Builder Lifecycle Event Listeners
		///////////////////////////////////////////////////////////////////////////////////

		//	document.addEventListener("WvPbManager_Design_Loaded", function (event) {
		//		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList") {
		//			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList Design loaded");
		//			// Initialize component in design mode if needed
		//		}
		//	});

		//	document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
		//		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList") {
		//			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList Design unloaded");
		//			// Cleanup when component is removed from design
		//			stopAutoRefresh();
		//		}
		//	});

		//	document.addEventListener("WvPbManager_Options_Loaded", function (event) {
		//		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList") {
		//			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList Options loaded");
		//			// Initialize options panel if needed
		//		}
		//	});

		//	document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
		//		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList") {
		//			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList Options unloaded");
		//			// Cleanup options panel if needed
		//		}
		//	});

		//	document.addEventListener("WvPbManager_Node_Moved", function (event) {
		//		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList") {
		//			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList Moved");
		//		}
		//	});
	});

	/// Public API (exposed on window for external access if needed)
	///////////////////////////////////////////////////////////////////////////////////
	
	window.PcApprovalRequestList = {
		refresh: function () {
			$(".pc-approval-request-list").each(function () {
				refreshList($(this));
			});
		},
		startAutoRefresh: function (interval) {
			$(".pc-approval-request-list").each(function () {
				startAutoRefresh($(this), interval);
			});
		},
		stopAutoRefresh: stopAutoRefresh,
		applyFilters: function (filters) {
			if (filters) {
				$.extend(currentFilters, filters);
			}
			$(".pc-approval-request-list").each(function () {
				applyFilters($(this));
			});
		},
		resetFilters: function () {
			$(".pc-approval-request-list").each(function () {
				resetFilters($(this));
			});
		},
		goToPage: function (page) {
			$(".pc-approval-request-list").each(function () {
				goToPage($(this), page);
			});
		}
	};

})(window, jQuery);
