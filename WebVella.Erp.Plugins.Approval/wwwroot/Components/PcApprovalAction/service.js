/**
 * service.js - Client-side JavaScript for PcApprovalAction component
 * 
 * Provides AJAX functionality for approval actions (approve, reject, delegate).
 * Implemented as IIFE (Immediately Invoked Function Expression) following WebVella patterns.
 * Handles POST requests to /api/v3.0/p/approval/request/{id}/approve|reject|delegate endpoints
 * with modal comment capture and success/error handling.
 * 
 * Dependencies:
 * - jQuery (for AJAX and DOM manipulation)
 * - Bootstrap 4 (for modal handling)
 * - toastr (optional, for toast notifications - falls back to alert if unavailable)
 */
'use strict';

(function (window, $) {

    //////////////////////////////////////////////////////////////////////////////////
    /// Your code is below

    /**
     * Namespace for approval action functions.
     * Exposed globally as window.WvApprovalAction for access from inline scripts.
     */
    window.WvApprovalAction = window.WvApprovalAction || {};

    /**
     * Modal element IDs used by this component.
     */
    var MODAL_IDS = {
        approve: 'wv-approval-modal-approve',
        reject: 'wv-approval-modal-reject',
        delegate: 'wv-approval-modal-delegate'
    };

    /**
     * Currently active request ID being processed.
     */
    var currentRequestId = null;

    /**
     * Approves an approval request.
     * Sends a POST request to the approval API endpoint with optional comments.
     * 
     * @param {string} requestId - The GUID of the approval request to approve.
     * @param {string} comments - Optional comments to include with the approval.
     */
    window.WvApprovalAction.approveRequest = function (requestId, comments) {
        if (!requestId) {
            console.error('[ApprovalAction] approveRequest: requestId is required');
            showNotification('error', 'Invalid request ID.');
            return;
        }

        performApprovalAction(
            '/api/v3.0/p/approval/request/' + requestId + '/approve',
            { comments: comments || '' },
            MODAL_IDS.approve,
            'Request approved successfully!'
        );
    };

    /**
     * Rejects an approval request.
     * Sends a POST request to the rejection API endpoint with reason and comments.
     * 
     * @param {string} requestId - The GUID of the approval request to reject.
     * @param {string} comments - Optional additional comments.
     * @param {string} reason - The reason for rejection (required).
     */
    window.WvApprovalAction.rejectRequest = function (requestId, comments, reason) {
        if (!requestId) {
            console.error('[ApprovalAction] rejectRequest: requestId is required');
            showNotification('error', 'Invalid request ID.');
            return;
        }

        if (!reason || reason.trim() === '') {
            showNotification('error', 'Please provide a reason for rejection.');
            // Highlight the reason field if modal is open
            var reasonField = document.querySelector('#' + MODAL_IDS.reject + ' .rejection-reason');
            if (reasonField) {
                reasonField.classList.add('is-invalid');
                reasonField.focus();
            }
            return;
        }

        performApprovalAction(
            '/api/v3.0/p/approval/request/' + requestId + '/reject',
            { comments: comments || '', reason: reason },
            MODAL_IDS.reject,
            'Request rejected.'
        );
    };

    /**
     * Delegates an approval request to another user.
     * Sends a POST request to the delegation API endpoint with the target user ID.
     * 
     * @param {string} requestId - The GUID of the approval request to delegate.
     * @param {string} delegateToUserId - The GUID of the user to delegate to.
     * @param {string} comments - Optional comments/instructions for the delegate.
     */
    window.WvApprovalAction.delegateRequest = function (requestId, delegateToUserId, comments) {
        if (!requestId) {
            console.error('[ApprovalAction] delegateRequest: requestId is required');
            showNotification('error', 'Invalid request ID.');
            return;
        }

        if (!delegateToUserId || delegateToUserId.trim() === '') {
            showNotification('error', 'Please select a user to delegate to.');
            // Highlight the user selection field if modal is open
            var userField = document.querySelector('#' + MODAL_IDS.delegate + ' .delegate-user-select');
            if (userField) {
                userField.classList.add('is-invalid');
                userField.focus();
            }
            return;
        }

        performApprovalAction(
            '/api/v3.0/p/approval/request/' + requestId + '/delegate',
            { delegateToUserId: delegateToUserId, comments: comments || '' },
            MODAL_IDS.delegate,
            'Request delegated successfully!'
        );
    };

    /**
     * Shows the approve modal dialog for the specified request.
     * Displays a Bootstrap modal with a comments textarea and confirm button.
     * 
     * @param {string} requestId - The GUID of the approval request.
     */
    window.WvApprovalAction.showApproveModal = function (requestId) {
        if (!requestId) {
            console.error('[ApprovalAction] showApproveModal: requestId is required');
            return;
        }

        currentRequestId = requestId;

        // Check if modal already exists in the DOM
        var existingModal = document.getElementById(MODAL_IDS.approve);
        if (existingModal) {
            // Reset form and show existing modal
            resetModalForm(existingModal);
            showModal(MODAL_IDS.approve);
            return;
        }

        // Create the modal HTML
        var modalHtml = createApproveModalHtml();
        
        // Append to body
        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Setup event handlers for the new modal
        setupApproveModalHandlers();

        // Show the modal
        showModal(MODAL_IDS.approve);
    };

    /**
     * Shows the reject modal dialog for the specified request.
     * Displays a Bootstrap modal with a required reason field and comments textarea.
     * 
     * @param {string} requestId - The GUID of the approval request.
     */
    window.WvApprovalAction.showRejectModal = function (requestId) {
        if (!requestId) {
            console.error('[ApprovalAction] showRejectModal: requestId is required');
            return;
        }

        currentRequestId = requestId;

        // Check if modal already exists in the DOM
        var existingModal = document.getElementById(MODAL_IDS.reject);
        if (existingModal) {
            // Reset form and show existing modal
            resetModalForm(existingModal);
            showModal(MODAL_IDS.reject);
            return;
        }

        // Create the modal HTML
        var modalHtml = createRejectModalHtml();
        
        // Append to body
        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Setup event handlers for the new modal
        setupRejectModalHandlers();

        // Show the modal
        showModal(MODAL_IDS.reject);
    };

    /**
     * Shows the delegate modal dialog for the specified request.
     * Displays a Bootstrap modal with a user selection dropdown and comments textarea.
     * 
     * @param {string} requestId - The GUID of the approval request.
     */
    window.WvApprovalAction.showDelegateModal = function (requestId) {
        if (!requestId) {
            console.error('[ApprovalAction] showDelegateModal: requestId is required');
            return;
        }

        currentRequestId = requestId;

        // Check if modal already exists in the DOM
        var existingModal = document.getElementById(MODAL_IDS.delegate);
        if (existingModal) {
            // Reset form and show existing modal
            resetModalForm(existingModal);
            loadDelegateUsers();
            showModal(MODAL_IDS.delegate);
            return;
        }

        // Create the modal HTML
        var modalHtml = createDelegateModalHtml();
        
        // Append to body
        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Setup event handlers for the new modal
        setupDelegateModalHandlers();

        // Load available users for delegation
        loadDelegateUsers();

        // Show the modal
        showModal(MODAL_IDS.delegate);
    };

    /**
     * Creates the HTML for the approve modal.
     * 
     * @returns {string} The modal HTML string.
     */
    function createApproveModalHtml() {
        return '<div class="modal fade" id="' + MODAL_IDS.approve + '" tabindex="-1" role="dialog" aria-labelledby="' + MODAL_IDS.approve + '-label" aria-hidden="true">' +
            '<div class="modal-dialog modal-dialog-centered" role="document">' +
                '<div class="modal-content">' +
                    '<div class="modal-header bg-success text-white">' +
                        '<h5 class="modal-title" id="' + MODAL_IDS.approve + '-label">' +
                            '<i class="fas fa-check-circle mr-2"></i>Approve Request' +
                        '</h5>' +
                        '<button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">' +
                            '<span aria-hidden="true">&times;</span>' +
                        '</button>' +
                    '</div>' +
                    '<div class="modal-body">' +
                        '<p>Are you sure you want to approve this request?</p>' +
                        '<div class="form-group">' +
                            '<label for="approve-comments">Comments (optional)</label>' +
                            '<textarea class="form-control approval-comments" id="approve-comments" rows="3" ' +
                                'placeholder="Enter any comments for this approval..."></textarea>' +
                        '</div>' +
                    '</div>' +
                    '<div class="modal-footer">' +
                        '<button type="button" class="btn btn-secondary" data-dismiss="modal">' +
                            '<i class="fas fa-times mr-1"></i>Cancel' +
                        '</button>' +
                        '<button type="button" class="btn btn-success confirm-approve">' +
                            '<i class="fas fa-check mr-1"></i>Approve' +
                        '</button>' +
                    '</div>' +
                '</div>' +
            '</div>' +
        '</div>';
    }

    /**
     * Creates the HTML for the reject modal.
     * 
     * @returns {string} The modal HTML string.
     */
    function createRejectModalHtml() {
        return '<div class="modal fade" id="' + MODAL_IDS.reject + '" tabindex="-1" role="dialog" aria-labelledby="' + MODAL_IDS.reject + '-label" aria-hidden="true">' +
            '<div class="modal-dialog modal-dialog-centered" role="document">' +
                '<div class="modal-content">' +
                    '<div class="modal-header bg-danger text-white">' +
                        '<h5 class="modal-title" id="' + MODAL_IDS.reject + '-label">' +
                            '<i class="fas fa-times-circle mr-2"></i>Reject Request' +
                        '</h5>' +
                        '<button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">' +
                            '<span aria-hidden="true">&times;</span>' +
                        '</button>' +
                    '</div>' +
                    '<div class="modal-body">' +
                        '<p>Please provide a reason for rejecting this request.</p>' +
                        '<div class="form-group">' +
                            '<label for="reject-reason">Reason <span class="text-danger">*</span></label>' +
                            '<textarea class="form-control rejection-reason" id="reject-reason" rows="2" required ' +
                                'placeholder="Enter the reason for rejection (required)..."></textarea>' +
                            '<div class="invalid-feedback">Reason is required for rejection.</div>' +
                        '</div>' +
                        '<div class="form-group">' +
                            '<label for="reject-comments">Additional Comments (optional)</label>' +
                            '<textarea class="form-control rejection-comments" id="reject-comments" rows="2" ' +
                                'placeholder="Enter any additional comments..."></textarea>' +
                        '</div>' +
                    '</div>' +
                    '<div class="modal-footer">' +
                        '<button type="button" class="btn btn-secondary" data-dismiss="modal">' +
                            '<i class="fas fa-times mr-1"></i>Cancel' +
                        '</button>' +
                        '<button type="button" class="btn btn-danger confirm-reject">' +
                            '<i class="fas fa-times mr-1"></i>Reject' +
                        '</button>' +
                    '</div>' +
                '</div>' +
            '</div>' +
        '</div>';
    }

    /**
     * Creates the HTML for the delegate modal.
     * 
     * @returns {string} The modal HTML string.
     */
    function createDelegateModalHtml() {
        return '<div class="modal fade" id="' + MODAL_IDS.delegate + '" tabindex="-1" role="dialog" aria-labelledby="' + MODAL_IDS.delegate + '-label" aria-hidden="true">' +
            '<div class="modal-dialog modal-dialog-centered" role="document">' +
                '<div class="modal-content">' +
                    '<div class="modal-header bg-info text-white">' +
                        '<h5 class="modal-title" id="' + MODAL_IDS.delegate + '-label">' +
                            '<i class="fas fa-share mr-2"></i>Delegate Request' +
                        '</h5>' +
                        '<button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">' +
                            '<span aria-hidden="true">&times;</span>' +
                        '</button>' +
                    '</div>' +
                    '<div class="modal-body">' +
                        '<p>Select a user to delegate this approval request to.</p>' +
                        '<div class="form-group">' +
                            '<label for="delegate-user">Delegate To <span class="text-danger">*</span></label>' +
                            '<select class="form-control delegate-user-select" id="delegate-user" required>' +
                                '<option value="">-- Select User --</option>' +
                            '</select>' +
                            '<div class="invalid-feedback">Please select a user to delegate to.</div>' +
                        '</div>' +
                        '<div class="form-group">' +
                            '<label for="delegate-comments">Instructions/Comments (optional)</label>' +
                            '<textarea class="form-control delegation-comments" id="delegate-comments" rows="3" ' +
                                'placeholder="Enter any instructions or comments for the delegate..."></textarea>' +
                        '</div>' +
                    '</div>' +
                    '<div class="modal-footer">' +
                        '<button type="button" class="btn btn-secondary" data-dismiss="modal">' +
                            '<i class="fas fa-times mr-1"></i>Cancel' +
                        '</button>' +
                        '<button type="button" class="btn btn-info confirm-delegate">' +
                            '<i class="fas fa-share mr-1"></i>Delegate' +
                        '</button>' +
                    '</div>' +
                '</div>' +
            '</div>' +
        '</div>';
    }

    /**
     * Sets up event handlers for the approve modal.
     */
    function setupApproveModalHandlers() {
        var modal = document.getElementById(MODAL_IDS.approve);
        if (!modal) return;

        // Confirm button click handler
        var confirmBtn = modal.querySelector('.confirm-approve');
        if (confirmBtn) {
            confirmBtn.addEventListener('click', function () {
                var comments = modal.querySelector('.approval-comments').value.trim();
                window.WvApprovalAction.approveRequest(currentRequestId, comments);
            });
        }

        // Clear form on modal hidden
        modal.addEventListener('hidden.bs.modal', function () {
            resetModalForm(modal);
        });
    }

    /**
     * Sets up event handlers for the reject modal.
     */
    function setupRejectModalHandlers() {
        var modal = document.getElementById(MODAL_IDS.reject);
        if (!modal) return;

        // Confirm button click handler
        var confirmBtn = modal.querySelector('.confirm-reject');
        if (confirmBtn) {
            confirmBtn.addEventListener('click', function () {
                var reason = modal.querySelector('.rejection-reason').value.trim();
                var comments = modal.querySelector('.rejection-comments').value.trim();
                window.WvApprovalAction.rejectRequest(currentRequestId, comments, reason);
            });
        }

        // Clear invalid state on input
        var reasonField = modal.querySelector('.rejection-reason');
        if (reasonField) {
            reasonField.addEventListener('input', function () {
                this.classList.remove('is-invalid');
            });
        }

        // Clear form on modal hidden
        modal.addEventListener('hidden.bs.modal', function () {
            resetModalForm(modal);
        });
    }

    /**
     * Sets up event handlers for the delegate modal.
     */
    function setupDelegateModalHandlers() {
        var modal = document.getElementById(MODAL_IDS.delegate);
        if (!modal) return;

        // Confirm button click handler
        var confirmBtn = modal.querySelector('.confirm-delegate');
        if (confirmBtn) {
            confirmBtn.addEventListener('click', function () {
                var userSelect = modal.querySelector('.delegate-user-select');
                var delegateToUserId = userSelect ? userSelect.value : '';
                var comments = modal.querySelector('.delegation-comments').value.trim();
                window.WvApprovalAction.delegateRequest(currentRequestId, delegateToUserId, comments);
            });
        }

        // Clear invalid state on select change
        var userSelect = modal.querySelector('.delegate-user-select');
        if (userSelect) {
            userSelect.addEventListener('change', function () {
                this.classList.remove('is-invalid');
            });
        }

        // Clear form on modal hidden
        modal.addEventListener('hidden.bs.modal', function () {
            resetModalForm(modal);
        });
    }

    /**
     * Loads available users for the delegate dropdown.
     * Fetches users from the API and populates the select element.
     */
    function loadDelegateUsers() {
        var userSelect = document.querySelector('#' + MODAL_IDS.delegate + ' .delegate-user-select');
        if (!userSelect) return;

        // Show loading state
        userSelect.innerHTML = '<option value="">Loading users...</option>';
        userSelect.disabled = true;

        // Fetch users from API
        $.ajax({
            url: '/api/v3.0/p/approval/users/available',
            type: 'GET',
            contentType: 'application/json',
            success: function (response) {
                userSelect.innerHTML = '<option value="">-- Select User --</option>';
                
                if (response && response.object && Array.isArray(response.object)) {
                    response.object.forEach(function (user) {
                        var option = document.createElement('option');
                        option.value = user.id;
                        option.textContent = user.username || user.email || user.id;
                        userSelect.appendChild(option);
                    });
                }
                
                userSelect.disabled = false;
            },
            error: function (xhr, status, error) {
                console.error('[ApprovalAction] Failed to load users:', error);
                userSelect.innerHTML = '<option value="">-- Failed to load users --</option>';
                userSelect.disabled = false;
            }
        });
    }

    /**
     * Shows a Bootstrap modal by its ID.
     * Supports both Bootstrap 4 (via jQuery) and Bootstrap 5 (via native API).
     * 
     * @param {string} modalId - The ID of the modal to show.
     */
    function showModal(modalId) {
        var modalElement = document.getElementById(modalId);
        if (!modalElement) {
            console.error('[ApprovalAction] Modal not found:', modalId);
            return;
        }

        // Try Bootstrap 5 method
        if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
            var modal = bootstrap.Modal.getOrCreateInstance(modalElement);
            modal.show();
        }
        // Fallback to jQuery method (Bootstrap 4)
        else if (typeof $ !== 'undefined' && $.fn.modal) {
            $(modalElement).modal('show');
        } else {
            console.error('[ApprovalAction] Bootstrap modal not available');
        }
    }

    /**
     * Closes a Bootstrap modal by its ID.
     * Supports both Bootstrap 4 (via jQuery) and Bootstrap 5 (via native API).
     * 
     * @param {string} modalId - The ID of the modal to close.
     */
    function closeModal(modalId) {
        if (!modalId) return;

        var modalElement = document.getElementById(modalId);
        if (modalElement) {
            // Try Bootstrap 5 method
            if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
                var modalInstance = bootstrap.Modal.getInstance(modalElement);
                if (modalInstance) {
                    modalInstance.hide();
                }
            }
            // Fallback to jQuery method (Bootstrap 4)
            else if (typeof $ !== 'undefined' && $.fn.modal) {
                $(modalElement).modal('hide');
            }
        }
    }

    /**
     * Resets a modal form to its initial state.
     * Clears all input values and removes validation classes.
     * 
     * @param {Element} modal - The modal DOM element.
     */
    function resetModalForm(modal) {
        if (!modal) return;

        // Clear all textareas
        var textareas = modal.querySelectorAll('textarea');
        textareas.forEach(function (textarea) {
            textarea.value = '';
            textarea.classList.remove('is-invalid', 'is-valid');
        });

        // Reset all selects to first option
        var selects = modal.querySelectorAll('select');
        selects.forEach(function (select) {
            select.selectedIndex = 0;
            select.classList.remove('is-invalid', 'is-valid');
        });

        // Reset button state
        var buttons = modal.querySelectorAll('button[class*="confirm-"]');
        buttons.forEach(function (btn) {
            btn.disabled = false;
            // Remove spinner if present
            var spinner = btn.querySelector('.fa-spinner');
            if (spinner) {
                var icon = btn.querySelector('i');
                if (icon) {
                    icon.className = icon.className.replace('fa-spinner fa-spin', 'fa-check');
                }
                var text = btn.textContent;
                if (text.indexOf('Processing') !== -1) {
                    btn.innerHTML = btn.innerHTML.replace('Processing...', getButtonOriginalText(btn));
                }
            }
        });
    }

    /**
     * Gets the original text for a confirm button based on its class.
     * 
     * @param {Element} btn - The button element.
     * @returns {string} The original button text.
     */
    function getButtonOriginalText(btn) {
        if (btn.classList.contains('confirm-approve')) return 'Approve';
        if (btn.classList.contains('confirm-reject')) return 'Reject';
        if (btn.classList.contains('confirm-delegate')) return 'Delegate';
        return 'Confirm';
    }

    /**
     * Performs an approval action via AJAX POST request.
     * Handles loading state, success/error responses, and modal closing.
     * 
     * @param {string} url - The API endpoint URL.
     * @param {object} data - The request payload to send as JSON.
     * @param {string} modalId - The ID of the Bootstrap modal to close on success.
     * @param {string} successMessage - The message to display on success.
     */
    function performApprovalAction(url, data, modalId, successMessage) {
        // Find the confirmation button in the modal and disable it
        var btn = null;
        if (modalId) {
            btn = document.querySelector('#' + modalId + ' button[class*="confirm-"]');
        }
        var originalHtml = btn ? btn.innerHTML : '';

        // Show loading state on button
        if (btn) {
            btn.disabled = true;
            btn.innerHTML = '<i class="fas fa-spinner fa-spin mr-1"></i>Processing...';
        }

        // Perform AJAX request using jQuery
        $.ajax({
            url: url,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            success: function (response) {
                // Check if response indicates success
                if (response && response.success === false) {
                    handleError(response.message || 'An error occurred', btn, originalHtml);
                    return;
                }

                // Close the modal if it exists
                closeModal(modalId);

                // Show success message
                showNotification('success', successMessage);

                // Refresh the page to show updated status
                setTimeout(function () {
                    window.location.reload();
                }, 1000);
            },
            error: function (xhr, status, error) {
                var errorMessage = 'An error occurred while processing your request.';
                
                // Try to extract error message from response
                if (xhr.responseJSON && xhr.responseJSON.message) {
                    errorMessage = xhr.responseJSON.message;
                } else if (xhr.responseText) {
                    try {
                        var errorResponse = JSON.parse(xhr.responseText);
                        if (errorResponse.message) {
                            errorMessage = errorResponse.message;
                        }
                    } catch (e) {
                        // Use default error message if JSON parsing fails
                        console.error('[ApprovalAction] Failed to parse error response:', e);
                    }
                }

                handleError(errorMessage, btn, originalHtml);
            }
        });
    }

    /**
     * Handles error display and button state restoration.
     * Logs error to console and displays user-friendly notification.
     * 
     * @param {string} message - The error message to display.
     * @param {Element} btn - The button element to restore.
     * @param {string} originalHtml - The original button HTML content.
     */
    function handleError(message, btn, originalHtml) {
        // Log error to console for debugging
        console.error('[ApprovalAction] Error:', message);

        // Show error notification to user
        showNotification('error', message);

        // Re-enable the button and restore original content
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = originalHtml;
        }
    }

    /**
     * Shows a notification using toastr if available, otherwise falls back to alert.
     * 
     * @param {string} type - The notification type ('success', 'error', 'warning', 'info').
     * @param {string} message - The message to display.
     */
    function showNotification(type, message) {
        if (typeof toastr !== 'undefined') {
            switch (type) {
                case 'success':
                    toastr.success(message);
                    break;
                case 'error':
                    toastr.error(message);
                    break;
                case 'warning':
                    toastr.warning(message);
                    break;
                case 'info':
                    toastr.info(message);
                    break;
                default:
                    toastr.info(message);
            }
        } else {
            // Fallback to browser alert
            if (type === 'error') {
                alert('Error: ' + message);
            } else {
                alert(message);
            }
        }
    }

    // Wire up approve action - called from inline onclick handlers
    window.approveAction = function(requestId, modalId) {
        var comments = $('#' + modalId).find('textarea[name="approve-comments"]').val();
        window.WvApprovalAction.approveRequest(requestId, comments || '');
    };

    // Wire up reject action - called from inline onclick handlers
    window.rejectAction = function(requestId, modalId) {
        var reason = $('#' + modalId).find('textarea[name="reject-reason"]').val();
        var comments = $('#' + modalId).find('textarea[name="reject-comments"]').val();
        
        if (!reason || reason.trim() === '') {
            alert('Rejection reason is required');
            return;
        }
        
        window.WvApprovalAction.rejectRequest(requestId, comments || '', reason);
    };

    // Wire up delegate action - called from inline onclick handlers
    window.delegateAction = function(requestId, modalId) {
        var delegateUserId = $('#' + modalId).find('select[name="delegate-user"]').val();
        var comments = $('#' + modalId).find('textarea[name="delegate-comments"]').val();
        
        if (!delegateUserId) {
            alert('Please select a user to delegate to');
            return;
        }
        
        window.WvApprovalAction.delegateRequest(requestId, delegateUserId, comments || '');
    };

    /**
     * DOM Ready Handler
     * Sets up event handlers and WebVella Page Builder lifecycle listeners.
     */
    $(function () {

        /**
         * Click handler for approve buttons.
         * Extracts request ID from data-request-id attribute and shows approve modal.
         */
        $(document).on('click', '.btn-approve', function (e) {
            var requestId = $(this).attr('data-request-id') || $(this).data('requestId');
            if (requestId) {
                e.preventDefault();
                window.WvApprovalAction.showApproveModal(requestId);
            }
            // If no request ID found, let Bootstrap's data-bs-toggle handle the modal
        });

        /**
         * Click handler for reject buttons.
         * Extracts request ID from data-request-id attribute and shows reject modal.
         */
        $(document).on('click', '.btn-reject', function (e) {
            var requestId = $(this).attr('data-request-id') || $(this).data('requestId');
            if (requestId) {
                e.preventDefault();
                window.WvApprovalAction.showRejectModal(requestId);
            }
            // If no request ID found, let Bootstrap's data-bs-toggle handle the modal
        });

        /**
         * Click handler for delegate buttons.
         * Extracts request ID from data-request-id attribute and shows delegate modal.
         */
        $(document).on('click', '.btn-delegate', function (e) {
            var requestId = $(this).attr('data-request-id') || $(this).data('requestId');
            if (requestId) {
                e.preventDefault();
                window.WvApprovalAction.showDelegateModal(requestId);
            }
            // If no request ID found, let Bootstrap's data-bs-toggle handle the modal
        });

        /**
         * Clear validation state when modal inputs change.
         */
        $(document).on('input change', '.modal textarea, .modal select', function () {
            $(this).removeClass('is-invalid');
        });

        /**
         * Clear validation state and form fields when modal is hidden.
         */
        $(document).on('hidden.bs.modal', '.modal', function () {
            var modal = this;
            // Only reset if it's one of our modals
            if (modal.id === MODAL_IDS.approve || 
                modal.id === MODAL_IDS.reject || 
                modal.id === MODAL_IDS.delegate) {
                resetModalForm(modal);
            }
        });

        //////////////////////////////////////////////////////////////////////////////////
        // WebVella Page Builder lifecycle event handlers (commented for reference)
        // These fire during page builder operations to enable custom behavior
        //////////////////////////////////////////////////////////////////////////////////

        // WvPbManager_Design_Loaded: Fired when component enters design mode in page builder
        // document.addEventListener("WvPbManager_Design_Loaded", function (event) {
        //     if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //         console.log("[ApprovalAction] Design mode loaded");
        //         // Initialize any design-time specific functionality here
        //     }
        // });

        // WvPbManager_Design_Unloaded: Fired when component exits design mode in page builder
        // document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
        //     if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //         console.log("[ApprovalAction] Design mode unloaded");
        //         // Cleanup any design-time specific resources here
        //     }
        // });

        // WvPbManager_Options_Loaded: Fired when component options panel is displayed in page builder
        // document.addEventListener("WvPbManager_Options_Loaded", function (event) {
        //     if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //         console.log("[ApprovalAction] Options panel loaded");
        //         // Initialize options panel specific functionality here
        //     }
        // });

        // WvPbManager_Options_Unloaded: Fired when component options panel is closed in page builder
        // document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
        //     if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //         console.log("[ApprovalAction] Options panel unloaded");
        //         // Cleanup options panel specific resources here
        //     }
        // });

        // WvPbManager_Node_Moved: Fired when component is moved/reordered in page builder
        // document.addEventListener("WvPbManager_Node_Moved", function (event) {
        //     if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //         console.log("[ApprovalAction] Component moved in page builder");
        //         // Handle any repositioning logic here if needed
        //     }
        // });

    });

    //////////////////////////////////////////////////////////////////////////////////
    /// Your code is above

})(window, jQuery);
