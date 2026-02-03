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
        // This function is now handled by openStepsModal via the global WvApprovalWorkflowConfig namespace
        // Directly trigger the openStepsModal function with workflow ID
        if (window.WvApprovalWorkflowConfig && window.WvApprovalWorkflowConfig.openStepsModalDirect) {
            window.WvApprovalWorkflowConfig.openStepsModalDirect(workflowId);
        } else {
            console.warn('[ApprovalWorkflowConfig] openStepsModalDirect not available, finding container...');
            // Try to find any registered component container and open the modal
            for (var containerId in componentStates) {
                if (componentStates.hasOwnProperty(containerId)) {
                    openStepsModal(containerId, workflowId);
                    return;
                }
            }
            console.error('[ApprovalWorkflowConfig] No container found for steps configuration');
        }
    }

    /**
     * Opens the rules configuration modal for a workflow.
     * 
     * @param {string} workflowId - The ID of the workflow.
     */
    function navigateToRulesConfig(workflowId) {
        // This function is now handled by openRulesModal via the global WvApprovalWorkflowConfig namespace
        // Directly trigger the openRulesModal function with workflow ID
        if (window.WvApprovalWorkflowConfig && window.WvApprovalWorkflowConfig.openRulesModalDirect) {
            window.WvApprovalWorkflowConfig.openRulesModalDirect(workflowId);
        } else {
            console.warn('[ApprovalWorkflowConfig] openRulesModalDirect not available, finding container...');
            // Try to find any registered component container and open the modal
            for (var containerId in componentStates) {
                if (componentStates.hasOwnProperty(containerId)) {
                    openRulesModal(containerId, workflowId);
                    return;
                }
            }
            console.error('[ApprovalWorkflowConfig] No container found for rules configuration');
        }
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

    /**
     * Filters workflows by status (all/enabled/disabled).
     * 
     * @param {string} containerId - The unique ID of the container element.
     * @param {string} filter - Filter value: 'all', 'enabled', or 'disabled'.
     */
    function filterByStatus(containerId, filter) {
        var state = componentStates[containerId];
        if (!state || !state.allWorkflows) return;
        
        state.currentFilter = filter;
        
        if (filter === 'all') {
            state.workflows = state.allWorkflows.slice();
        } else if (filter === 'enabled') {
            state.workflows = state.allWorkflows.filter(function(w) { return w.isEnabled === true; });
        } else if (filter === 'disabled') {
            state.workflows = state.allWorkflows.filter(function(w) { return w.isEnabled === false; });
        }
        
        state.currentPage = 1;
        renderWorkflowTable(containerId);
    }

    /**
     * Opens the steps configuration modal for a workflow.
     * 
     * @param {string} containerId - The unique ID of the container element.
     * @param {string} workflowId - The workflow ID.
     */
    function openStepsModal(containerId, workflowId) {
        var state = componentStates[containerId];
        if (!state) return;
        
        var $modal = $('#' + containerId + '-steps-modal');
        if (!$modal.length) {
            console.error('[ApprovalWorkflowConfig] Steps modal not found');
            return;
        }
        
        // Find workflow name
        var workflow = state.workflows.find(function(w) { return w.id === workflowId; }) ||
                       (state.allWorkflows && state.allWorkflows.find(function(w) { return w.id === workflowId; }));
        
        // Set workflow info
        $('#' + containerId + '-steps-workflow-id').val(workflowId);
        $('#' + containerId + '-steps-workflow-name').text(workflow ? workflow.name : 'Workflow');
        
        // Load steps for this workflow
        loadSteps(containerId, workflowId);
        
        // Hide the add/edit form
        $('#' + containerId + '-step-form-container').hide();
        
        // Show modal
        if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
            var modal = new bootstrap.Modal($modal[0]);
            modal.show();
        } else {
            $modal.modal('show');
        }
    }

    /**
     * Opens the rules configuration modal for a workflow.
     * 
     * @param {string} containerId - The unique ID of the container element.
     * @param {string} workflowId - The workflow ID.
     */
    function openRulesModal(containerId, workflowId) {
        var state = componentStates[containerId];
        if (!state) return;
        
        var $modal = $('#' + containerId + '-rules-modal');
        if (!$modal.length) {
            console.error('[ApprovalWorkflowConfig] Rules modal not found');
            return;
        }
        
        // Find workflow name
        var workflow = state.workflows.find(function(w) { return w.id === workflowId; }) ||
                       (state.allWorkflows && state.allWorkflows.find(function(w) { return w.id === workflowId; }));
        
        // Set workflow info
        $('#' + containerId + '-rules-workflow-id').val(workflowId);
        $('#' + containerId + '-rules-workflow-name').text(workflow ? workflow.name : 'Workflow');
        
        // Load rules for this workflow
        loadRules(containerId, workflowId);
        
        // Hide the add/edit form
        $('#' + containerId + '-rule-form-container').hide();
        
        // Show modal
        if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
            var modal = new bootstrap.Modal($modal[0]);
            modal.show();
        } else {
            $modal.modal('show');
        }
    }

    /**
     * Loads steps for a workflow.
     * 
     * @param {string} containerId - The unique ID of the container element.
     * @param {string} workflowId - The workflow ID.
     */
    function loadSteps(containerId, workflowId) {
        var $stepsList = $('#' + containerId + '-steps-list');
        $stepsList.html('<div class="text-muted text-center py-3">Loading steps...</div>');
        
        $.ajax({
            url: API_BASE + '/workflow/' + workflowId + '/steps',
            type: 'GET',
            dataType: 'json',
            success: function(response) {
                if (response && response.success) {
                    renderStepsList(containerId, response.object || []);
                } else {
                    $stepsList.html('<div class="text-danger text-center py-3">' + (response.message || 'Failed to load steps') + '</div>');
                }
            },
            error: function() {
                $stepsList.html('<div class="text-danger text-center py-3">Error loading steps</div>');
            }
        });
    }

    /**
     * Renders the steps list.
     */
    function renderStepsList(containerId, steps) {
        var $stepsList = $('#' + containerId + '-steps-list');
        $stepsList.empty();
        
        if (!steps || steps.length === 0) {
            $stepsList.html('<div class="text-muted text-center py-3">No steps configured yet. Click "Add Step" to create one.</div>');
            return;
        }
        
        steps.sort(function(a, b) { return (a.stepOrder || 0) - (b.stepOrder || 0); });
        
        steps.forEach(function(step) {
            var finalBadge = step.isFinal ? '<span class="badge bg-success ml-2">Final</span>' : '';
            var item = '<div class="list-group-item d-flex justify-content-between align-items-center" data-step-id="' + step.id + '">' +
                '<div>' +
                    '<strong>#' + step.stepOrder + ' - ' + escapeHtml(step.name) + '</strong>' + finalBadge +
                    '<br><small class="text-muted">Approver: ' + step.approverType + (step.timeoutHours ? ', Timeout: ' + step.timeoutHours + 'h' : '') + '</small>' +
                '</div>' +
                '<div class="btn-group btn-group-sm">' +
                    '<button type="button" class="btn btn-outline-primary" onclick="window.WvApprovalWorkflowConfig.editStep(\'' + containerId + '\', \'' + step.id + '\')" title="Edit"><i class="fas fa-edit"></i></button>' +
                    '<button type="button" class="btn btn-outline-danger" onclick="window.WvApprovalWorkflowConfig.deleteStep(\'' + containerId + '\', \'' + step.id + '\')" title="Delete"><i class="fas fa-trash"></i></button>' +
                '</div>' +
            '</div>';
            $stepsList.append(item);
        });
    }

    /**
     * Loads rules for a workflow.
     */
    function loadRules(containerId, workflowId) {
        var $rulesList = $('#' + containerId + '-rules-list');
        $rulesList.html('<div class="text-muted text-center py-3">Loading rules...</div>');
        
        $.ajax({
            url: API_BASE + '/workflow/' + workflowId + '/rules',
            type: 'GET',
            dataType: 'json',
            success: function(response) {
                if (response && response.success) {
                    renderRulesList(containerId, response.object || []);
                } else {
                    $rulesList.html('<div class="text-danger text-center py-3">' + (response.message || 'Failed to load rules') + '</div>');
                }
            },
            error: function() {
                $rulesList.html('<div class="text-danger text-center py-3">Error loading rules</div>');
            }
        });
    }

    /**
     * Renders the rules list.
     */
    function renderRulesList(containerId, rules) {
        var $rulesList = $('#' + containerId + '-rules-list');
        $rulesList.empty();
        
        if (!rules || rules.length === 0) {
            $rulesList.html('<div class="text-muted text-center py-3">No rules configured yet. Click "Add Rule" to create one.</div>');
            return;
        }
        
        rules.sort(function(a, b) { return (b.priority || 0) - (a.priority || 0); });
        
        rules.forEach(function(rule) {
            var item = '<div class="list-group-item d-flex justify-content-between align-items-center" data-rule-id="' + rule.id + '">' +
                '<div>' +
                    '<strong>' + escapeHtml(rule.name) + '</strong>' +
                    '<br><small class="text-muted">' + escapeHtml(rule.fieldName) + ' ' + rule.operator + ' "' + escapeHtml(rule.value) + '" (Priority: ' + (rule.priority || 0) + ')</small>' +
                '</div>' +
                '<div class="btn-group btn-group-sm">' +
                    '<button type="button" class="btn btn-outline-primary" onclick="window.WvApprovalWorkflowConfig.editRule(\'' + containerId + '\', \'' + rule.id + '\')" title="Edit"><i class="fas fa-edit"></i></button>' +
                    '<button type="button" class="btn btn-outline-danger" onclick="window.WvApprovalWorkflowConfig.deleteRule(\'' + containerId + '\', \'' + rule.id + '\')" title="Delete"><i class="fas fa-trash"></i></button>' +
                '</div>' +
            '</div>';
            $rulesList.append(item);
        });
    }

    /**
     * Confirms delete of a workflow.
     */
    function confirmDelete(containerId) {
        var $modal = $('#' + containerId + '-delete-modal');
        var workflowId = $('#' + containerId + '-delete-workflow-id').val();
        if (workflowId) {
            deleteWorkflow(containerId, workflowId);
        }
        if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
            var modal = bootstrap.Modal.getInstance($modal[0]);
            if (modal) modal.hide();
        } else {
            $modal.modal('hide');
        }
    }

    /**
     * Opens the add step form.
     */
    function openAddStepForm(containerId) {
        // Clear form
        $('#' + containerId + '-step-id').val('');
        $('#' + containerId + '-step-name').val('');
        $('#' + containerId + '-step-approver-type').val('role');
        $('#' + containerId + '-step-approver-id').val('');
        $('#' + containerId + '-step-timeout').val('');
        $('#' + containerId + '-step-order').val('1');
        $('#' + containerId + '-step-is-final').prop('checked', false);
        
        // Load approver options dynamically (Issue 4 enhancement)
        loadApproverOptions(containerId);
        
        $('#' + containerId + '-step-form-title').text('Add New Step');
        $('#' + containerId + '-step-form-container').slideDown();
    }

    /**
     * Opens the edit step form.
     */
    function editStep(containerId, stepId) {
        var workflowId = $('#' + containerId + '-steps-workflow-id').val();
        
        // Load approver options dynamically (Issue 4 enhancement)
        loadApproverOptions(containerId);
        
        // Fetch step data
        $.ajax({
            url: API_BASE + '/workflow/' + workflowId + '/steps/' + stepId,
            type: 'GET',
            dataType: 'json',
            success: function(response) {
                if (response && response.success && response.object) {
                    var step = response.object;
                    $('#' + containerId + '-step-id').val(step.id);
                    $('#' + containerId + '-step-name').val(step.name);
                    $('#' + containerId + '-step-approver-type').val(step.approverType);
                    // Set approver ID after a short delay to allow options to load
                    setTimeout(function() {
                        $('#' + containerId + '-step-approver-id').val(step.approverId || '');
                    }, 300);
                    $('#' + containerId + '-step-timeout').val(step.timeoutHours || '');
                    $('#' + containerId + '-step-order').val(step.stepOrder);
                    $('#' + containerId + '-step-is-final').prop('checked', step.isFinal === true);
                    
                    $('#' + containerId + '-step-form-title').text('Edit Step');
                    $('#' + containerId + '-step-form-container').slideDown();
                } else {
                    if (typeof toastr !== 'undefined') toastr.error('Failed to load step data');
                }
            },
            error: function() {
                if (typeof toastr !== 'undefined') toastr.error('Error loading step data');
            }
        });
    }

    /**
     * Saves a step (create or update).
     */
    function saveStep(containerId, event) {
        if (event) event.preventDefault();
        
        var workflowId = $('#' + containerId + '-steps-workflow-id').val();
        var stepId = $('#' + containerId + '-step-id').val();
        
        var stepData = {
            name: $('#' + containerId + '-step-name').val(),
            approverType: $('#' + containerId + '-step-approver-type').val(),
            approverId: $('#' + containerId + '-step-approver-id').val() || null,
            timeoutHours: parseInt($('#' + containerId + '-step-timeout').val()) || null,
            stepOrder: parseInt($('#' + containerId + '-step-order').val()) || 1,
            isFinal: $('#' + containerId + '-step-is-final').is(':checked')
        };
        
        var isUpdate = !!stepId;
        var url = isUpdate 
            ? API_BASE + '/workflow/' + workflowId + '/steps/' + stepId 
            : API_BASE + '/workflow/' + workflowId + '/steps';
        
        $.ajax({
            url: url,
            type: isUpdate ? 'PUT' : 'POST',
            contentType: 'application/json',
            data: JSON.stringify(stepData),
            dataType: 'json',
            success: function(response) {
                if (response && response.success) {
                    if (typeof toastr !== 'undefined') toastr.success(isUpdate ? 'Step updated' : 'Step created');
                    cancelStepForm(containerId);
                    loadSteps(containerId, workflowId);
                } else {
                    if (typeof toastr !== 'undefined') toastr.error(response.message || 'Failed to save step');
                }
            },
            error: function() {
                if (typeof toastr !== 'undefined') toastr.error('Error saving step');
            }
        });
        
        return false;
    }

    /**
     * Cancels the step form.
     */
    function cancelStepForm(containerId) {
        $('#' + containerId + '-step-form-container').slideUp();
    }

    /**
     * Loads roles and users into the approver dropdown.
     * Issue 4: Enhance UX by dynamically populating the approver dropdown
     * instead of requiring users to manually type GUIDs.
     */
    function loadApproverOptions(containerId) {
        var $select = $('#' + containerId + '-step-approver-id');
        var currentValue = $select.val();
        
        // Clear existing options except the placeholder
        $select.find('option:not(:first)').remove();
        
        // Load roles from API
        $.ajax({
            url: '/api/v3.0/en_US/meta/role/list',
            type: 'GET',
            dataType: 'json',
            success: function(response) {
                if (response && response.success && response.object) {
                    var roles = response.object;
                    
                    // Add role optgroup
                    var $roleGroup = $('<optgroup label="Roles"></optgroup>');
                    
                    if (Array.isArray(roles)) {
                        roles.forEach(function(role) {
                            if (role && role.id) {
                                $roleGroup.append(
                                    $('<option></option>')
                                        .val(role.id)
                                        .text('Role: ' + (role.name || 'Unnamed'))
                                );
                            }
                        });
                    }
                    
                    if ($roleGroup.children().length > 0) {
                        $select.append($roleGroup);
                    }
                    
                    // Restore previous value if it was set
                    if (currentValue) {
                        $select.val(currentValue);
                    }
                }
            },
            error: function(xhr, status, error) {
                console.warn('[ApprovalWorkflowConfig] Failed to load roles:', error);
            }
        });
        
        // Load users from API
        $.ajax({
            url: '/api/v3.0/en_US/user/list',
            type: 'GET',
            dataType: 'json',
            success: function(response) {
                if (response && response.success && response.object) {
                    var users = response.object;
                    
                    // Add user optgroup
                    var $userGroup = $('<optgroup label="Users"></optgroup>');
                    
                    if (Array.isArray(users)) {
                        users.forEach(function(user) {
                            if (user && user.id) {
                                $userGroup.append(
                                    $('<option></option>')
                                        .val(user.id)
                                        .text('User: ' + (user.username || user.email || 'Unnamed'))
                                );
                            }
                        });
                    }
                    
                    if ($userGroup.children().length > 0) {
                        $select.append($userGroup);
                    }
                    
                    // Restore previous value if it was set
                    if (currentValue) {
                        $select.val(currentValue);
                    }
                }
            },
            error: function(xhr, status, error) {
                console.warn('[ApprovalWorkflowConfig] Failed to load users:', error);
            }
        });
    }

    /**
     * Deletes a step.
     */
    function deleteStep(containerId, stepId) {
        if (!confirm('Are you sure you want to delete this step?')) return;
        
        var workflowId = $('#' + containerId + '-steps-workflow-id').val();
        
        $.ajax({
            url: API_BASE + '/workflow/' + workflowId + '/steps/' + stepId,
            type: 'DELETE',
            dataType: 'json',
            success: function(response) {
                if (response && response.success) {
                    if (typeof toastr !== 'undefined') toastr.success('Step deleted');
                    loadSteps(containerId, workflowId);
                } else {
                    if (typeof toastr !== 'undefined') toastr.error(response.message || 'Failed to delete step');
                }
            },
            error: function() {
                if (typeof toastr !== 'undefined') toastr.error('Error deleting step');
            }
        });
    }

    /**
     * Opens the add rule form.
     */
    function openAddRuleForm(containerId) {
        // Clear form
        $('#' + containerId + '-rule-id').val('');
        $('#' + containerId + '-rule-name').val('');
        $('#' + containerId + '-rule-field').val('');
        $('#' + containerId + '-rule-operator').val('eq');
        $('#' + containerId + '-rule-value').val('');
        $('#' + containerId + '-rule-priority').val('0');
        
        $('#' + containerId + '-rule-form-title').text('Add New Rule');
        $('#' + containerId + '-rule-form-container').slideDown();
    }

    /**
     * Opens the edit rule form.
     */
    function editRule(containerId, ruleId) {
        var workflowId = $('#' + containerId + '-rules-workflow-id').val();
        
        // Fetch rule data
        $.ajax({
            url: API_BASE + '/workflow/' + workflowId + '/rules/' + ruleId,
            type: 'GET',
            dataType: 'json',
            success: function(response) {
                if (response && response.success && response.object) {
                    var rule = response.object;
                    $('#' + containerId + '-rule-id').val(rule.id);
                    $('#' + containerId + '-rule-name').val(rule.name);
                    // Use snake_case property name (field_name) from API
                    $('#' + containerId + '-rule-field').val(rule.field_name || rule.fieldName);
                    $('#' + containerId + '-rule-operator').val(rule.operator);
                    $('#' + containerId + '-rule-value').val(rule.value);
                    $('#' + containerId + '-rule-priority').val(rule.priority || 0);
                    
                    $('#' + containerId + '-rule-form-title').text('Edit Rule');
                    $('#' + containerId + '-rule-form-container').slideDown();
                } else {
                    if (typeof toastr !== 'undefined') toastr.error('Failed to load rule data');
                }
            },
            error: function() {
                if (typeof toastr !== 'undefined') toastr.error('Error loading rule data');
            }
        });
    }

    /**
     * Saves a rule (create or update).
     */
    function saveRule(containerId, event) {
        if (event) event.preventDefault();
        
        var workflowId = $('#' + containerId + '-rules-workflow-id').val();
        var ruleId = $('#' + containerId + '-rule-id').val();
        
        // Use snake_case property names to match API model (JsonProperty)
        var ruleData = {
            name: $('#' + containerId + '-rule-name').val(),
            field_name: $('#' + containerId + '-rule-field').val(),
            operator: $('#' + containerId + '-rule-operator').val(),
            value: $('#' + containerId + '-rule-value').val(),
            priority: parseInt($('#' + containerId + '-rule-priority').val()) || 0
        };
        
        var isUpdate = !!ruleId;
        var url = isUpdate 
            ? API_BASE + '/workflow/' + workflowId + '/rules/' + ruleId 
            : API_BASE + '/workflow/' + workflowId + '/rules';
        
        $.ajax({
            url: url,
            type: isUpdate ? 'PUT' : 'POST',
            contentType: 'application/json',
            data: JSON.stringify(ruleData),
            dataType: 'json',
            success: function(response) {
                if (response && response.success) {
                    if (typeof toastr !== 'undefined') toastr.success(isUpdate ? 'Rule updated' : 'Rule created');
                    cancelRuleForm(containerId);
                    loadRules(containerId, workflowId);
                } else {
                    if (typeof toastr !== 'undefined') toastr.error(response.message || 'Failed to save rule');
                }
            },
            error: function() {
                if (typeof toastr !== 'undefined') toastr.error('Error saving rule');
            }
        });
        
        return false;
    }

    /**
     * Cancels the rule form.
     */
    function cancelRuleForm(containerId) {
        $('#' + containerId + '-rule-form-container').slideUp();
    }

    /**
     * Deletes a rule.
     */
    function deleteRule(containerId, ruleId) {
        if (!confirm('Are you sure you want to delete this rule?')) return;
        
        var workflowId = $('#' + containerId + '-rules-workflow-id').val();
        
        $.ajax({
            url: API_BASE + '/workflow/' + workflowId + '/rules/' + ruleId,
            type: 'DELETE',
            dataType: 'json',
            success: function(response) {
                if (response && response.success) {
                    if (typeof toastr !== 'undefined') toastr.success('Rule deleted');
                    loadRules(containerId, workflowId);
                } else {
                    if (typeof toastr !== 'undefined') toastr.error(response.message || 'Failed to delete rule');
                }
            },
            error: function() {
                if (typeof toastr !== 'undefined') toastr.error('Error deleting rule');
            }
        });
    }

    // Expose functions globally for external access
    window.initApprovalWorkflowConfig = initApprovalWorkflowConfig;
    window.loadApprovalWorkflows = loadWorkflows;
    window.refreshApprovalWorkflowConfig = loadWorkflows;
    
    // Create global namespace for component functions
    window.WvApprovalWorkflowConfig = {
        init: initApprovalWorkflowConfig,
        loadWorkflows: loadWorkflows,
        filterByStatus: filterByStatus,
        openCreateModal: openCreateModal,
        openEditModal: openEditModal,
        openDeleteModal: openDeleteModal,
        saveWorkflow: saveWorkflow,
        confirmDelete: confirmDelete,
        openStepsModal: openStepsModal,
        openRulesModal: openRulesModal,
        openStepsModalDirect: function(workflowId) {
            // Find first container and open modal
            for (var containerId in componentStates) {
                if (componentStates.hasOwnProperty(containerId)) {
                    openStepsModal(containerId, workflowId);
                    return;
                }
            }
        },
        openRulesModalDirect: function(workflowId) {
            // Find first container and open modal
            for (var containerId in componentStates) {
                if (componentStates.hasOwnProperty(containerId)) {
                    openRulesModal(containerId, workflowId);
                    return;
                }
            }
        },
        loadSteps: loadSteps,
        loadRules: loadRules,
        openAddStepForm: openAddStepForm,
        editStep: editStep,
        saveStep: saveStep,
        cancelStepForm: cancelStepForm,
        deleteStep: deleteStep,
        openAddRuleForm: openAddRuleForm,
        editRule: editRule,
        saveRule: saveRule,
        cancelRuleForm: cancelRuleForm,
        deleteRule: deleteRule
    };

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
