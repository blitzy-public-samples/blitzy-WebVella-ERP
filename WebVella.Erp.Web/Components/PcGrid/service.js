"use strict";

	/// Your code goes below
	///////////////////////////////////////////////////////////////////////////////////

	function ColumnCountChange(e) {
		var oldValue = e.target.getAttribute("data-old-value");
		var value = e.target.value;
		if (value.toString() !== oldValue.toString()) {
			e.target.setAttribute("data-old-value",value);
			$('#modal-component-options div[id^="section-column"]').addClass("d-none");
			for (var i = 1; i <= value; i++) {
				$('#modal-component-options #section-column'+ i).removeClass("d-none");
			}	
		}
	}

	// Reflect the master bulk-actions toggle on the delete and archive child toggles without disabling
	// them. A disabled checkbox drops out of the submitted form, which would lose the saved child value,
	// so each child row dims and stops taking clicks while its input stays enabled and serializable.
	function BulkActionsToggle(e) {
		if (!e || !e.target) { return; }
		var enabled = e.target.checked;
		var deleteInput = document.querySelector('#modal-component-options .modal-body input[name="enable_bulk_delete"]');
		var archiveInput = document.querySelector('#modal-component-options .modal-body input[name="enable_bulk_archive"]');
		[deleteInput, archiveInput].forEach(function (input) {
			if (!input) { return; }
			var row = (input.closest ? input.closest(".form-group") : null) || input.parentElement;
			if (row) {
				row.style.opacity = enabled ? "" : "0.5";
				row.style.pointerEvents = enabled ? "" : "none";
			}
		});
	}


	//	document.addEventListener("WvPbManager_Design_Loaded", function (event) {
	//		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Web.Components.PcGrid"){
	//			console.log("WebVella.Erp.Web.Components.PcRecordList Design loaded");
	//		}
	//	});

	//	document.addEventListener("WvPbManager_Design_Unloaded", function (event) {
	//		if (event && event.payload && event.payload.component_name === "WebVella.Erp.Web.Components.PcGrid"){
	//			console.log("WebVella.Erp.Web.Components.PcRecordList Design unloaded");
	//		}
	//	});



		// Track the deferred options-init timer so the Unloaded handler can cancel it. Without the stored id
		// the timer can fire after the options panel closes and then reach for nodes that no longer exist.
		var pcGridOptionsInitTimeout = null;

		document.addEventListener("WvPbManager_Options_Loaded", function (event) {
			if (event && event.payload && event.payload.component_name === "WebVella.Erp.Web.Components.PcGrid"){
				pcGridOptionsInitTimeout = window.setTimeout(function () {
					pcGridOptionsInitTimeout = null;
					var visibleColumnsCount = document.querySelector('#modal-component-options .modal-body input[name="visible_columns"]');
					if (visibleColumnsCount) {
						visibleColumnsCount.setAttribute("data-old-value", visibleColumnsCount.value);
						visibleColumnsCount.addEventListener("blur", ColumnCountChange);
					}
					var bulkActionsMaster = document.querySelector('#modal-component-options .modal-body input[name="enable_bulk_actions"]');
					if (bulkActionsMaster) {
						bulkActionsMaster.addEventListener("change", BulkActionsToggle);
						BulkActionsToggle({ target: bulkActionsMaster });
					}
				}, 500);
			}
		});

		document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
			if (event && event.payload && event.payload.component_name === "WebVella.Erp.Web.Components.PcGrid"){
				if (pcGridOptionsInitTimeout !== null) {
					window.clearTimeout(pcGridOptionsInitTimeout);
					pcGridOptionsInitTimeout = null;
				}
				var visibleColumnsCount = document.querySelector('#modal-component-options .modal-body input[name="visible_columns"]');
				if (visibleColumnsCount) {
					visibleColumnsCount.removeEventListener("blur", ColumnCountChange);
				}
				var bulkActionsMaster = document.querySelector('#modal-component-options .modal-body input[name="enable_bulk_actions"]');
				if (bulkActionsMaster) {
					bulkActionsMaster.removeEventListener("change", BulkActionsToggle);
				}
			}
		});


	//////////////////////////////////////////////////////////////////////////////////
	/// You code is above
	