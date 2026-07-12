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

	// Keep the delete and archive toggles in sync with the master bulk-actions toggle.
	function BulkActionsToggle(e) {
		var enabled = e.target.checked;
		var deleteInput = document.querySelector('#modal-component-options .modal-body input[name="enable_bulk_delete"]');
		var archiveInput = document.querySelector('#modal-component-options .modal-body input[name="enable_bulk_archive"]');
		if (deleteInput) { deleteInput.disabled = !enabled; }
		if (archiveInput) { archiveInput.disabled = !enabled; }
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



		document.addEventListener("WvPbManager_Options_Loaded", function (event) {
			if (event && event.payload && event.payload.component_name === "WebVella.Erp.Web.Components.PcGrid"){
				window.setTimeout(function () {
					var visibleColumnsCount = document.querySelector('#modal-component-options .modal-body input[name="visible_columns"]');
					visibleColumnsCount.setAttribute("data-old-value",visibleColumnsCount.value);
					visibleColumnsCount.addEventListener("blur", ColumnCountChange);
					var bulkActionsMaster = document.querySelector('#modal-component-options .modal-body input[name="enable_bulk_actions"]');
					if (bulkActionsMaster) {
						bulkActionsMaster.addEventListener("change", BulkActionsToggle);
						BulkActionsToggle({ target: bulkActionsMaster });
					}
				},500);
			}
		});

		document.addEventListener("WvPbManager_Options_Unloaded", function (event) {
			if (event && event.payload && event.payload.component_name === "WebVella.Erp.Web.Components.PcGrid"){
			console.log("WebVella.Erp.Web.Components.PcGrid UnLoad");
				var visibleColumnsCount = document.querySelector('#modal-component-options .modal-body input[name="visible_columns"]');
				visibleColumnsCount.removeEventListener("blur", ColumnCountChange);
				var bulkActionsMaster = document.querySelector('#modal-component-options .modal-body input[name="enable_bulk_actions"]');
				if (bulkActionsMaster) {
					bulkActionsMaster.removeEventListener("change", BulkActionsToggle);
				}
			}
		});


	//////////////////////////////////////////////////////////////////////////////////
	/// You code is above
	