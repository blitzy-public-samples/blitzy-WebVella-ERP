/**
 * service.js - Client-side JavaScript for PcApprovalAction component
 * 
 * Provides AJAX functionality for approval actions (approve, reject, delegate).
 * Implemented as IIFE (Immediately Invoked Function Expression) following WebVella patterns.
 * 
 * Dependencies:
 * - jQuery (for AJAX and DOM manipulation)
 * - Bootstrap 5 (for modal handling)
 * - toastr (optional, for toast notifications)
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
     * Approves an approval request.
     * Sends a POST request to the approval API endpoint with optional comments.
     * 
     * @param {string} requestId - The GUID of the approval request to approve.
     * @param {string} comments - Optional comments to include with the approval.
     * @param {string} modalId - The ID of the Bootstrap modal to close on success.
     */
    window.WvApprovalAction.approveRequest = function (requestId, comments, modalId) {
        performApprovalAction(
            '/api/v3.0/p/approval/request/' + requestId + '/approve',
            { comments: comments || '' },
            modalId,
            'Request approved successfully!'
        );
    };

    /**
     * Rejects an approval request.
     * Sends a POST request to the rejection API endpoint with reason and comments.
     * 
     * @param {string} requestId - The GUID of the approval request to reject.
     * @param {string} reason - The reason for rejection (required).
     * @param {string} comments - Optional additional comments.
     * @param {string} modalId - The ID of the Bootstrap modal to close on success.
     */
    window.WvApprovalAction.rejectRequest = function (requestId, reason, comments, modalId) {
        if (!reason || reason.trim() === '') {
            if (typeof toastr !== 'undefined') {
                toastr.error('Please provide a reason for rejection.');
            } else {
                alert('Please provide a reason for rejection.');
            }
            return;
        }

        performApprovalAction(
            '/api/v3.0/p/approval/request/' + requestId + '/reject',
            { reason: reason, comments: comments || '' },
            modalId,
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
     * @param {string} modalId - The ID of the Bootstrap modal to close on success.
     */
    window.WvApprovalAction.delegateRequest = function (requestId, delegateToUserId, comments, modalId) {
        if (!delegateToUserId || delegateToUserId.trim() === '') {
            if (typeof toastr !== 'undefined') {
                toastr.error('Please select a user to delegate to.');
            } else {
                alert('Please select a user to delegate to.');
            }
            return;
        }

        performApprovalAction(
            '/api/v3.0/p/approval/request/' + requestId + '/delegate',
            { delegateToUserId: delegateToUserId, comments: comments || '' },
            modalId,
            'Request delegated successfully!'
        );
    };

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
        var originalText = btn ? btn.innerHTML : '';

        // Show loading state on button
        if (btn) {
            btn.disabled = true;
            btn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Processing...';
        }

        // Perform AJAX request using jQuery
        $.ajax({
            url: url,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            success: function (response) {
                // Check if response indicates success
                if (response.success === false) {
                    handleError(response.message || 'An error occurred', btn, originalText);
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
                        // Use default error message
                    }
                }

                handleError(errorMessage, btn, originalText);
            }
        });
    }

    /**
     * Handles error display and button state restoration.
     * 
     * @param {string} message - The error message to display.
     * @param {Element} btn - The button element to restore.
     * @param {string} originalText - The original button HTML content.
     */
    function handleError(message, btn, originalText) {
        console.error('[ApprovalAction] Error:', message);

        // Show error notification
        showNotification('error', message);

        // Re-enable the button
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = originalText;
        }
    }

    /**
     * Closes a Bootstrap 5 modal by its ID.
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
            alert(message);
        }
    }

    /**
     * DOM Ready Handler
     * Sets up event handlers and WebVella Page Builder lifecycle listeners.
     */
    $(function () {

        // Global click handler for approve buttons (for dynamically added elements)
        $(document).on('click', '.btn-approve', function (e) {
            // Let the button's native data-bs-toggle handle modal opening
            // The actual approval is handled by the confirm-approve button click
        });

        // Global click handler for reject buttons
        $(document).on('click', '.btn-reject', function (e) {
            // Let the button's native data-bs-toggle handle modal opening
        });

        // Global click handler for delegate buttons
        $(document).on('click', '.btn-delegate', function (e) {
            // Let the button's native data-bs-toggle handle modal opening
        });

        // Clear validation state when modal inputs change
        $(document).on('input change', '.modal textarea, .modal select', function () {
            $(this).removeClass('is-invalid');
        });

        // Clear validation state when modal is hidden
        $(document).on('hidden.bs.modal', '.modal', function () {
            $(this).find('.is-invalid').removeClass('is-invalid');
            $(this).find('textarea').val('');
            $(this).find('select').val('');
        });

        // WebVella Page Builder lifecycle event handlers (commented for reference)
        // These fire during page builder operations

        // WvPbManager_Design_Loaded: Fired when component enters design mode
        // document.addEventListener("WvPbManager_Design_Loaded", function (event) {
        //     if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //         console.log("[ApprovalAction] Design mode loaded");
        //     }
        // });

        // WvPbManager_Design_Unloaded: Fired when component exits design mode
        // document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
        //     if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //         console.log("[ApprovalAction] Design mode unloaded");
        //     }
        // });

        // WvPbManager_Options_Loaded: Fired when options panel is displayed
        // document.addEventListener("WvPbManager_Options_Loaded", function (event) {
        //     if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //         console.log("[ApprovalAction] Options panel loaded");
        //     }
        // });

        // WvPbManager_Options_Unloaded: Fired when options panel is closed
        // document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
        //     if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //         console.log("[ApprovalAction] Options panel unloaded");
        //     }
        // });

        // WvPbManager_Node_Moved: Fired when component is moved in page builder
        document.addEventListener("WvPbManager_Node_Moved", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
                console.log("[ApprovalAction] Component moved in page builder");
            }
        });

    });

    //////////////////////////////////////////////////////////////////////////////////
    /// Your code is above

})(window, jQuery);
