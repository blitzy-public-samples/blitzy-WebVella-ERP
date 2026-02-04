/**
 * PcApprovalAction Component - Client-side JavaScript Module
 * 
 * Handles AJAX interactions for approval workflow actions including:
 * - Approve: Approves a pending approval request
 * - Reject: Rejects a pending approval request with required comments
 * - Delegate: Delegates an approval request to another user
 * 
 * Dependencies: jQuery 3.x, Bootstrap 5.x, toastr 2.x
 * API Endpoints: /api/v3.0/p/approval/requests/{id}/approve|reject|delegate
 */
"use strict";

(function (window, $) {

    /// Your code goes below
    ///////////////////////////////////////////////////////////////////////////////////

    /**
     * API base path for approval requests
     * @constant {string}
     */
    var API_BASE_PATH = '/api/v3.0/p/approval/requests';

    /**
     * CSS selectors for action buttons
     * @constant {Object}
     */
    var SELECTORS = {
        approveBtn: '.approval-action-approve-btn',
        rejectBtn: '.approval-action-reject-btn',
        delegateBtn: '.approval-action-delegate-btn',
        approveModal: '#approvalApproveModal',
        rejectModal: '#approvalRejectModal',
        delegateModal: '#approvalDelegateModal',
        approveComments: '#approveComments',
        rejectComments: '#rejectComments',
        delegateComments: '#delegateComments',
        delegateUserSelect: '#delegateUserId',
        approveConfirmBtn: '.approval-action-approve-confirm-btn',
        rejectConfirmBtn: '.approval-action-reject-confirm-btn',
        delegateConfirmBtn: '.approval-action-delegate-confirm-btn',
        loadingSpinner: '.approval-loading-spinner',
        requestIdInput: 'input[name="approval_request_id"]'
    };

    /**
     * Toastr notification options
     * @constant {Object}
     */
    var TOASTR_OPTIONS = {
        closeButton: true,
        tapToDismiss: true,
        timeOut: 5000,
        progressBar: true
    };

    /**
     * Enables or disables a button and shows/hides loading spinner
     * @param {jQuery} $button - The button element
     * @param {boolean} isLoading - Whether the button should show loading state
     */
    function setButtonLoadingState($button, isLoading) {
        if (isLoading) {
            $button.prop('disabled', true);
            $button.find('.fa, .fas, .far').addClass('d-none');
            $button.find(SELECTORS.loadingSpinner).removeClass('d-none');
            if ($button.find(SELECTORS.loadingSpinner).length === 0) {
                $button.prepend('<span class="approval-loading-spinner fa fa-spinner fa-spin me-1"></span>');
            }
        } else {
            $button.prop('disabled', false);
            $button.find('.fa, .fas, .far').not(SELECTORS.loadingSpinner).removeClass('d-none');
            $button.find(SELECTORS.loadingSpinner).addClass('d-none');
        }
    }

    /**
     * Sets loading state for all action buttons in a container
     * @param {jQuery} $container - The container element
     * @param {boolean} isLoading - Whether buttons should show loading state
     */
    function setContainerLoadingState($container, isLoading) {
        var $buttons = $container.find(SELECTORS.approveBtn + ', ' + SELECTORS.rejectBtn + ', ' + SELECTORS.delegateBtn);
        $buttons.each(function () {
            setButtonLoadingState($(this), isLoading);
        });
    }

    /**
     * Extracts the request ID from a button element
     * @param {jQuery} $button - The button element
     * @returns {string|null} The request ID or null if not found
     */
    function getRequestIdFromButton($button) {
        var requestId = $button.data('request-id');
        if (!requestId) {
            // Try to find from a nearby hidden input
            var $container = $button.closest('.approval-action-container');
            if ($container.length) {
                var $input = $container.find(SELECTORS.requestIdInput);
                if ($input.length) {
                    requestId = $input.val();
                }
            }
        }
        return requestId || null;
    }

    /**
     * Gets CSRF token from meta tag or hidden input if required
     * @returns {string|null} The CSRF token or null if not found
     */
    function getCsrfToken() {
        var $metaToken = $('meta[name="__RequestVerificationToken"]');
        if ($metaToken.length) {
            return $metaToken.attr('content');
        }
        var $inputToken = $('input[name="__RequestVerificationToken"]');
        if ($inputToken.length) {
            return $inputToken.val();
        }
        return null;
    }

    /**
     * Builds AJAX headers including CSRF token if available
     * @returns {Object} Headers object for AJAX request
     */
    function buildAjaxHeaders() {
        var headers = {
            'Content-Type': 'application/json'
        };
        var csrfToken = getCsrfToken();
        if (csrfToken) {
            headers['RequestVerificationToken'] = csrfToken;
        }
        return headers;
    }

    /**
     * Makes an AJAX POST request to an approval endpoint
     * @param {string} requestId - The approval request ID
     * @param {string} action - The action to perform (approve/reject/delegate)
     * @param {Object} data - The request payload
     * @param {Function} onSuccess - Success callback
     * @param {Function} onError - Error callback
     * @param {Function} onComplete - Complete callback
     */
    function makeApprovalRequest(requestId, action, data, onSuccess, onError, onComplete) {
        var url = API_BASE_PATH + '/' + requestId + '/' + action;

        $.ajax({
            type: 'POST',
            url: url,
            headers: buildAjaxHeaders(),
            data: JSON.stringify(data),
            contentType: 'application/json',
            dataType: 'json',
            success: function (response) {
                if (response && response.success === false) {
                    var errorMessage = response.message || 'An error occurred while processing your request.';
                    if (typeof onError === 'function') {
                        onError(errorMessage);
                    }
                } else {
                    if (typeof onSuccess === 'function') {
                        onSuccess(response);
                    }
                }
            },
            error: function (xhr, textStatus, errorThrown) {
                var errorMessage = 'An error occurred while processing your request.';
                try {
                    if (xhr.responseJSON && xhr.responseJSON.message) {
                        errorMessage = xhr.responseJSON.message;
                    } else if (xhr.responseText) {
                        var response = JSON.parse(xhr.responseText);
                        if (response.message) {
                            errorMessage = response.message;
                        }
                    }
                } catch (e) {
                    errorMessage = textStatus || errorThrown || errorMessage;
                }
                if (typeof onError === 'function') {
                    onError(errorMessage);
                }
            },
            complete: function () {
                if (typeof onComplete === 'function') {
                    onComplete();
                }
            }
        });
    }

    /**
     * Shows a Bootstrap modal
     * @param {string} modalSelector - The modal CSS selector
     */
    function showModal(modalSelector) {
        var $modal = $(modalSelector);
        if ($modal.length) {
            // Bootstrap 5 modal initialization
            var modalInstance = bootstrap.Modal.getOrCreateInstance($modal[0]);
            modalInstance.show();
        }
    }

    /**
     * Hides a Bootstrap modal
     * @param {string} modalSelector - The modal CSS selector
     */
    function hideModal(modalSelector) {
        var $modal = $(modalSelector);
        if ($modal.length) {
            var modalInstance = bootstrap.Modal.getInstance($modal[0]);
            if (modalInstance) {
                modalInstance.hide();
            }
        }
    }

    /**
     * Resets form fields within a modal
     * @param {string} modalSelector - The modal CSS selector
     */
    function resetModalForm(modalSelector) {
        var $modal = $(modalSelector);
        if ($modal.length) {
            $modal.find('textarea').val('');
            $modal.find('select').prop('selectedIndex', 0);
            $modal.find('.is-invalid').removeClass('is-invalid');
            $modal.find('.invalid-feedback').hide();
        }
    }

    /**
     * Validates that a field has a non-empty value
     * @param {jQuery} $field - The field element to validate
     * @param {string} errorMessage - The error message to display
     * @returns {boolean} True if valid, false otherwise
     */
    function validateRequiredField($field, errorMessage) {
        var value = $.trim($field.val());
        if (!value) {
            $field.addClass('is-invalid');
            var $feedback = $field.siblings('.invalid-feedback');
            if ($feedback.length) {
                $feedback.text(errorMessage).show();
            } else {
                $field.after('<div class="invalid-feedback">' + errorMessage + '</div>');
            }
            $field.focus();
            return false;
        }
        $field.removeClass('is-invalid');
        return true;
    }

    /**
     * Handles the approve action
     * @param {string} requestId - The approval request ID
     * @param {string} comments - Optional comments
     * @param {jQuery} $button - The button that triggered the action
     */
    function handleApprove(requestId, comments, $button) {
        var $container = $button.closest('.approval-action-container');
        setContainerLoadingState($container, true);

        var payload = {
            comments: comments || ''
        };

        makeApprovalRequest(
            requestId,
            'approve',
            payload,
            function (response) {
                toastr.success('Request approved successfully.', 'Success', TOASTR_OPTIONS);
                hideModal(SELECTORS.approveModal);
                // Refresh the page to reflect the updated status
                setTimeout(function () {
                    location.reload();
                }, 1000);
            },
            function (errorMessage) {
                toastr.error(errorMessage, 'Error', TOASTR_OPTIONS);
                setContainerLoadingState($container, false);
            },
            function () {
                // Complete callback - loading state handled in success/error
            }
        );
    }

    /**
     * Handles the reject action
     * @param {string} requestId - The approval request ID
     * @param {string} comments - Required rejection reason
     * @param {jQuery} $button - The button that triggered the action
     */
    function handleReject(requestId, comments, $button) {
        var $container = $button.closest('.approval-action-container, .modal');
        setButtonLoadingState($button, true);

        var payload = {
            comments: comments
        };

        makeApprovalRequest(
            requestId,
            'reject',
            payload,
            function (response) {
                toastr.success('Request rejected successfully.', 'Success', TOASTR_OPTIONS);
                hideModal(SELECTORS.rejectModal);
                // Refresh the page to reflect the updated status
                setTimeout(function () {
                    location.reload();
                }, 1000);
            },
            function (errorMessage) {
                toastr.error(errorMessage, 'Error', TOASTR_OPTIONS);
                setButtonLoadingState($button, false);
            },
            function () {
                // Complete callback - loading state handled in success/error
            }
        );
    }

    /**
     * Handles the delegate action
     * @param {string} requestId - The approval request ID
     * @param {string} delegateToUserId - The user ID to delegate to
     * @param {string} comments - Optional comments
     * @param {jQuery} $button - The button that triggered the action
     */
    function handleDelegate(requestId, delegateToUserId, comments, $button) {
        var $container = $button.closest('.approval-action-container, .modal');
        setButtonLoadingState($button, true);

        var payload = {
            delegateToUserId: delegateToUserId,
            comments: comments || ''
        };

        makeApprovalRequest(
            requestId,
            'delegate',
            payload,
            function (response) {
                toastr.success('Request delegated successfully.', 'Success', TOASTR_OPTIONS);
                hideModal(SELECTORS.delegateModal);
                // Refresh the page to reflect the updated status
                setTimeout(function () {
                    location.reload();
                }, 1000);
            },
            function (errorMessage) {
                toastr.error(errorMessage, 'Error', TOASTR_OPTIONS);
                setButtonLoadingState($button, false);
            },
            function () {
                // Complete callback - loading state handled in success/error
            }
        );
    }

    /**
     * DOM-ready event handler
     * Initializes all event listeners for approval actions
     */
    $(function () {

        // Store the current request ID for modal operations
        var currentRequestId = null;

        /**
         * Approve button click handler
         * Shows confirmation modal if configured, or directly approves
         */
        $(document).on('click', SELECTORS.approveBtn, function (e) {
            e.preventDefault();
            try {
                var $button = $(this);
                currentRequestId = getRequestIdFromButton($button);

                if (!currentRequestId) {
                    toastr.error('Request ID not found. Please refresh the page and try again.', 'Error', TOASTR_OPTIONS);
                    return;
                }

                var showConfirmation = $button.data('show-confirmation');
                if (showConfirmation === true || showConfirmation === 'true') {
                    // Store request ID in modal for later use
                    $(SELECTORS.approveModal).data('request-id', currentRequestId);
                    resetModalForm(SELECTORS.approveModal);
                    showModal(SELECTORS.approveModal);
                } else {
                    // Direct approve without modal
                    handleApprove(currentRequestId, '', $button);
                }
            } catch (err) {
                console.error('Error in approve button handler:', err);
                toastr.error('An unexpected error occurred. Please try again.', 'Error', TOASTR_OPTIONS);
            }
        });

        /**
         * Approve confirmation button click handler (from modal)
         */
        $(document).on('click', SELECTORS.approveConfirmBtn, function (e) {
            e.preventDefault();
            try {
                var $button = $(this);
                var $modal = $(SELECTORS.approveModal);
                var requestId = $modal.data('request-id') || currentRequestId;
                var comments = $.trim($(SELECTORS.approveComments).val());

                if (!requestId) {
                    toastr.error('Request ID not found. Please close this dialog and try again.', 'Error', TOASTR_OPTIONS);
                    return;
                }

                handleApprove(requestId, comments, $button);
            } catch (err) {
                console.error('Error in approve confirmation handler:', err);
                toastr.error('An unexpected error occurred. Please try again.', 'Error', TOASTR_OPTIONS);
            }
        });

        /**
         * Reject button click handler
         * Shows modal for rejection reason entry
         */
        $(document).on('click', SELECTORS.rejectBtn, function (e) {
            e.preventDefault();
            try {
                var $button = $(this);
                currentRequestId = getRequestIdFromButton($button);

                if (!currentRequestId) {
                    toastr.error('Request ID not found. Please refresh the page and try again.', 'Error', TOASTR_OPTIONS);
                    return;
                }

                // Store request ID in modal for later use
                $(SELECTORS.rejectModal).data('request-id', currentRequestId);
                resetModalForm(SELECTORS.rejectModal);
                showModal(SELECTORS.rejectModal);
            } catch (err) {
                console.error('Error in reject button handler:', err);
                toastr.error('An unexpected error occurred. Please try again.', 'Error', TOASTR_OPTIONS);
            }
        });

        /**
         * Reject confirmation button click handler (from modal)
         * Validates that comments are provided before submitting
         */
        $(document).on('click', SELECTORS.rejectConfirmBtn, function (e) {
            e.preventDefault();
            try {
                var $button = $(this);
                var $modal = $(SELECTORS.rejectModal);
                var requestId = $modal.data('request-id') || currentRequestId;
                var $commentsField = $(SELECTORS.rejectComments);
                var comments = $.trim($commentsField.val());

                if (!requestId) {
                    toastr.error('Request ID not found. Please close this dialog and try again.', 'Error', TOASTR_OPTIONS);
                    return;
                }

                // Validate that rejection reason is provided
                if (!validateRequiredField($commentsField, 'Please provide a reason for rejection.')) {
                    return;
                }

                handleReject(requestId, comments, $button);
            } catch (err) {
                console.error('Error in reject confirmation handler:', err);
                toastr.error('An unexpected error occurred. Please try again.', 'Error', TOASTR_OPTIONS);
            }
        });

        /**
         * Delegate button click handler
         * Shows modal for delegate user selection
         */
        $(document).on('click', SELECTORS.delegateBtn, function (e) {
            e.preventDefault();
            try {
                var $button = $(this);
                currentRequestId = getRequestIdFromButton($button);

                if (!currentRequestId) {
                    toastr.error('Request ID not found. Please refresh the page and try again.', 'Error', TOASTR_OPTIONS);
                    return;
                }

                // Store request ID in modal for later use
                $(SELECTORS.delegateModal).data('request-id', currentRequestId);
                resetModalForm(SELECTORS.delegateModal);
                showModal(SELECTORS.delegateModal);
            } catch (err) {
                console.error('Error in delegate button handler:', err);
                toastr.error('An unexpected error occurred. Please try again.', 'Error', TOASTR_OPTIONS);
            }
        });

        /**
         * Delegate confirmation button click handler (from modal)
         * Validates that a delegate user is selected before submitting
         */
        $(document).on('click', SELECTORS.delegateConfirmBtn, function (e) {
            e.preventDefault();
            try {
                var $button = $(this);
                var $modal = $(SELECTORS.delegateModal);
                var requestId = $modal.data('request-id') || currentRequestId;
                var $userSelect = $(SELECTORS.delegateUserSelect);
                var delegateToUserId = $userSelect.val();
                var comments = $.trim($(SELECTORS.delegateComments).val());

                if (!requestId) {
                    toastr.error('Request ID not found. Please close this dialog and try again.', 'Error', TOASTR_OPTIONS);
                    return;
                }

                // Validate that a delegate user is selected
                if (!validateRequiredField($userSelect, 'Please select a user to delegate to.')) {
                    return;
                }

                handleDelegate(requestId, delegateToUserId, comments, $button);
            } catch (err) {
                console.error('Error in delegate confirmation handler:', err);
                toastr.error('An unexpected error occurred. Please try again.', 'Error', TOASTR_OPTIONS);
            }
        });

        /**
         * Modal shown event handler
         * Focuses the comment textarea when modal is shown
         */
        $(SELECTORS.approveModal + ', ' + SELECTORS.rejectModal + ', ' + SELECTORS.delegateModal).on('shown.bs.modal', function () {
            var $modal = $(this);
            var $textarea = $modal.find('textarea').first();
            if ($textarea.length) {
                $textarea.focus();
            }
        });

        /**
         * Modal hidden event handler
         * Resets form fields when modal is closed
         */
        $(SELECTORS.approveModal + ', ' + SELECTORS.rejectModal + ', ' + SELECTORS.delegateModal).on('hidden.bs.modal', function () {
            var $modal = $(this);
            resetModalForm('#' + $modal.attr('id'));
            $modal.removeData('request-id');
        });

        /**
         * Clear validation state when user starts typing
         */
        $(document).on('input', SELECTORS.rejectComments + ', ' + SELECTORS.delegateUserSelect, function () {
            $(this).removeClass('is-invalid');
        });

        //	Page Builder Integration Event Listeners (commented out)
        //	These can be enabled when integrating with the WebVella Page Builder

        //	document.addEventListener("WvPbManager_Design_Loaded", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalAction Design loaded");
        //		}
        //	});

        //	document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalAction Design unloaded");
        //		}
        //	});

        //	document.addEventListener("WvPbManager_Options_Loaded", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalAction Options loaded");
        //		}
        //	});

        //	document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalAction Options unloaded");
        //		}
        //	});

        //	document.addEventListener("WvPbManager_Node_Moved", function (event) {
        //		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalAction") {
        //			console.log("WebVella.Erp.Plugins.Approval.Components.PcApprovalAction Moved");
        //		}
        //	});

    });

    //////////////////////////////////////////////////////////////////////////////////
    /// Your code is above

})(window, jQuery);
