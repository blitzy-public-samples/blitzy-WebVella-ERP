"use strict";

/**
 * PcApprovalWorkflowConfig Component Service
 * Client-side JavaScript for workflow configuration management.
 * Provides AJAX handlers for workflow, step, and rule CRUD operations.
 * Follows WebVella jQuery IIFE pattern with page-builder lifecycle hooks.
 */
(function (window, $) {

    // Component name constant for event filtering
    var COMPONENT_NAME = "WebVella.Erp.Plugins.Approval.Components.PcApprovalWorkflowConfig";

    // API base URL for approval endpoints
    var API_BASE_URL = "/api/v3.0/p/approval";

    /**
     * ApprovalWorkflowConfigService - Handles all workflow configuration operations
     */
    var ApprovalWorkflowConfigService = {

        /**
         * Initialize the service with component context
         * @param {Object} context - Component context from page builder
         */
        init: function (context) {
            this.context = context;
            this.bindEventHandlers();
        },

        /**
         * Clean up event handlers and references
         */
        destroy: function () {
            this.unbindEventHandlers();
            this.context = null;
        },

        /**
         * Bind DOM event handlers for the component
         */
        bindEventHandlers: function () {
            var self = this;

            // Workflow list action handlers
            $(document).on("click.approvalWorkflowConfig", "[data-action='create-workflow']", function (e) {
                e.preventDefault();
                self.showWorkflowModal(null);
            });

            $(document).on("click.approvalWorkflowConfig", "[data-action='edit-workflow']", function (e) {
                e.preventDefault();
                var workflowId = $(this).data("workflow-id");
                self.showWorkflowModal(workflowId);
            });

            $(document).on("click.approvalWorkflowConfig", "[data-action='delete-workflow']", function (e) {
                e.preventDefault();
                var workflowId = $(this).data("workflow-id");
                var workflowName = $(this).data("workflow-name");
                self.confirmDeleteWorkflow(workflowId, workflowName);
            });

            // Step action handlers
            $(document).on("click.approvalWorkflowConfig", "[data-action='create-step']", function (e) {
                e.preventDefault();
                var workflowId = $(this).data("workflow-id");
                self.showStepModal(workflowId, null);
            });

            $(document).on("click.approvalWorkflowConfig", "[data-action='edit-step']", function (e) {
                e.preventDefault();
                var workflowId = $(this).data("workflow-id");
                var stepId = $(this).data("step-id");
                self.showStepModal(workflowId, stepId);
            });

            $(document).on("click.approvalWorkflowConfig", "[data-action='delete-step']", function (e) {
                e.preventDefault();
                var stepId = $(this).data("step-id");
                var stepName = $(this).data("step-name");
                self.confirmDeleteStep(stepId, stepName);
            });

            // Rule action handlers
            $(document).on("click.approvalWorkflowConfig", "[data-action='create-rule']", function (e) {
                e.preventDefault();
                var stepId = $(this).data("step-id");
                self.showRuleModal(stepId, null);
            });

            $(document).on("click.approvalWorkflowConfig", "[data-action='edit-rule']", function (e) {
                e.preventDefault();
                var stepId = $(this).data("step-id");
                var ruleId = $(this).data("rule-id");
                self.showRuleModal(stepId, ruleId);
            });

            $(document).on("click.approvalWorkflowConfig", "[data-action='delete-rule']", function (e) {
                e.preventDefault();
                var ruleId = $(this).data("rule-id");
                self.confirmDeleteRule(ruleId);
            });

            // Form submission handlers
            $(document).on("submit.approvalWorkflowConfig", "#workflow-form", function (e) {
                e.preventDefault();
                self.saveWorkflowFromForm($(this));
            });

            $(document).on("submit.approvalWorkflowConfig", "#step-form", function (e) {
                e.preventDefault();
                self.saveStepFromForm($(this));
            });

            $(document).on("submit.approvalWorkflowConfig", "#rule-form", function (e) {
                e.preventDefault();
                self.saveRuleFromForm($(this));
            });

            // Sortable step reordering (if jQuery UI sortable is available)
            if ($.fn.sortable) {
                $(".step-list-sortable").sortable({
                    handle: ".step-drag-handle",
                    update: function (event, ui) {
                        var workflowId = $(this).data("workflow-id");
                        var stepIds = $(this).sortable("toArray", { attribute: "data-step-id" });
                        self.reorderSteps(workflowId, stepIds);
                    }
                });
            }
        },

        /**
         * Unbind all event handlers
         */
        unbindEventHandlers: function () {
            $(document).off(".approvalWorkflowConfig");
            if ($.fn.sortable && $(".step-list-sortable").length) {
                $(".step-list-sortable").sortable("destroy");
            }
        },

        // ============================================================================
        // WORKFLOW CRUD OPERATIONS
        // ============================================================================

        /**
         * Load all workflows from the server
         * @param {Function} callback - Callback function with (error, workflows) signature
         */
        loadWorkflows: function (callback) {
            $.ajax({
                url: API_BASE_URL + "/workflows",
                type: "GET",
                dataType: "json",
                success: function (response) {
                    if (response && response.success) {
                        callback(null, response.object || []);
                    } else {
                        var errorMsg = response && response.message ? response.message : "Failed to load workflows";
                        callback(new Error(errorMsg), null);
                    }
                },
                error: function (xhr, status, error) {
                    console.error("Error loading workflows:", error);
                    callback(new Error("Network error while loading workflows"), null);
                }
            });
        },

        /**
         * Load a single workflow by ID
         * @param {string} workflowId - The workflow GUID
         * @param {Function} callback - Callback function with (error, workflow) signature
         */
        loadWorkflow: function (workflowId, callback) {
            $.ajax({
                url: API_BASE_URL + "/workflows/" + workflowId,
                type: "GET",
                dataType: "json",
                success: function (response) {
                    if (response && response.success) {
                        callback(null, response.object);
                    } else {
                        var errorMsg = response && response.message ? response.message : "Failed to load workflow";
                        callback(new Error(errorMsg), null);
                    }
                },
                error: function (xhr, status, error) {
                    console.error("Error loading workflow:", error);
                    callback(new Error("Network error while loading workflow"), null);
                }
            });
        },

        /**
         * Save a workflow (create or update)
         * @param {Object} data - Workflow data object
         * @param {Function} callback - Callback function with (error, workflow) signature
         */
        saveWorkflow: function (data, callback) {
            var isUpdate = data.id && data.id !== "00000000-0000-0000-0000-000000000000";
            var url = isUpdate ? API_BASE_URL + "/workflows/" + data.id : API_BASE_URL + "/workflows";
            var method = isUpdate ? "PUT" : "POST";

            $.ajax({
                url: url,
                type: method,
                contentType: "application/json",
                data: JSON.stringify(data),
                dataType: "json",
                success: function (response) {
                    if (response && response.success) {
                        var msg = isUpdate ? "Workflow updated successfully" : "Workflow created successfully";
                        toastr.success(msg);
                        callback(null, response.object);
                    } else {
                        var errorMsg = response && response.message ? response.message : "Failed to save workflow";
                        toastr.error(errorMsg);
                        callback(new Error(errorMsg), null);
                    }
                },
                error: function (xhr, status, error) {
                    console.error("Error saving workflow:", error);
                    toastr.error("Network error while saving workflow");
                    callback(new Error("Network error while saving workflow"), null);
                }
            });
        },

        /**
         * Delete a workflow by ID
         * @param {string} workflowId - The workflow GUID
         * @param {Function} callback - Callback function with (error) signature
         */
        deleteWorkflow: function (workflowId, callback) {
            $.ajax({
                url: API_BASE_URL + "/workflows/" + workflowId,
                type: "DELETE",
                dataType: "json",
                success: function (response) {
                    if (response && response.success) {
                        toastr.success("Workflow deleted successfully");
                        callback(null);
                    } else {
                        var errorMsg = response && response.message ? response.message : "Failed to delete workflow";
                        toastr.error(errorMsg);
                        callback(new Error(errorMsg));
                    }
                },
                error: function (xhr, status, error) {
                    console.error("Error deleting workflow:", error);
                    toastr.error("Network error while deleting workflow");
                    callback(new Error("Network error while deleting workflow"));
                }
            });
        },

        // ============================================================================
        // STEP CRUD OPERATIONS
        // ============================================================================

        /**
         * Load all steps for a workflow
         * @param {string} workflowId - The workflow GUID
         * @param {Function} callback - Callback function with (error, steps) signature
         */
        loadSteps: function (workflowId, callback) {
            $.ajax({
                url: API_BASE_URL + "/workflows/" + workflowId + "/steps",
                type: "GET",
                dataType: "json",
                success: function (response) {
                    if (response && response.success) {
                        callback(null, response.object || []);
                    } else {
                        var errorMsg = response && response.message ? response.message : "Failed to load steps";
                        callback(new Error(errorMsg), null);
                    }
                },
                error: function (xhr, status, error) {
                    console.error("Error loading steps:", error);
                    callback(new Error("Network error while loading steps"), null);
                }
            });
        },

        /**
         * Save a step (create or update)
         * @param {Object} data - Step data object including workflow_id
         * @param {Function} callback - Callback function with (error, step) signature
         */
        saveStep: function (data, callback) {
            var isUpdate = data.id && data.id !== "00000000-0000-0000-0000-000000000000";
            var url = isUpdate 
                ? API_BASE_URL + "/workflows/" + data.workflow_id + "/steps/" + data.id 
                : API_BASE_URL + "/workflows/" + data.workflow_id + "/steps";
            var method = isUpdate ? "PUT" : "POST";

            $.ajax({
                url: url,
                type: method,
                contentType: "application/json",
                data: JSON.stringify(data),
                dataType: "json",
                success: function (response) {
                    if (response && response.success) {
                        var msg = isUpdate ? "Step updated successfully" : "Step created successfully";
                        toastr.success(msg);
                        callback(null, response.object);
                    } else {
                        var errorMsg = response && response.message ? response.message : "Failed to save step";
                        toastr.error(errorMsg);
                        callback(new Error(errorMsg), null);
                    }
                },
                error: function (xhr, status, error) {
                    console.error("Error saving step:", error);
                    toastr.error("Network error while saving step");
                    callback(new Error("Network error while saving step"), null);
                }
            });
        },

        /**
         * Delete a step by ID
         * @param {string} stepId - The step GUID
         * @param {Function} callback - Callback function with (error) signature
         */
        deleteStep: function (stepId, callback) {
            $.ajax({
                url: API_BASE_URL + "/steps/" + stepId,
                type: "DELETE",
                dataType: "json",
                success: function (response) {
                    if (response && response.success) {
                        toastr.success("Step deleted successfully");
                        callback(null);
                    } else {
                        var errorMsg = response && response.message ? response.message : "Failed to delete step";
                        toastr.error(errorMsg);
                        callback(new Error(errorMsg));
                    }
                },
                error: function (xhr, status, error) {
                    console.error("Error deleting step:", error);
                    toastr.error("Network error while deleting step");
                    callback(new Error("Network error while deleting step"));
                }
            });
        },

        /**
         * Reorder steps within a workflow
         * @param {string} workflowId - The workflow GUID
         * @param {Array} stepIds - Array of step GUIDs in new order
         * @param {Function} callback - Optional callback function
         */
        reorderSteps: function (workflowId, stepIds, callback) {
            $.ajax({
                url: API_BASE_URL + "/workflows/" + workflowId + "/steps/reorder",
                type: "PUT",
                contentType: "application/json",
                data: JSON.stringify({ step_ids: stepIds }),
                dataType: "json",
                success: function (response) {
                    if (response && response.success) {
                        toastr.success("Steps reordered successfully");
                        if (callback) callback(null);
                    } else {
                        var errorMsg = response && response.message ? response.message : "Failed to reorder steps";
                        toastr.error(errorMsg);
                        if (callback) callback(new Error(errorMsg));
                    }
                },
                error: function (xhr, status, error) {
                    console.error("Error reordering steps:", error);
                    toastr.error("Network error while reordering steps");
                    if (callback) callback(new Error("Network error while reordering steps"));
                }
            });
        },

        // ============================================================================
        // RULE CRUD OPERATIONS
        // ============================================================================

        /**
         * Load all rules for a step
         * @param {string} stepId - The step GUID
         * @param {Function} callback - Callback function with (error, rules) signature
         */
        loadRules: function (stepId, callback) {
            $.ajax({
                url: API_BASE_URL + "/steps/" + stepId + "/rules",
                type: "GET",
                dataType: "json",
                success: function (response) {
                    if (response && response.success) {
                        callback(null, response.object || []);
                    } else {
                        var errorMsg = response && response.message ? response.message : "Failed to load rules";
                        callback(new Error(errorMsg), null);
                    }
                },
                error: function (xhr, status, error) {
                    console.error("Error loading rules:", error);
                    callback(new Error("Network error while loading rules"), null);
                }
            });
        },

        /**
         * Save a rule (create or update)
         * @param {Object} data - Rule data object including step_id
         * @param {Function} callback - Callback function with (error, rule) signature
         */
        saveRule: function (data, callback) {
            var isUpdate = data.id && data.id !== "00000000-0000-0000-0000-000000000000";
            var url = isUpdate 
                ? API_BASE_URL + "/steps/" + data.step_id + "/rules/" + data.id 
                : API_BASE_URL + "/steps/" + data.step_id + "/rules";
            var method = isUpdate ? "PUT" : "POST";

            $.ajax({
                url: url,
                type: method,
                contentType: "application/json",
                data: JSON.stringify(data),
                dataType: "json",
                success: function (response) {
                    if (response && response.success) {
                        var msg = isUpdate ? "Rule updated successfully" : "Rule created successfully";
                        toastr.success(msg);
                        callback(null, response.object);
                    } else {
                        var errorMsg = response && response.message ? response.message : "Failed to save rule";
                        toastr.error(errorMsg);
                        callback(new Error(errorMsg), null);
                    }
                },
                error: function (xhr, status, error) {
                    console.error("Error saving rule:", error);
                    toastr.error("Network error while saving rule");
                    callback(new Error("Network error while saving rule"), null);
                }
            });
        },

        /**
         * Delete a rule by ID
         * @param {string} ruleId - The rule GUID
         * @param {Function} callback - Callback function with (error) signature
         */
        deleteRule: function (ruleId, callback) {
            $.ajax({
                url: API_BASE_URL + "/rules/" + ruleId,
                type: "DELETE",
                dataType: "json",
                success: function (response) {
                    if (response && response.success) {
                        toastr.success("Rule deleted successfully");
                        callback(null);
                    } else {
                        var errorMsg = response && response.message ? response.message : "Failed to delete rule";
                        toastr.error(errorMsg);
                        callback(new Error(errorMsg));
                    }
                },
                error: function (xhr, status, error) {
                    console.error("Error deleting rule:", error);
                    toastr.error("Network error while deleting rule");
                    callback(new Error("Network error while deleting rule"));
                }
            });
        },

        // ============================================================================
        // UI HELPER METHODS
        // ============================================================================

        /**
         * Show workflow create/edit modal
         * @param {string|null} workflowId - Workflow ID for edit, null for create
         */
        showWorkflowModal: function (workflowId) {
            var self = this;
            var $modal = $("#workflow-modal");

            if (workflowId) {
                // Edit mode - load existing workflow
                $modal.find(".modal-title").text("Edit Workflow");
                self.loadWorkflow(workflowId, function (error, workflow) {
                    if (error) {
                        toastr.error("Failed to load workflow for editing");
                        return;
                    }
                    self.populateWorkflowForm(workflow);
                    $modal.modal("show");
                });
            } else {
                // Create mode
                $modal.find(".modal-title").text("Create Workflow");
                self.resetWorkflowForm();
                $modal.modal("show");
            }
        },

        /**
         * Populate workflow form with data
         * @param {Object} workflow - Workflow data object
         */
        populateWorkflowForm: function (workflow) {
            var $form = $("#workflow-form");
            $form.find("[name='id']").val(workflow.id || "");
            $form.find("[name='name']").val(workflow.name || "");
            $form.find("[name='entity_name']").val(workflow.entity_name || "");
            $form.find("[name='is_active']").prop("checked", workflow.is_active === true);
            $form.find("[name='description']").val(workflow.description || "");
        },

        /**
         * Reset workflow form to empty state
         */
        resetWorkflowForm: function () {
            var $form = $("#workflow-form");
            $form[0].reset();
            $form.find("[name='id']").val("");
            $form.find("[name='is_active']").prop("checked", true);
        },

        /**
         * Save workflow from form submission
         * @param {jQuery} $form - The form jQuery element
         */
        saveWorkflowFromForm: function ($form) {
            var self = this;

            if (!self.validateWorkflowForm($form)) {
                return;
            }

            var data = {
                id: $form.find("[name='id']").val() || null,
                name: $form.find("[name='name']").val(),
                entity_name: $form.find("[name='entity_name']").val(),
                is_active: $form.find("[name='is_active']").is(":checked"),
                description: $form.find("[name='description']").val()
            };

            self.saveWorkflow(data, function (error, workflow) {
                if (!error) {
                    $("#workflow-modal").modal("hide");
                    self.refreshWorkflowList();
                }
            });
        },

        /**
         * Show step create/edit modal
         * @param {string} workflowId - Parent workflow ID
         * @param {string|null} stepId - Step ID for edit, null for create
         */
        showStepModal: function (workflowId, stepId) {
            var self = this;
            var $modal = $("#step-modal");
            $modal.find("[name='workflow_id']").val(workflowId);

            if (stepId) {
                // Edit mode
                $modal.find(".modal-title").text("Edit Step");
                // Load step data via AJAX
                $.ajax({
                    url: API_BASE_URL + "/steps/" + stepId,
                    type: "GET",
                    dataType: "json",
                    success: function (response) {
                        if (response && response.success) {
                            self.populateStepForm(response.object);
                            $modal.modal("show");
                        } else {
                            toastr.error("Failed to load step for editing");
                        }
                    },
                    error: function () {
                        toastr.error("Network error while loading step");
                    }
                });
            } else {
                // Create mode
                $modal.find(".modal-title").text("Add Step");
                self.resetStepForm(workflowId);
                $modal.modal("show");
            }
        },

        /**
         * Populate step form with data
         * @param {Object} step - Step data object
         */
        populateStepForm: function (step) {
            var $form = $("#step-form");
            $form.find("[name='id']").val(step.id || "");
            $form.find("[name='workflow_id']").val(step.workflow_id || "");
            $form.find("[name='name']").val(step.name || "");
            $form.find("[name='step_order']").val(step.step_order || 1);
            $form.find("[name='approver_type']").val(step.approver_type || "");
            $form.find("[name='approver_id']").val(step.approver_id || "");
            $form.find("[name='sla_hours']").val(step.sla_hours || "");
        },

        /**
         * Reset step form to empty state
         * @param {string} workflowId - Parent workflow ID
         */
        resetStepForm: function (workflowId) {
            var $form = $("#step-form");
            $form[0].reset();
            $form.find("[name='id']").val("");
            $form.find("[name='workflow_id']").val(workflowId);
            $form.find("[name='step_order']").val(1);
        },

        /**
         * Save step from form submission
         * @param {jQuery} $form - The form jQuery element
         */
        saveStepFromForm: function ($form) {
            var self = this;

            if (!self.validateStepForm($form)) {
                return;
            }

            var data = {
                id: $form.find("[name='id']").val() || null,
                workflow_id: $form.find("[name='workflow_id']").val(),
                name: $form.find("[name='name']").val(),
                step_order: parseInt($form.find("[name='step_order']").val(), 10) || 1,
                approver_type: $form.find("[name='approver_type']").val(),
                approver_id: $form.find("[name='approver_id']").val() || null,
                sla_hours: parseInt($form.find("[name='sla_hours']").val(), 10) || null
            };

            self.saveStep(data, function (error, step) {
                if (!error) {
                    $("#step-modal").modal("hide");
                    self.refreshStepList(data.workflow_id);
                }
            });
        },

        /**
         * Show rule create/edit modal
         * @param {string} stepId - Parent step ID
         * @param {string|null} ruleId - Rule ID for edit, null for create
         */
        showRuleModal: function (stepId, ruleId) {
            var self = this;
            var $modal = $("#rule-modal");
            $modal.find("[name='step_id']").val(stepId);

            if (ruleId) {
                // Edit mode
                $modal.find(".modal-title").text("Edit Rule");
                // Load rule data via AJAX
                $.ajax({
                    url: API_BASE_URL + "/rules/" + ruleId,
                    type: "GET",
                    dataType: "json",
                    success: function (response) {
                        if (response && response.success) {
                            self.populateRuleForm(response.object);
                            $modal.modal("show");
                        } else {
                            toastr.error("Failed to load rule for editing");
                        }
                    },
                    error: function () {
                        toastr.error("Network error while loading rule");
                    }
                });
            } else {
                // Create mode
                $modal.find(".modal-title").text("Add Rule");
                self.resetRuleForm(stepId);
                $modal.modal("show");
            }
        },

        /**
         * Populate rule form with data
         * @param {Object} rule - Rule data object
         */
        populateRuleForm: function (rule) {
            var $form = $("#rule-form");
            $form.find("[name='id']").val(rule.id || "");
            $form.find("[name='step_id']").val(rule.step_id || "");
            $form.find("[name='rule_type']").val(rule.rule_type || "");
            $form.find("[name='field_name']").val(rule.field_name || "");
            $form.find("[name='operator']").val(rule.operator || "");
            $form.find("[name='value']").val(rule.value || "");
            $form.find("[name='action']").val(rule.action || "");
        },

        /**
         * Reset rule form to empty state
         * @param {string} stepId - Parent step ID
         */
        resetRuleForm: function (stepId) {
            var $form = $("#rule-form");
            $form[0].reset();
            $form.find("[name='id']").val("");
            $form.find("[name='step_id']").val(stepId);
        },

        /**
         * Save rule from form submission
         * @param {jQuery} $form - The form jQuery element
         */
        saveRuleFromForm: function ($form) {
            var self = this;

            if (!self.validateRuleForm($form)) {
                return;
            }

            var data = {
                id: $form.find("[name='id']").val() || null,
                step_id: $form.find("[name='step_id']").val(),
                rule_type: $form.find("[name='rule_type']").val(),
                field_name: $form.find("[name='field_name']").val(),
                operator: $form.find("[name='operator']").val(),
                value: $form.find("[name='value']").val(),
                action: $form.find("[name='action']").val()
            };

            self.saveRule(data, function (error, rule) {
                if (!error) {
                    $("#rule-modal").modal("hide");
                    self.refreshRuleList(data.step_id);
                }
            });
        },

        // ============================================================================
        // FORM VALIDATION HELPERS
        // ============================================================================

        /**
         * Validate workflow form fields
         * @param {jQuery} $form - The form jQuery element
         * @returns {boolean} True if valid, false otherwise
         */
        validateWorkflowForm: function ($form) {
            var isValid = true;
            var errors = [];

            // Validate name
            var name = $form.find("[name='name']").val();
            if (!name || name.trim() === "") {
                errors.push("Workflow name is required");
                $form.find("[name='name']").addClass("is-invalid");
                isValid = false;
            } else {
                $form.find("[name='name']").removeClass("is-invalid");
            }

            // Validate entity_name
            var entityName = $form.find("[name='entity_name']").val();
            if (!entityName || entityName.trim() === "") {
                errors.push("Target entity is required");
                $form.find("[name='entity_name']").addClass("is-invalid");
                isValid = false;
            } else {
                $form.find("[name='entity_name']").removeClass("is-invalid");
            }

            if (!isValid) {
                errors.forEach(function (error) {
                    toastr.error(error);
                });
            }

            return isValid;
        },

        /**
         * Validate step form fields
         * @param {jQuery} $form - The form jQuery element
         * @returns {boolean} True if valid, false otherwise
         */
        validateStepForm: function ($form) {
            var isValid = true;
            var errors = [];

            // Validate name
            var name = $form.find("[name='name']").val();
            if (!name || name.trim() === "") {
                errors.push("Step name is required");
                $form.find("[name='name']").addClass("is-invalid");
                isValid = false;
            } else {
                $form.find("[name='name']").removeClass("is-invalid");
            }

            // Validate approver_type
            var approverType = $form.find("[name='approver_type']").val();
            if (!approverType || approverType.trim() === "") {
                errors.push("Approver type is required");
                $form.find("[name='approver_type']").addClass("is-invalid");
                isValid = false;
            } else {
                $form.find("[name='approver_type']").removeClass("is-invalid");
            }

            // Validate step_order
            var stepOrder = $form.find("[name='step_order']").val();
            if (!stepOrder || parseInt(stepOrder, 10) < 1) {
                errors.push("Step order must be a positive number");
                $form.find("[name='step_order']").addClass("is-invalid");
                isValid = false;
            } else {
                $form.find("[name='step_order']").removeClass("is-invalid");
            }

            if (!isValid) {
                errors.forEach(function (error) {
                    toastr.error(error);
                });
            }

            return isValid;
        },

        /**
         * Validate rule form fields
         * @param {jQuery} $form - The form jQuery element
         * @returns {boolean} True if valid, false otherwise
         */
        validateRuleForm: function ($form) {
            var isValid = true;
            var errors = [];

            // Validate rule_type
            var ruleType = $form.find("[name='rule_type']").val();
            if (!ruleType || ruleType.trim() === "") {
                errors.push("Rule type is required");
                $form.find("[name='rule_type']").addClass("is-invalid");
                isValid = false;
            } else {
                $form.find("[name='rule_type']").removeClass("is-invalid");
            }

            // Validate field_name
            var fieldName = $form.find("[name='field_name']").val();
            if (!fieldName || fieldName.trim() === "") {
                errors.push("Field name is required");
                $form.find("[name='field_name']").addClass("is-invalid");
                isValid = false;
            } else {
                $form.find("[name='field_name']").removeClass("is-invalid");
            }

            // Validate operator
            var operator = $form.find("[name='operator']").val();
            if (!operator || operator.trim() === "") {
                errors.push("Operator is required");
                $form.find("[name='operator']").addClass("is-invalid");
                isValid = false;
            } else {
                $form.find("[name='operator']").removeClass("is-invalid");
            }

            if (!isValid) {
                errors.forEach(function (error) {
                    toastr.error(error);
                });
            }

            return isValid;
        },

        // ============================================================================
        // CONFIRMATION DIALOGS
        // ============================================================================

        /**
         * Show confirmation dialog for workflow deletion
         * @param {string} workflowId - The workflow GUID
         * @param {string} workflowName - The workflow name for display
         */
        confirmDeleteWorkflow: function (workflowId, workflowName) {
            var self = this;
            var message = "Are you sure you want to delete the workflow '" + (workflowName || "Unnamed") + "'? This will also delete all associated steps and rules.";

            if (window.confirm(message)) {
                self.deleteWorkflow(workflowId, function (error) {
                    if (!error) {
                        self.refreshWorkflowList();
                    }
                });
            }
        },

        /**
         * Show confirmation dialog for step deletion
         * @param {string} stepId - The step GUID
         * @param {string} stepName - The step name for display
         */
        confirmDeleteStep: function (stepId, stepName) {
            var self = this;
            var message = "Are you sure you want to delete the step '" + (stepName || "Unnamed") + "'? This will also delete all associated rules.";

            if (window.confirm(message)) {
                self.deleteStep(stepId, function (error) {
                    if (!error) {
                        // Refresh the parent workflow's step list
                        // Note: The workflow ID should be available from context
                        location.reload();
                    }
                });
            }
        },

        /**
         * Show confirmation dialog for rule deletion
         * @param {string} ruleId - The rule GUID
         */
        confirmDeleteRule: function (ruleId) {
            var self = this;
            var message = "Are you sure you want to delete this rule?";

            if (window.confirm(message)) {
                self.deleteRule(ruleId, function (error) {
                    if (!error) {
                        // Refresh the parent step's rule list
                        location.reload();
                    }
                });
            }
        },

        // ============================================================================
        // LIST REFRESH METHODS
        // ============================================================================

        /**
         * Refresh the workflow list display
         */
        refreshWorkflowList: function () {
            // Reload the page to refresh the workflow list
            // In a more sophisticated implementation, this would use AJAX to refresh just the list
            location.reload();
        },

        /**
         * Refresh the step list display for a workflow
         * @param {string} workflowId - The workflow GUID
         */
        refreshStepList: function (workflowId) {
            // Reload the page to refresh the step list
            location.reload();
        },

        /**
         * Refresh the rule list display for a step
         * @param {string} stepId - The step GUID
         */
        refreshRuleList: function (stepId) {
            // Reload the page to refresh the rule list
            location.reload();
        }
    };

    // ============================================================================
    // PAGE BUILDER LIFECYCLE EVENT HANDLERS
    // ============================================================================

    /**
     * jQuery document ready handler
     */
    $(function () {

        /**
         * WvPbManager_Design_Loaded event handler
         * Called when the component is loaded in design mode
         */
        document.addEventListener("WvPbManager_Design_Loaded", function (event) {
            if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
                console.log(COMPONENT_NAME + " Design loaded");
                // Initialize component in design mode if needed
            }
        });

        /**
         * WvPbManager_Design_Unloaded event handler
         * Called when the component is unloaded from design mode
         */
        document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
            if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
                console.log(COMPONENT_NAME + " Design unloaded");
                // Cleanup design mode resources if needed
            }
        });

        /**
         * WvPbManager_Options_Loaded event handler
         * Called when the component options panel is loaded
         */
        document.addEventListener("WvPbManager_Options_Loaded", function (event) {
            if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
                console.log(COMPONENT_NAME + " Options loaded");
                // Initialize options panel interactions
                ApprovalWorkflowConfigService.init(event.payload);
            }
        });

        /**
         * WvPbManager_Options_Unloaded event handler
         * Called when the component options panel is closed
         */
        document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
            if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
                console.log(COMPONENT_NAME + " Options unloaded");
                // Cleanup options panel resources
                ApprovalWorkflowConfigService.destroy();
            }
        });

        /**
         * WvPbManager_Node_Moved event handler
         * Called when the component is moved via drag/drop in the page builder
         */
        document.addEventListener("WvPbManager_Node_Moved", function (event) {
            if (event && event.payload && event.payload.component_name === COMPONENT_NAME) {
                console.log(COMPONENT_NAME + " Node moved");
                // Handle any repositioning logic if needed
            }
        });

        // Initialize the service for display mode (non-page-builder context)
        if ($("[data-component='" + COMPONENT_NAME + "']").length > 0) {
            ApprovalWorkflowConfigService.init({});
        }

    });

    // Expose the service globally for external access if needed
    window.ApprovalWorkflowConfigService = ApprovalWorkflowConfigService;

})(window, jQuery);
