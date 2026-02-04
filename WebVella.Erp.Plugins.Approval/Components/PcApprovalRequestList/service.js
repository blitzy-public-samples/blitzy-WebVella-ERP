"use strict";

/**
 * PcApprovalRequestList Client-Side Service
 * 
 * Provides client-side interaction handling for the PcApprovalRequestList PageComponent.
 * Handles page-builder lifecycle events, AJAX operations for loading/filtering approval requests,
 * sorting, and pagination control.
 * 
 * @namespace WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList
 */
(function (window, $) {

	/// Configuration and state management
	///////////////////////////////////////////////////////////////////////////////////
	
	var componentName = "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList";
	var apiBaseUrl = "/api/v3.0/p/approval/requests";
	
	// Current filter/pagination state
	var currentState = {
		filters: {
			status: "",
			workflowId: "",
			fromDate: "",
			toDate: ""
		},
		page: 1,
		sortColumn: "created_on",
		sortDirection: "desc"
	};

	/// Utility Functions
	///////////////////////////////////////////////////////////////////////////////////

	/**
	 * Shows a loading spinner overlay on the request list container
	 * @param {jQuery} $container - The container element to show spinner in
	 */
	function showLoading($container) {
		if ($container && $container.length) {
			$container.addClass("loading");
			if (!$container.find(".wv-loading-spinner").length) {
				$container.append('<div class="wv-loading-spinner"><i class="fas fa-spinner fa-spin fa-2x"></i></div>');
			}
			$container.find(".wv-loading-spinner").show();
		}
	}

	/**
	 * Hides the loading spinner overlay from the request list container
	 * @param {jQuery} $container - The container element to hide spinner from
	 */
	function hideLoading($container) {
		if ($container && $container.length) {
			$container.removeClass("loading");
			$container.find(".wv-loading-spinner").hide();
		}
	}

	/**
	 * Displays a success notification using toastr
	 * @param {string} message - The success message to display
	 */
	function showSuccess(message) {
		if (window.toastr) {
			toastr.success(message);
		}
		// console.log("Success: " + message);
	}

	/**
	 * Displays an error notification using toastr
	 * @param {string} message - The error message to display
	 */
	function showError(message) {
		if (window.toastr) {
			toastr.error(message);
		}
		// console.log("Error: " + message);
	}

	/**
	 * Builds the query string from the current filter state
	 * @returns {string} The query string for API requests
	 */
	function buildQueryString() {
		var params = [];
		
		if (currentState.filters.status) {
			params.push("status=" + encodeURIComponent(currentState.filters.status));
		}
		if (currentState.filters.workflowId) {
			params.push("workflowId=" + encodeURIComponent(currentState.filters.workflowId));
		}
		if (currentState.filters.fromDate) {
			params.push("fromDate=" + encodeURIComponent(currentState.filters.fromDate));
		}
		if (currentState.filters.toDate) {
			params.push("toDate=" + encodeURIComponent(currentState.filters.toDate));
		}
		
		params.push("page=" + currentState.page);
		params.push("sortColumn=" + encodeURIComponent(currentState.sortColumn));
		params.push("sortDirection=" + encodeURIComponent(currentState.sortDirection));
		
		return params.length > 0 ? "?" + params.join("&") : "";
	}

	/// AJAX Handler Functions
	///////////////////////////////////////////////////////////////////////////////////

	/**
	 * Loads approval requests from the API with the specified filters and pagination
	 * @param {Object} filters - Filter criteria (status, workflowId, fromDate, toDate)
	 * @param {number} page - Page number to load
	 * @param {string} sortColumn - Column name to sort by
	 * @param {string} sortDirection - Sort direction (asc or desc)
	 * @returns {jQuery.Promise} Promise that resolves with the request data
	 */
	function loadApprovalRequests(filters, page, sortColumn, sortDirection) {
		// Update current state
		if (filters) {
			currentState.filters = $.extend({}, currentState.filters, filters);
		}
		if (typeof page === "number" && page > 0) {
			currentState.page = page;
		}
		if (sortColumn) {
			currentState.sortColumn = sortColumn;
		}
		if (sortDirection && (sortDirection === "asc" || sortDirection === "desc")) {
			currentState.sortDirection = sortDirection;
		}

		var $container = $(".pc-approval-request-list-container");
		showLoading($container);

		var url = apiBaseUrl + buildQueryString();
		// console.log("Loading approval requests from: " + url);

		return $.ajax({
			url: url,
			type: "GET",
			dataType: "json",
			contentType: "application/json",
			headers: {
				"Accept": "application/json"
			}
		}).done(function (response) {
			hideLoading($container);
			
			if (response && response.success) {
				// console.log("Successfully loaded " + (response.object ? response.object.length : 0) + " requests");
				updateRequestListUI(response.object, response.meta);
			} else {
				var errorMsg = response && response.message ? response.message : "Failed to load approval requests";
				showError(errorMsg);
			}
		}).fail(function (xhr, status, error) {
			hideLoading($container);
			var errorMsg = "Error loading approval requests: " + (error || status);
			showError(errorMsg);
			// console.log("AJAX Error: " + errorMsg);
		});
	}

	/**
	 * Refreshes the current request list view using existing filter/sort state
	 */
	function refreshRequestList() {
		// console.log("Refreshing request list with current state");
		return loadApprovalRequests(null, null, null, null);
	}

	/**
	 * Handles pagination page change events
	 * @param {number} page - The page number to navigate to
	 */
	function handlePagination(page) {
		if (typeof page !== "number" || page < 1) {
			// console.log("Invalid page number: " + page);
			return;
		}
		// console.log("Navigating to page: " + page);
		loadApprovalRequests(null, page, null, null);
	}

	/**
	 * Handles column header sort click events
	 * @param {string} column - The column name to sort by
	 */
	function handleSort(column) {
		if (!column) {
			return;
		}

		// Toggle direction if clicking same column, otherwise default to descending
		var newDirection = "desc";
		if (currentState.sortColumn === column) {
			newDirection = currentState.sortDirection === "asc" ? "desc" : "asc";
		}

		// console.log("Sorting by column: " + column + " " + newDirection);
		loadApprovalRequests(null, 1, column, newDirection);
	}

	/**
	 * Handles filter form submission
	 * Collects filter values from form inputs and triggers a reload
	 */
	function handleFilter() {
		var filters = {
			status: $("#approval-filter-status").val() || "",
			workflowId: $("#approval-filter-workflow").val() || "",
			fromDate: $("#approval-filter-from-date").val() || "",
			toDate: $("#approval-filter-to-date").val() || ""
		};

		// console.log("Applying filters: " + JSON.stringify(filters));
		
		// Reset to page 1 when filtering
		loadApprovalRequests(filters, 1, null, null);
	}

	/**
	 * Clears all filters and reloads the request list
	 */
	function clearFilters() {
		currentState.filters = {
			status: "",
			workflowId: "",
			fromDate: "",
			toDate: ""
		};

		// Clear filter form inputs
		$("#approval-filter-status").val("");
		$("#approval-filter-workflow").val("");
		$("#approval-filter-from-date").val("");
		$("#approval-filter-to-date").val("");

		// console.log("Filters cleared");
		loadApprovalRequests(null, 1, null, null);
	}

	/// UI Update Functions
	///////////////////////////////////////////////////////////////////////////////////

	/**
	 * Updates the request list UI with new data
	 * @param {Array} requests - Array of approval request objects
	 * @param {Object} meta - Pagination metadata (totalCount, pageSize, currentPage, totalPages)
	 */
	function updateRequestListUI(requests, meta) {
		var $tableBody = $(".pc-approval-request-list-container .request-table tbody");
		var $pagination = $(".pc-approval-request-list-container .pagination-container");
		var $emptyState = $(".pc-approval-request-list-container .empty-state");

		if (!requests || requests.length === 0) {
			$tableBody.empty();
			$pagination.hide();
			$emptyState.show();
			return;
		}

		$emptyState.hide();
		
		// Build table rows
		var rowsHtml = "";
		for (var i = 0; i < requests.length; i++) {
			var request = requests[i];
			rowsHtml += buildRequestRow(request);
		}
		
		$tableBody.html(rowsHtml);

		// Update pagination
		if (meta) {
			updatePaginationUI(meta);
			$pagination.show();
		}

		// Update sort indicators
		updateSortIndicators();
	}

	/**
	 * Builds HTML for a single request table row
	 * @param {Object} request - The approval request object
	 * @returns {string} HTML string for the table row
	 */
	function buildRequestRow(request) {
		var statusClass = getStatusBadgeClass(request.status);
		var createdOn = formatDateTime(request.created_on);
		var dueDate = request.due_date ? formatDateTime(request.due_date) : "-";
		var detailUrl = "/approval/requests/" + request.id;

		return '<tr data-request-id="' + request.id + '">' +
			'<td>' + (request.id ? request.id.substring(0, 8) + '...' : '-') + '</td>' +
			'<td>' + escapeHtml(request.workflow_name || '-') + '</td>' +
			'<td>' + escapeHtml(request.entity_name || '-') + ' / ' + (request.record_id ? request.record_id.substring(0, 8) + '...' : '-') + '</td>' +
			'<td><span class="badge ' + statusClass + '">' + escapeHtml(request.status || 'Unknown') + '</span></td>' +
			'<td>' + createdOn + '</td>' +
			'<td>' + dueDate + '</td>' +
			'<td><a href="' + detailUrl + '" class="btn btn-sm btn-outline-primary view-details-btn"><i class="fas fa-eye"></i> View</a></td>' +
			'</tr>';
	}

	/**
	 * Gets the Bootstrap badge class for a given status
	 * @param {string} status - The approval status
	 * @returns {string} The badge CSS class
	 */
	function getStatusBadgeClass(status) {
		if (!status) {
			return "badge-secondary";
		}
		
		var statusLower = status.toLowerCase();
		switch (statusLower) {
			case "pending":
				return "badge-warning";
			case "approved":
				return "badge-success";
			case "rejected":
				return "badge-danger";
			case "escalated":
				return "badge-info";
			case "delegated":
				return "badge-primary";
			case "cancelled":
				return "badge-secondary";
			default:
				return "badge-secondary";
		}
	}

	/**
	 * Formats a date/time value for display
	 * @param {string} dateValue - The date string to format
	 * @returns {string} Formatted date string (yyyy-MM-dd HH:mm)
	 */
	function formatDateTime(dateValue) {
		if (!dateValue) {
			return "-";
		}
		
		try {
			var date = new Date(dateValue);
			if (isNaN(date.getTime())) {
				return "-";
			}
			
			var year = date.getFullYear();
			var month = ("0" + (date.getMonth() + 1)).slice(-2);
			var day = ("0" + date.getDate()).slice(-2);
			var hours = ("0" + date.getHours()).slice(-2);
			var minutes = ("0" + date.getMinutes()).slice(-2);
			
			return year + "-" + month + "-" + day + " " + hours + ":" + minutes;
		} catch (e) {
			return "-";
		}
	}

	/**
	 * Escapes HTML special characters for safe display
	 * @param {string} text - The text to escape
	 * @returns {string} HTML-escaped text
	 */
	function escapeHtml(text) {
		if (!text) {
			return "";
		}
		
		var div = document.createElement("div");
		div.appendChild(document.createTextNode(text));
		return div.innerHTML;
	}

	/**
	 * Updates pagination controls based on metadata
	 * @param {Object} meta - Pagination metadata
	 */
	function updatePaginationUI(meta) {
		var $pagination = $(".pc-approval-request-list-container .pagination-container .pagination");
		if (!$pagination.length) {
			return;
		}

		var totalPages = meta.totalPages || 1;
		var currentPage = meta.currentPage || 1;
		var html = "";

		// Previous button
		html += '<li class="page-item' + (currentPage <= 1 ? ' disabled' : '') + '">';
		html += '<a class="page-link" href="#" data-page="' + (currentPage - 1) + '" aria-label="Previous">';
		html += '<span aria-hidden="true">&laquo;</span></a></li>';

		// Page numbers (show max 5 pages centered around current)
		var startPage = Math.max(1, currentPage - 2);
		var endPage = Math.min(totalPages, startPage + 4);
		if (endPage - startPage < 4) {
			startPage = Math.max(1, endPage - 4);
		}

		for (var i = startPage; i <= endPage; i++) {
			html += '<li class="page-item' + (i === currentPage ? ' active' : '') + '">';
			html += '<a class="page-link" href="#" data-page="' + i + '">' + i + '</a></li>';
		}

		// Next button
		html += '<li class="page-item' + (currentPage >= totalPages ? ' disabled' : '') + '">';
		html += '<a class="page-link" href="#" data-page="' + (currentPage + 1) + '" aria-label="Next">';
		html += '<span aria-hidden="true">&raquo;</span></a></li>';

		$pagination.html(html);

		// Update total count display
		$(".pc-approval-request-list-container .total-count").text(
			"Showing " + ((currentPage - 1) * (meta.pageSize || 10) + 1) + 
			" - " + Math.min(currentPage * (meta.pageSize || 10), meta.totalCount || 0) + 
			" of " + (meta.totalCount || 0)
		);
	}

	/**
	 * Updates sort indicator icons on table headers
	 */
	function updateSortIndicators() {
		// Remove existing indicators
		$(".pc-approval-request-list-container th[data-sortable] .sort-indicator").remove();
		
		// Add indicator to current sort column
		var $sortColumn = $(".pc-approval-request-list-container th[data-sortable='" + currentState.sortColumn + "']");
		if ($sortColumn.length) {
			var iconClass = currentState.sortDirection === "asc" ? "fa-sort-up" : "fa-sort-down";
			$sortColumn.append(' <i class="fas ' + iconClass + ' sort-indicator"></i>');
		}
	}

	/// Event Binding and Initialization
	///////////////////////////////////////////////////////////////////////////////////

	$(function () {

		// Page Builder Lifecycle Event Handlers (commented for debugging)
		//	document.addEventListener("WvPbManager_Design_Loaded", function (event) {
		//		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList") {
		//			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList Design loaded");
		//		}
		//	});

		//	document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
		//		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList") {
		//			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList Design unloaded");
		//		}
		//	});

		//	document.addEventListener("WvPbManager_Options_Loaded", function (event) {
		//		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList") {
		//			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList Options loaded");
		//		}
		//	});

		//	document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
		//		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList") {
		//			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList Options unloaded");
		//		}
		//	});

		//	document.addEventListener("WvPbManager_Node_Moved", function (event) {
		//		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList") {
		//			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalRequestList Moved");
		//		}
		//	});

		// Sort column click handler
		$(document).on("click", ".pc-approval-request-list-container th[data-sortable]", function (e) {
			e.preventDefault();
			var column = $(this).attr("data-sortable");
			if (column) {
				handleSort(column);
			}
		});

		// Pagination click handler
		$(document).on("click", ".pc-approval-request-list-container .pagination .page-link", function (e) {
			e.preventDefault();
			var $link = $(this);
			if ($link.parent().hasClass("disabled")) {
				return;
			}
			var page = parseInt($link.attr("data-page"), 10);
			if (!isNaN(page) && page > 0) {
				handlePagination(page);
			}
		});

		// Filter form submit handler
		$(document).on("submit", ".pc-approval-request-list-container .filter-form", function (e) {
			e.preventDefault();
			handleFilter();
		});

		// Filter button click handler
		$(document).on("click", ".pc-approval-request-list-container .btn-apply-filter", function (e) {
			e.preventDefault();
			handleFilter();
		});

		// Clear filter button handler
		$(document).on("click", ".pc-approval-request-list-container .btn-clear-filter", function (e) {
			e.preventDefault();
			clearFilters();
		});

		// Refresh button handler
		$(document).on("click", ".pc-approval-request-list-container .btn-refresh", function (e) {
			e.preventDefault();
			refreshRequestList();
		});

		// Filter input change handler (auto-filter on change for select elements)
		$(document).on("change", ".pc-approval-request-list-container #approval-filter-status", function () {
			handleFilter();
		});

		$(document).on("change", ".pc-approval-request-list-container #approval-filter-workflow", function () {
			handleFilter();
		});

	});

	/// Expose functions to global scope for external access
	///////////////////////////////////////////////////////////////////////////////////

	window.PcApprovalRequestList = {
		loadApprovalRequests: loadApprovalRequests,
		refreshRequestList: refreshRequestList,
		handlePagination: handlePagination,
		handleSort: handleSort,
		handleFilter: handleFilter,
		clearFilters: clearFilters,
		getCurrentState: function () {
			return $.extend(true, {}, currentState);
		}
	};

})(window, jQuery);
