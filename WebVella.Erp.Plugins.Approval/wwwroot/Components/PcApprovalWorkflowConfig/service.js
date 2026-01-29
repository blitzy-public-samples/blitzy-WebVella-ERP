/**
 * PcApprovalWorkflowConfig - Client-side JavaScript Service
 * 
 * Provides AJAX functionality for managing approval workflows including
 * create, update, delete, and status toggle operations.
 * 
 * STORY-003: Workflow Configuration Management
 * STORY-008: UI Components
 * 
 * Uses IIFE pattern for scope isolation as per WebVella conventions.
 */
(function (window, $) {
    'use strict';

    ///////////////////////////////////////////////////////////////////////////////////
    /// Component Script: WebVella.Erp.Plugins.Approval.Components.PcApprovalWorkflowConfig
    /// Your code is below

    /**
     * Base API path for approval workflow operations.
     */
    var API_BASE = '/api/v3.0/p/approval';

    /**
     * Store for tracking component state by container ID.
     */
    var componentStates = {};

    /**
     * Initializes the approval workflow configuration component.
     * Sets up event handlers for CRUD operations and loads initial workflow data.
     * 
     * @param {string} containerId - The unique ID of the container element.
     * @param {object} options - Component configuration options:
     *   - showInactive: Whether to show disabled workflows
     *   - pageSize: Number of items per page
     *   - filterEntityName: Optional entity name filter
     */
    function initApprovalWorkflowConfig(containerId, options) {
        var container = document.getElementById(containerId);
        if (!container) {
            console.error('[ApprovalWorkflowConfig] Container not found:', containerId);
            return;
        }

        // Initialize component state
        componentStates[containerId] = {
            options: options || {},
            workflows: [],
            currentPage: 1,
            editingWorkflowId: null
        };

        console.log('[ApprovalWorkflowConfig] Initializing component:', containerId, options);

        // Set up event handlers
        setupEventHandlers(containerId);

        // Load initial workflow data
        loadWorkflows(containerId);
    }

    /**
     * Sets up all event handlers for the component.
     * Binds click events for buttons, forms, and modal actions.
     * 
     * @param {string} containerId - The unique ID of the container element.
     */
    function setupEventHandlers(containerId) {
        var container = document.getElementById(containerId);
        if (!container) return;

        var $container = $(container);

        // Add Workflow button click handler
        $container.on('click', '.btn-add-workflow', function (e) {
            e.preventDefault();
            openCreateModal(containerId);
        });

        // Edit workflow button click handler
        $container.on('click', '.btn-edit-workflow', function (e) {
            e.preventDefault();
            var workflowId = $(this).data('workflow-id');
            openEditModal(containerId, workflowId);
        });

        // Delete workflow button click handler
        $container.on('click', '.btn-delete-workflow', function (e) {
            e.preventDefault();
            var workflowId = $(this).data('workflow-id');
            var workflowName = $(this).data('workflow-name');
            openDeleteModal(containerId, workflowId, workflowName);
        });

        // Toggle workflow status button click handler
        $container.on('click', '.btn-toggle-workflow', function (e) {
            e.preventDefault();
            var workflowId = $(this).data('workflow-id');
            var currentStatus = $(this).data('workflow-enabled') === true;
            toggleWorkflowStatus(containerId, workflowId, !currentStatus);
        });

        // Configure steps link click handler
        $container.on('click', '.btn-configure-steps', function (e) {
            e.preventDefault();
            var workflowId = $(this).data('workflow-id');
            navigateToStepsConfig(workflowId);
        });

        // Configure rules link click handler
        $container.on('click', '.btn-configure-rules', function (e) {
            e.preventDefault();
            var workflowId = $(this).data('workflow-id');
            navigateToRulesConfig(workflowId);
        });

        // Form submit handler for create/edit modal
        $container.on('submit', '.workflow-form', function (e) {
            e.preventDefault();
            saveWorkflow(containerId);
        });

        // Confirm delete button handler
        $container.on('click', '.btn-confirm-delete', function (e) {
            e.preventDefault();
            var workflowId = $(this).data('workflow-id');
            deleteWorkflow(containerId, workflowId);
        });

        // Refresh button handler
        $container.on('click', '.btn-refresh-workflows', function (e) {
            e.preventDefault();
            loadWorkflows(containerId);
        });

        // Pagination handlers
        $container.on('click', '.pagination-prev', function (e) {
            e.preventDefault();
            changePage(containerId, -1);
        });

        $container.on('click', '.pagination-next', function (e) {
            e.preventDefault();
            changePage(containerId, 1);
        });
    }

    /**
     * Loads workflow data from the API and renders the table.
     * 
     * @param {string} containerId - The unique ID of the container element.
     */
    function loadWorkflows(containerId) {
        var state = componentStates[containerId];
        if (!state) return;

        var container = document.getElementById(containerId);
        if (!container) return;

        $(container).addClass('loading');

        var params = {};
        if (state.options.showInactive) {
            params.includeInactive = true;
        }
        if (state.options.filterEntityName) {
            params.entityName = state.options.filterEntityName;
        }

        $.ajax({
            url: API_BASE + '/workflow',
            type: 'GET',
            data: params,
            dataType: 'json',
            success: function (response) {
                $(container).removeClass('loading');

                if (response && response.success) {
                    state.workflows = response.object || [];
                    renderWorkflowTable(containerId);
                    console.log('[ApprovalWorkflowConfig] Loaded', state.workflows.length, 'workflows');
                } else {
                    console.error('[ApprovalWorkflowConfig] API error:', response.message);
                    if (typeof toastr !== 'undefined') {
                        toastr.error(response.message || 'Failed to load workflows');
                    }
                }
            },
            error: function (xhr, status, error) {
                $(container).removeClass('loading');
                console.error('[ApprovalWorkflowConfig] AJAX error:', status, error);
                if (typeof toastr !== 'undefined') {
                    toastr.error('Failed to load workflows');
                }
            }
        });
    }

    /**
     * Renders the workflow table with the current data.
     * 
     * @param {string} containerId - The unique ID of the container element.
     */
    function renderWorkflowTable(containerId) {
        var state = componentStates[containerId];
        if (!state) return;

        var $tbody = $('#' + containerId + ' .workflow-table tbody');
        if (!$tbody.length) return;

        $tbody.empty();

        if (!state.workflows || state.workflows.length === 0) {
            $tbody.append('<tr><td colspan="7" class="text-center text-muted">No workflows found</td></tr>');
            return;
        }

        var pageSize = state.options.pageSize || 10;
        var startIndex = (state.currentPage - 1) * pageSize;
        var endIndex = Math.min(startIndex + pageSize, state.workflows.length);
        var pageWorkflows = state.workflows.slice(startIndex, endIndex);

        pageWorkflows.forEach(function (workflow) {
            var statusBadge = workflow.isEnabled
                ? '<span class="badge bg-success">Enabled</span>'
                : '<span class="badge bg-secondary">Disabled</span>';

            var createdOn = workflow.createdOn
                ? new Date(workflow.createdOn).toLocaleDateString()
                : 'N/A';

            var row = '<tr data-workflow-id="' + workflow.id + '">' +
                '<td>' + escapeHtml(workflow.name) + '</td>' +
                '<td>' + escapeHtml(workflow.targetEntityName) + '</td>' +
                '<td>' + statusBadge + '</td>' +
                '<td>' + (workflow.stepsCount || 0) + '</td>' +
                '<td>' + (workflow.rulesCount || 0) + '</td>' +
                '<td>' + createdOn + '</td>' +
                '<td class="actions">' +
                    '<button type="button" class="btn btn-sm btn-outline-primary btn-edit-workflow" ' +
                        'data-workflow-id="' + workflow.id + '" title="Edit">' +
                        '<i class="fas fa-edit"></i>' +
                    '</button> ' +
                    '<button type="button" class="btn btn-sm btn-outline-secondary btn-configure-steps" ' +
                        'data-workflow-id="' + workflow.id + '" title="Configure Steps">' +
                        '<i class="fas fa-list-ol"></i>' +
                    '</button> ' +
                    '<button type="button" class="btn btn-sm btn-outline-secondary btn-configure-rules" ' +
                        'data-workflow-id="' + workflow.id + '" title="Configure Rules">' +
                        '<i class="fas fa-code-branch"></i>' +
                    '</button> ' +
                    '<button type="button" class="btn btn-sm btn-outline-warning btn-toggle-workflow" ' +
                        'data-workflow-id="' + workflow.id + '" ' +
                        'data-workflow-enabled="' + workflow.isEnabled + '" ' +
                        'title="' + (workflow.isEnabled ? 'Disable' : 'Enable') + '">' +
                        '<i class="fas fa-' + (workflow.isEnabled ? 'pause' : 'play') + '"></i>' +
                    '</button> ' +
                    '<button type="button" class="btn btn-sm btn-outline-danger btn-delete-workflow" ' +
                        'data-workflow-id="' + workflow.id + '" ' +
                        'data-workflow-name="' + escapeHtml(workflow.name) + '" title="Delete">' +
                        '<i class="fas fa-trash"></i>' +
                    '</button>' +
                '</td>' +
                '</tr>';

            $tbody.append(row);
        });

        // Update pagination info
        updatePagination(containerId, state.workflows.length, pageSize);
    }

    /**
     * Updates pagination controls based on current state.
     * 
     * @param {string} containerId - The unique ID of the container element.
     * @param {number} totalItems - Total number of workflow items.
     * @param {number} pageSize - Number of items per page.
     */
    function updatePagination(containerId, totalItems, pageSize) {
        var state = componentStates[containerId];
        if (!state) return;

        var totalPages = Math.ceil(totalItems / pageSize) || 1;
        var $pagination = $('#' + containerId + ' .pagination-info');
        
        if ($pagination.length) {
            $pagination.text('Page ' + state.currentPage + ' of ' + totalPages + ' (' + totalItems + ' total)');
        }

        var $prevBtn = $('#' + containerId + ' .pagination-prev');
        var $nextBtn = $('#' + containerId + ' .pagination-next');

        if ($prevBtn.length) {
            $prevBtn.prop('disabled', state.currentPage <= 1);
        }
        if ($nextBtn.length) {
            $nextBtn.prop('disabled', state.currentPage >= totalPages);
        }
    }

    /**
     * Changes the current page of the workflow list.
     * 
     * @param {string} containerId - The unique ID of the container element.
     * @param {number} delta - Page change direction (-1 or 1).
     */
    function changePage(containerId, delta) {
        var state = componentStates[containerId];
        if (!state) return;

        var pageSize = state.options.pageSize || 10;
        var totalPages = Math.ceil(state.workflows.length / pageSize) || 1;
        var newPage = state.currentPage + delta;

        if (newPage >= 1 && newPage <= totalPages) {
            state.currentPage = newPage;
            renderWorkflowTable(containerId);
        }
    }

    /**
     * Opens the create workflow modal with empty form.
     * 
     * @param {string} containerId - The unique ID of the container element.
     */
    function openCreateModal(containerId) {
        var state = componentStates[containerId];
        if (!state) return;

        state.editingWorkflowId = null;

        var $modal = $('#' + containerId + '-workflow-modal');
        if (!$modal.length) {
            console.error('[ApprovalWorkflowConfig] Modal not found');
            return;
        }

        // Reset form
        $modal.find('.modal-title').text('Add Workflow');
        $modal.find('input[name="name"]').val('');
        $modal.find('input[name="targetEntityName"]').val('');
        $modal.find('input[name="isEnabled"]').prop('checked', true);

        // Show modal
        if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
            var modal = new bootstrap.Modal($modal[0]);
            modal.show();
        } else {
            $modal.modal('show');
        }
    }

    /**
     * Opens the edit workflow modal with existing data.
     * 
     * @param {string} containerId - The unique ID of the container element.
     * @param {string} workflowId - The ID of the workflow to edit.
     */
    function openEditModal(containerId, workflowId) {
        var state = componentStates[containerId];
        if (!state) return;

        var workflow = state.workflows.find(function (w) { return w.id === workflowId; });
        if (!workflow) {
            console.error('[ApprovalWorkflowConfig] Workflow not found:', workflowId);
            return;
        }

        state.editingWorkflowId = workflowId;

        var $modal = $('#' + containerId + '-workflow-modal');
        if (!$modal.length) {
            console.error('[ApprovalWorkflowConfig] Modal not found');
            return;
        }

        // Populate form
        $modal.find('.modal-title').text('Edit Workflow');
        $modal.find('input[name="name"]').val(workflow.name);
        $modal.find('input[name="targetEntityName"]').val(workflow.targetEntityName);
        $modal.find('input[name="isEnabled"]').prop('checked', workflow.isEnabled);

        // Show modal
        if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
            var modal = new bootstrap.Modal($modal[0]);
            modal.show();
        } else {
            $modal.modal('show');
        }
    }

    /**
     * Opens the delete confirmation modal.
     * 
     * @param {string} containerId - The unique ID of the container element.
     * @param {string} workflowId - The ID of the workflow to delete.
     * @param {string} workflowName - The name of the workflow for confirmation display.
     */
    function openDeleteModal(containerId, workflowId, workflowName) {
        var $modal = $('#' + containerId + '-delete-modal');
        if (!$modal.length) {
            console.error('[ApprovalWorkflowConfig] Delete modal not found');
            return;
        }

        $modal.find('.workflow-name-display').text(workflowName);
        $modal.find('.btn-confirm-delete').data('workflow-id', workflowId);

        // Show modal
        if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
            var modal = new bootstrap.Modal($modal[0]);
            modal.show();
        } else {
            $modal.modal('show');
        }
    }

    /**
     * Saves the workflow (create or update based on state).
     * 
     * @param {string} containerId - The unique ID of the container element.
     */
    function saveWorkflow(containerId) {
        var state = componentStates[containerId];
        if (!state) return;

        var $modal = $('#' + containerId + '-workflow-modal');
        var formData = {
            name: $modal.find('input[name="name"]').val(),
            targetEntityName: $modal.find('input[name="targetEntityName"]').val(),
            isEnabled: $modal.find('input[name="isEnabled"]').is(':checked')
        };

        // Validate
        if (!formData.name || formData.name.trim() === '') {
            if (typeof toastr !== 'undefined') {
                toastr.warning('Please enter a workflow name');
            }
            return;
        }

        if (!formData.targetEntityName || formData.targetEntityName.trim() === '') {
            if (typeof toastr !== 'undefined') {
                toastr.warning('Please enter a target entity name');
            }
            return;
        }

        var isUpdate = !!state.editingWorkflowId;
        var url = API_BASE + '/workflow';
        var method = 'POST';

        if (isUpdate) {
            url = API_BASE + '/workflow/' + state.editingWorkflowId;
            method = 'PUT';
            formData.id = state.editingWorkflowId;
        }

        $modal.find('.btn-save').prop('disabled', true);

        $.ajax({
            url: url,
            type: method,
            contentType: 'application/json',
            data: JSON.stringify(formData),
            dataType: 'json',
            success: function (response) {
                $modal.find('.btn-save').prop('disabled', false);

                if (response && response.success) {
                    // Close modal
                    if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
                        var modal = bootstrap.Modal.getInstance($modal[0]);
                        if (modal) modal.hide();
                    } else {
                        $modal.modal('hide');
                    }

                    // Reload workflows
                    loadWorkflows(containerId);

                    if (typeof toastr !== 'undefined') {
                        toastr.success(isUpdate ? 'Workflow updated successfully' : 'Workflow created successfully');
                    }
                } else {
                    console.error('[ApprovalWorkflowConfig] Save error:', response.message);
                    if (typeof toastr !== 'undefined') {
                        toastr.error(response.message || 'Failed to save workflow');
                    }
                }
            },
            error: function (xhr, status, error) {
                $modal.find('.btn-save').prop('disabled', false);
                console.error('[ApprovalWorkflowConfig] Save AJAX error:', status, error);
                if (typeof toastr !== 'undefined') {
                    toastr.error('Failed to save workflow');
                }
            }
        });
    }

    /**
     * Deletes a workflow by ID.
     * 
     * @param {string} containerId - The unique ID of the container element.
     * @param {string} workflowId - The ID of the workflow to delete.
     */
    function deleteWorkflow(containerId, workflowId) {
        var $modal = $('#' + containerId + '-delete-modal');
        $modal.find('.btn-confirm-delete').prop('disabled', true);

        $.ajax({
            url: API_BASE + '/workflow/' + workflowId,
            type: 'DELETE',
            dataType: 'json',
            success: function (response) {
                $modal.find('.btn-confirm-delete').prop('disabled', false);

                if (response && response.success) {
                    // Close modal
                    if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
                        var modal = bootstrap.Modal.getInstance($modal[0]);
                        if (modal) modal.hide();
                    } else {
                        $modal.modal('hide');
                    }

                    // Reload workflows
                    loadWorkflows(containerId);

                    if (typeof toastr !== 'undefined') {
                        toastr.success('Workflow deleted successfully');
                    }
                } else {
                    console.error('[ApprovalWorkflowConfig] Delete error:', response.message);
                    if (typeof toastr !== 'undefined') {
                        toastr.error(response.message || 'Failed to delete workflow');
                    }
                }
            },
            error: function (xhr, status, error) {
                $modal.find('.btn-confirm-delete').prop('disabled', false);
                console.error('[ApprovalWorkflowConfig] Delete AJAX error:', status, error);
                if (typeof toastr !== 'undefined') {
                    toastr.error('Failed to delete workflow');
                }
            }
        });
    }

    /**
     * Toggles the enabled status of a workflow.
     * 
     * @param {string} containerId - The unique ID of the container element.
     * @param {string} workflowId - The ID of the workflow to toggle.
     * @param {boolean} newStatus - The new enabled status.
     */
    function toggleWorkflowStatus(containerId, workflowId, newStatus) {
        var state = componentStates[containerId];
        if (!state) return;

        var workflow = state.workflows.find(function (w) { return w.id === workflowId; });
        if (!workflow) return;

        $.ajax({
            url: API_BASE + '/workflow/' + workflowId,
            type: 'PUT',
            contentType: 'application/json',
            data: JSON.stringify({
                id: workflowId,
                name: workflow.name,
                targetEntityName: workflow.targetEntityName,
                isEnabled: newStatus
            }),
            dataType: 'json',
            success: function (response) {
                if (response && response.success) {
                    // Reload workflows
                    loadWorkflows(containerId);

                    if (typeof toastr !== 'undefined') {
                        toastr.success('Workflow ' + (newStatus ? 'enabled' : 'disabled') + ' successfully');
                    }
                } else {
                    console.error('[ApprovalWorkflowConfig] Toggle error:', response.message);
                    if (typeof toastr !== 'undefined') {
                        toastr.error(response.message || 'Failed to update workflow status');
                    }
                }
            },
            error: function (xhr, status, error) {
                console.error('[ApprovalWorkflowConfig] Toggle AJAX error:', status, error);
                if (typeof toastr !== 'undefined') {
                    toastr.error('Failed to update workflow status');
                }
            }
        });
    }

    /**
     * Navigates to the steps configuration page for a workflow.
     * 
     * @param {string} workflowId - The ID of the workflow.
     */
    function navigateToStepsConfig(workflowId) {
        // Navigate to steps configuration - URL pattern may vary based on application routing
        window.location.href = '/approval/workflow/' + workflowId + '/steps';
    }

    /**
     * Navigates to the rules configuration page for a workflow.
     * 
     * @param {string} workflowId - The ID of the workflow.
     */
    function navigateToRulesConfig(workflowId) {
        // Navigate to rules configuration - URL pattern may vary based on application routing
        window.location.href = '/approval/workflow/' + workflowId + '/rules';
    }

    /**
     * Escapes HTML special characters to prevent XSS.
     * 
     * @param {string} text - The text to escape.
     * @returns {string} - The escaped text.
     */
    function escapeHtml(text) {
        if (!text) return '';
        var map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"']/g, function (m) { return map[m]; });
    }

    // Expose functions globally for external access
    window.initApprovalWorkflowConfig = initApprovalWorkflowConfig;
    window.loadApprovalWorkflows = loadWorkflows;
    window.refreshApprovalWorkflowConfig = loadWorkflows;

    /**
     * DOM Ready Handler
     * Sets up WebVella Page Builder lifecycle event listeners.
     */
    $(function () {

        /**
         * WvPbManager_Design_Loaded: Fired when a component enters design mode in the page builder.
         * Initializes design-time specific functionality for the workflow configuration component.
         */
        document.addEventListener("WvPbManager_Design_Loaded", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalWorkflowConfig") {
                console.log("[ApprovalWorkflowConfig] Design mode loaded");
                // Initialize design-time preview with sample data
                var nodeId = event.payload.node_id;
                if (nodeId) {
                    var $component = $('[data-node-id="' + nodeId + '"]');
                    if ($component.length) {
                        $component.addClass('wv-pb-design-mode');
                    }
                }
            }
        });

        /**
         * WvPbManager_Design_Unloaded: Fired when a component exits design mode.
         * Cleans up any design-time resources and state.
         */
        document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalWorkflowConfig") {
                console.log("[ApprovalWorkflowConfig] Design mode unloaded");
                // Clean up design-time resources
                var nodeId = event.payload.node_id;
                if (nodeId) {
                    var $component = $('[data-node-id="' + nodeId + '"]');
                    if ($component.length) {
                        $component.removeClass('wv-pb-design-mode');
                    }
                }
            }
        });

        /**
         * WvPbManager_Options_Loaded: Fired when the component options panel is displayed.
         * Sets up options-specific UI behavior and event handlers.
         */
        document.addEventListener("WvPbManager_Options_Loaded", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalWorkflowConfig") {
                console.log("[ApprovalWorkflowConfig] Options panel loaded");
                // Initialize options panel interactions
                var optionsContainer = document.querySelector('.wv-pb-options-panel');
                if (optionsContainer) {
                    // Set up entity name autocomplete or select if available
                    var entityNameInput = optionsContainer.querySelector('input[name="filterEntityName"]');
                    if (entityNameInput) {
                        // Add input validation for entity name
                        $(entityNameInput).on('blur', function () {
                            var value = $(this).val();
                            if (value && !/^[a-z_][a-z0-9_]*$/.test(value)) {
                                if (typeof toastr !== 'undefined') {
                                    toastr.warning('Entity name should use snake_case format');
                                }
                            }
                        });
                    }
                }
            }
        });

        /**
         * WvPbManager_Options_Unloaded: Fired when the component options panel is closed.
         * Saves any pending changes and cleans up resources.
         */
        document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalWorkflowConfig") {
                console.log("[ApprovalWorkflowConfig] Options panel unloaded");
                // Clean up options panel event handlers
                var optionsContainer = document.querySelector('.wv-pb-options-panel');
                if (optionsContainer) {
                    var entityNameInput = optionsContainer.querySelector('input[name="filterEntityName"]');
                    if (entityNameInput) {
                        $(entityNameInput).off('blur');
                    }
                }
            }
        });

        /**
         * WvPbManager_Node_Moved: Fired when a component is moved within the page builder.
         * Handles any re-initialization needed after the component moves.
         */
        document.addEventListener("WvPbManager_Node_Moved", function (event) {
            if (event && event.payload && event.payload.component_name === "WebVella.Erp.Plugins.Approval.Components.PcApprovalWorkflowConfig") {
                console.log("[ApprovalWorkflowConfig] Component moved in page builder");
                // Re-initialize component after move if needed
                var nodeId = event.payload.node_id;
                if (nodeId) {
                    var containerId = $('[data-node-id="' + nodeId + '"]').find('.approval-workflow-config-container').attr('id');
                    if (containerId && componentStates[containerId]) {
                        // Refresh the component data after move
                        loadWorkflows(containerId);
                    }
                }
            }
        });

    });

    //////////////////////////////////////////////////////////////////////////////////
    /// Your code is above

})(window, jQuery);
