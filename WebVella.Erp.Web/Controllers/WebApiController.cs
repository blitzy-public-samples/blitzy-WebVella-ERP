using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WebVella.Erp.Api;
using WebVella.Erp.Api.Models;
using WebVella.Erp.Api.Models.AutoMapper;
using WebVella.Erp.Database;
using WebVella.Erp.Diagnostics;
using WebVella.Erp.Eql;
using WebVella.Erp.Jobs;
using WebVella.Erp.Utilities;
using WebVella.Erp.Web.Models;
using WebVella.Erp.Web.Service;
using WebVella.Erp.Web.Services;
using WebVella.Erp.Web.Utils;
using Wangkanai.Detection.Services;

namespace WebVella.Erp.Web.Controllers
{
	[Authorize]
	public class WebApiController : ApiControllerBase
	{
		private const char RELATION_SEPARATOR = '.';
		private const char RELATION_NAME_RESULT_SEPARATOR = '$';

		RecordManager recMan;
		EntityManager entMan;
		EntityRelationManager relMan;
		SecurityManager secMan;
		IErpService erpService;
		IDetectionService _detection;
		ErpRequestContext erpRequestContext;

		public WebApiController([FromServices] IErpService erpService,
			[FromServices] ErpRequestContext requestContext,
			[FromServices] IDetectionService detection)
		{
			recMan = new RecordManager();
			secMan = new SecurityManager();
			entMan = new EntityManager();
			relMan = new EntityRelationManager();
			this.erpService = erpService;
			this.erpRequestContext = requestContext;
			this._detection = detection;
		}

		[Route("api/v3/en_US/eql")]
		[HttpPost]
		public ActionResult EqlQueryAction([FromBody] EqlQuery model)
		{
			ResponseModel response = new ResponseModel();
			response.Success = true;

			if (model == null)
				return NotFound();

			try
			{
				var eqlResult = new EqlCommand(model.Eql, model.Parameters).Execute();
				response.Object = eqlResult;
			}
			catch (EqlException eqlEx)
			{
				return JsonFromEqlException(response, eqlEx);
			}
			catch (Exception ex)
			{
				return JsonFromException(response, ex);
			}

			return Json(response);
		}

		[Route("api/v3/en_US/eql-ds")]
		[HttpPost]
		public ActionResult DataSourceQueryAction([FromBody] JObject submitObj)
		{
			ResponseModel response = new ResponseModel();
			response.Success = true;


			if (submitObj == null)
				return NotFound();

			EqlDataSourceQuery model = BuildEqlDataSourceQueryFromSubmit(submitObj);


			try
			{
				DataSourceManager dsMan = new DataSourceManager();
				var dataSources = dsMan.GetAll();
				var ds = dataSources.SingleOrDefault(x => x.Name == model.Name);
				if (ds == null)
				{
					response.Success = false;
					response.Message = $"DataSource with name '{model.Name}' not found.";
					return Json(response);
				}

				if (ds is DatabaseDataSource)
				{
					var list = (EntityRecordList)dsMan.Execute(ds.Id, model.Parameters);
					response.Object = new { list, total_count = list.TotalCount };
				}
				else if (ds is CodeDataSource)
				{
					Dictionary<string, object> arguments = new Dictionary<string, object>();
					foreach (var par in model.Parameters)
						arguments[par.ParameterName] = par.Value;

					response.Object = ((CodeDataSource)ds).Execute(arguments);
				}
				else
				{
					response.Success = false;
					response.Message = $"DataSource type is not supported.";
					return Json(response);
				}
			}
			catch (EqlException eqlEx)
			{
				return JsonFromEqlException(response, eqlEx);
			}
			catch (Exception ex)
			{
				return JsonFromException(response, ex);
			}

			return Json(response);
		}

		[Route("api/v3/en_US/eql-ds-select2")]
		[HttpPost]
		public ActionResult DataSourceQueryActionForSelect2([FromBody] JObject submitObj)
		{
			if (submitObj == null)
				return NotFound();

			var result = new EntityRecord();
			result["results"] = new List<EntityRecord>();
			result["pagination"] = new EntityRecord();

			EqlDataSourceQuery model = BuildEqlDataSourceQueryFromSubmit(submitObj);
			var page = ResolveSelect2Page(model);
			var records = new List<EntityRecord>();
			int? total = 0;
			try
			{
				var earlyResult = ExecuteSelect2DataSource(model, ref records, ref total);
				if (earlyResult != null)
					return earlyResult;
			}
			catch
			{
				return BadRequest();
			}

			//Post process records according to requiredments {id,text}
			var processedRecords = MapRecordsToSelect2Items(records);
			var moreRecord = new EntityRecord();
			moreRecord["more"] = false;
			if (records.Count > 0)
			{
				if (total > page * 10)
				{
					moreRecord["more"] = true;
				}
				result["results"] = processedRecords;
			}

			result["pagination"] = moreRecord;
			return Json(result);
		}


		[Route("api/v3.0/user/preferences/toggle-sidebar-size")]
		[HttpPost]
		public ActionResult ToggleSidebarSize()
		{
			//TODO: Implement. Should Check the current size in user preferences and toggle in order "","sm","md","lg"
			var currentUser = AuthService.GetUser(User);
			var currentUserPreferences = currentUser.Preferences;
			var targetSidebarSize = "";
			switch (currentUserPreferences.SidebarSize)
			{
				case "sm":
					targetSidebarSize = "lg";
					break;
				case "lg":
					targetSidebarSize = "sm";
					break;
				default:
					targetSidebarSize = "lg";
					break;
			}
			var response = new BaseResponseModel();
			try
			{
				new UserPreferencies().SetSidebarSize(currentUser.Id, targetSidebarSize);
				response.Success = true;
				response.Message = "success";
				return Json(response);
			}
			catch (Exception ex)
			{
				response.Success = false;
				response.Message = ex.Message;
				new Log().Create(LogType.Error, "ToggleSidebarSize API Method Error", ex);
				return Json(response);
			}
		}

		[Route("api/v3.0/user/preferences/toggle-section-collapse")]
		[HttpPost]
		public ActionResult ToggleSection(Guid? nodeId = null, bool isCollapsed = false)
		{
			var response = new BaseResponseModel();
			try
			{
				if (nodeId == null)
					throw new Exception("nodeId query param is required");

				var userPreferencesService = new UserPreferencies();

				var currentUser = AuthService.GetUser(User);

				EntityRecord componentData = userPreferencesService.GetComponentData(currentUser.Id, "WebVella.Erp.Web.Components.PcSection");

				var collapsedNodeIds = new List<Guid>();
				var uncollapsedNodeIds = new List<Guid>();

				if (componentData == null)
				{
					componentData = new EntityRecord();
					componentData["collapsed_node_ids"] = new List<Guid>();
					componentData["uncollapsed_node_ids"] = new List<Guid>();
				}
				else
				{
					collapsedNodeIds = ResolveNodeIdsFromComponentData(componentData, "collapsed_node_ids");
					uncollapsedNodeIds = ResolveNodeIdsFromComponentData(componentData, "uncollapsed_node_ids");
				}

				ApplyToggleSectionState(isCollapsed, nodeId, ref collapsedNodeIds, ref uncollapsedNodeIds);

				componentData["collapsed_node_ids"] = collapsedNodeIds;
				componentData["uncollapsed_node_ids"] = uncollapsedNodeIds;

				userPreferencesService.SetComponentData(currentUser.Id, "WebVella.Erp.Web.Components.PcSection", componentData);
				response.Success = true;
				response.Message = "success";
				return Json(response);
			}
			catch (Exception ex)
			{
				response.Success = false;
				response.Message = ex.Message;
				new Log().Create(LogType.Error, "ToggleSidebarSize API Method Error", ex);
				return Json(response);
			}
		}

		[Route("api/v3.0/datasource/code-compile")]
		[HttpPost]
		public ActionResult DataSourceAction([FromBody] DataSourceCodeTestModel model)
		{
			try
			{
				CodeEvalService.Compile(model.CsCode);
			}
			catch (Exception ex)
			{
				new Log().Create(LogType.Error, "DataSourceAction Code compile API Method Error", ex);
				return Json(new { success = false, message = ex.Message });
			}

			return Json(new { success = true, message = "" });
		}

		[Route("api/v3.0/datasource/test")]
		[HttpPost]
		public ActionResult DataSourceAction([FromBody] DataSourceTestModel model)
		{
			if (model == null)
				return NotFound();

			string sql = string.Empty;
			string data = "";
			List<EqlError> errors = new List<EqlError>();
			try
			{
				DataSourceManager dataSourceManager = new DataSourceManager();
				if (model.Action == "sql")
					sql = dataSourceManager.GenerateSql(model.Eql, model.Parameters, model.ReturnTotal );
				if (model.Action == "data")
					data = JsonConvert.SerializeObject(dataSourceManager.Execute(model.Eql, model.Parameters, model.ReturnTotal), Formatting.Indented);
			}
			catch (EqlException eqlEx)
			{
				errors.AddRange(eqlEx.Errors);
			}
			catch (Exception ex)
			{
				new Log().Create(LogType.Error, "DataSourceAction test API Method Error", ex);
				errors.Add(new EqlError { Message = ex.Message });
			}

			return Json(new { sql, data, errors });
		}

		[Route("api/v3.0/datasource/{dataSourceId}/test")]
		[HttpPost]
		public ActionResult DataSourceAction(Guid dataSourceId, [FromBody] DataSourceTestModel model)
		{

			if (model == null)
				return NotFound();

			string sql = string.Empty;
			string data = "";
			List<EqlError> errors = new List<EqlError>();
			try
			{
				DataSourceManager dataSourceManager = new DataSourceManager();
				var dataSource = dataSourceManager.Get(dataSourceId);
				if (dataSource == null)
				{
					errors.Add(new EqlError { Message = "DataSource Not found" });
				}

				var dataSourceEql = "";
				if (dataSource is DatabaseDataSource)
				{
					dataSourceEql = ((DatabaseDataSource)dataSource).EqlText;
				}

				var compoundParams = new List<DataSourceParameter>();
				foreach (var dsParam in dataSource.Parameters)
				{
					var pageParameter = model.ParamList.FirstOrDefault(x => x.Name == dsParam.Name);
					if (pageParameter != null)
					{
						compoundParams.Add(pageParameter);
					}
					else
					{
						compoundParams.Add(dsParam);
					}
				}

				var paramText = dataSourceManager.ConvertParamsToText(compoundParams);

				if (model.Action == "sql")
					sql = dataSourceManager.GenerateSql(dataSourceEql, paramText, dataSource.ReturnTotal);
				if (model.Action == "data")
					data = JsonConvert.SerializeObject(dataSourceManager.Execute(dataSourceEql, paramText, dataSource.ReturnTotal), Formatting.Indented);
			}
			catch (EqlException eqlEx)
			{
				errors.AddRange(eqlEx.Errors);
			}
			catch (Exception ex)
			{
				new Log().Create(LogType.Error, "DataSourceAction Id test API Method Error", ex);
				errors.Add(new EqlError { Message = ex.Message });
			}

			return Json(new { sql, data, errors });
		}

		//[AllowAnonymous] //Needed only when webcomponent development
		[Route("api/v3.0/page/{pageId}/node/create")]
		[HttpPost]
		public ActionResult CreatePageBodyNode(Guid pageId, [FromBody] PageBodyNode newNode)
		{
			try
			{
				var pageSrv = new PageService();

				ErpPage page = pageSrv.GetPage(pageId);
				if (page == null) //page not found
					return NotFound();

				if (newNode == null)
					return NotFound();

				if (newNode.Id == Guid.Empty)
					newNode.Id = Guid.NewGuid();

				if (page.Body == null && newNode.ParentId != null)
					throw new Exception("Cannot create child node in page with no root node.");

				//if (page.Body != null && newNode.ParentId == null)
				//	throw new Exception("Cannot create root node in page with already existing root node.");

				pageSrv.CreatePageBodyNode(newNode.Id, newNode.ParentId, pageId, newNode.NodeId, newNode.Weight,
					newNode.ComponentName, newNode.ContainerId, newNode.Options);

				var createdNode = pageSrv.GetPageNodeById(newNode.Id);

				var currentUser = AuthService.GetUser(User);
				new UserPreferencies().SdkUseComponent(currentUser.Id, newNode.ComponentName);

				return Json(createdNode);
			}
			catch (Exception exception)
			{
				return LogErrorAndReturn500(exception, "CreatePageBodyNode API Method Error");
			}
		}

		//[AllowAnonymous] //Needed only when webcomponent development
		[Route("api/v3.0/page/{pageId}/node/{nodeId}/update")]
		[HttpPost]
		public ActionResult UpdatePageBodyNode(Guid pageId, Guid nodeId, [FromBody] PageBodyNode node)
		{
			try
			{
				var pageSrv = new PageService();

				ErpPage page = pageSrv.GetPage(pageId);
				if (page == null) //page not found
					return NotFound();

				var pageNodes = pageSrv.GetPageNodes(pageId);
				var existingNode = pageNodes.SingleOrDefault(x => x.Id == nodeId);
				if (existingNode == null)
					return NotFound();

				if (existingNode.ParentId != null && node.ParentId == null)
					throw new Exception("There is only one root node and cannot update parent to null. Check for error.");

				if (nodeId == node.ParentId)
				{
					throw new Exception("Node Id and Parent Id cannot be the same");
				}

				pageSrv.UpdatePageBodyNode(nodeId, node.ParentId, pageId, node.NodeId, node.Weight,
					node.ComponentName, node.ContainerId, node.Options);

				pageNodes = pageSrv.GetPageNodes(pageId);
				return Json(pageNodes);
			}
			catch (Exception exception)
			{
				return LogErrorAndReturn500(exception, "UpdatePageBodyNode API Method Error");
			}
		}

		//[AllowAnonymous] //Needed only when webcomponent development
		[Route("api/v3.0/page/{pageId}/node/{nodeId}/move")]
		[HttpPost]
		public ActionResult MovePageBodyNode(Guid pageId, Guid nodeId, [FromBody] MovedNodeInfo moveInfo)
		{
			try
			{
				var pageSrv = new PageService();

				ErpPage page = pageSrv.GetPage(pageId);
				if (page == null) //page not found
					return NotFound();

				if (moveInfo == null)
				{
					return BadRequest("MoveInfo cannot be restored");
				}


				var pageNodes = pageSrv.GetPageNodes(pageId);

				var movedNode = pageNodes.First(x => x.Id == nodeId);
				movedNode.ParentId = moveInfo.NewParentNodeId;
				movedNode.ContainerId = moveInfo.NewContainerId;
				movedNode.Weight = moveInfo.NewIndex + 1; //Convert index to weight
				var nodesToBeUpdated = new List<Guid>();
				pageNodes = Utils.PageUtils.RecalculateContainerNodeWeights(pageNodes, out nodesToBeUpdated, nodeId);

				//Update Nodes
				foreach (var updatedNodeId in nodesToBeUpdated)
				{
					var updatedNode = pageNodes.First(x => x.Id == updatedNodeId);

					if (updatedNodeId == updatedNode.ParentId)
					{
						throw new Exception("Node Id and Parent Id cannot be the same");
					}

					pageSrv.UpdatePageBodyNode(updatedNodeId, updatedNode.ParentId, pageId, updatedNode.NodeId,
						updatedNode.Weight, updatedNode.ComponentName, updatedNode.ContainerId, updatedNode.Options);
				}

				pageNodes = pageSrv.GetPageNodes(pageId);
				return Json(pageNodes);
			}
			catch (Exception exception)
			{
				return LogErrorAndReturn500(exception, "MovePageBodyNode API Method Error");
			}
		}

		//[AllowAnonymous] //Needed only when webcomponent development
		[Route("api/v3.0/page/{pageId}/node/{nodeId}/delete")]
		[HttpPost]
		public ActionResult DeletePageBodyNode(Guid pageId, Guid nodeId)
		{
			try
			{
				var pageSrv = new PageService();
				ErpPage page = pageSrv.GetPage(pageId);
				if (page == null) //page not found
					return NotFound();

				var pageNodes = pageSrv.GetPageNodes(pageId);
				if (!pageNodes.Any(x => x.Id == nodeId))
					return NotFound();

				pageSrv.DeletePageBodyNode(nodeId);

				pageNodes = pageSrv.GetPageNodes(pageId);
				return Json(pageNodes);
			}
			catch (Exception exception)
			{
				return LogErrorAndReturn500(exception, "DeletePageBodyNode API Method Error");
			}
		}

		//[AllowAnonymous] //Needed only when webcomponent development
		[Route("api/v3.0/page/{pageId}/node/{nodeId}/options/update")]
		[HttpPost]
		public ActionResult UpdatePageBodyNodeOptions(Guid pageId, Guid nodeId, [FromBody] JObject options)
		{
			try
			{
				if (options == null)
					return NotFound();

				var pageSrv = new PageService();

				ErpPage page = pageSrv.GetPage(pageId);
				if (page == null) //page not found
					return NotFound();

				pageSrv.UpdatePageBodyNodeOptions(nodeId, options.ToString());

				var updatedNode = pageSrv.GetPageNodeById(nodeId);
				var pageNodes = pageSrv.GetPageNodes(updatedNode.PageId);
				return Json(pageNodes);
			}
			catch (Exception exception)
			{
				return LogErrorAndReturn500(exception, "UpdatePageBodyNodeOptions API Method Error");
			}
		}

		//[AllowAnonymous] //Needed only when webcomponent development
		[Route("api/v3.0/pc/{fullComponentName}/view/{renderMode}")]
		[HttpPost]
		public ActionResult PageComponentRenderViews(string fullComponentName, string renderMode, [FromBody] JObject options,
			[FromQuery] Guid? nid = null, [FromQuery] Guid? pid = null, [FromQuery] Guid? entityId = null, [FromQuery] Guid? recordId = null)
		{
			try
			{
				var validationResult = ValidateRenderRequest(renderMode, pid);
				if (validationResult != null)
					return validationResult;

				var type = ResolvePageComponentType(fullComponentName);
				if (type == null)
					return NotFound();

				var pageServ = new PageService();
				PageBodyNode pagebodyNode = null;
				ErpPage page = null;
				PageDataModel pageModel = null;

				ApplySimulatedRouteData(entityId, pid, recordId);

				if (pid != null)
				{
					page = pageServ.GetPage(pid ?? Guid.Empty);

					if (nid != null)
					{
						pagebodyNode = pageServ.GetPageNodeById(nid ?? Guid.Empty);
					}
					else
					{
						pagebodyNode = PageUtils.GetAjaxPageBodyNode(fullComponentName, pid ?? Guid.Empty, JsonConvert.SerializeObject(options));
					}

					pageModel = BuildSimulationPageModel(page, entityId, recordId);
				}

				return DispatchComponentView(type, renderMode, pagebodyNode, pageModel, options);
			}
			catch (Exception exception)
			{
				return LogErrorAndReturn500(exception, "PageComponentRenderViews API Method Error");
			}
		}

		//[AllowAnonymous] //Needed only when webcomponent development
		[Route("api/v3.0/pc/{fullComponentName}/resource/{filename}")]
		[HttpGet]
		public ActionResult PageComponentServiceJs(string fullComponentName, string filename)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(fullComponentName))
					return NotFound();

				var assembly = FileService.GetTypeAssembly(fullComponentName);
				if (assembly == null)
					return NotFound();

				if (!FileService.EmbeddedResourceExists(filename, fullComponentName, assembly))
					return NotFound();

				var content = FileService.GetEmbeddedTextResource(filename, fullComponentName, assembly);
				switch (filename)
				{
					case "service.js":
						return Content(content, "text/javascript");
					case "options.html":
					case "design.html":
						return Content(content, "text/html");
				}

				return NotFound();
			}
			catch (Exception exception)
			{
				return LogErrorAndReturn500(exception, "PageComponentServiceJs API Method Error");
			}
		}

		[AllowAnonymous]
		[Route("api/v3.0/p/core/styles.css")]
		[ResponseCache(NoStore = false, Duration = 30 * 24 * 3600)]
		[HttpGet]
		public ContentResult StylesCss()
		{
			try
			{
				var cssContent = "";

				if (String.IsNullOrWhiteSpace(ErpAppContext.Current.StylesContent))
				{
					new ThemeService().GenerateStylesContent();
				}

				cssContent = ErpAppContext.Current.StylesContent;
				return Content(cssContent, "text/css");
			}
			catch (Exception ex)
			{
				new Log().Create(LogType.Error, "StylesCss API Method Error", ex);
				throw;
			}
		}


		//[Route("api/v3.0/p/core/select/font-awesome-icons")]
		//[HttpGet]
		//public ActionResult GetSelectCases([FromQuery]string search,[FromQuery]int page = 1)
		//{
		//	var pageSize = 10;
		//	var response = new ResponseModel();
		//	response.Timestamp = DateTime.UtcNow;
		//	try
		//	{
		//		var icons = RenderService.FontAwesomeIcons;
		//		var iconTotal = icons.Count();
		//		if(!String.IsNullOrWhiteSpace(search)){
		//			var filteredIcons = icons.FindAll(x=> x.Class.Contains(search) || x.Name.Contains(search)).ToList();
		//			iconTotal = filteredIcons.Count();
		//			icons = filteredIcons.Skip((page-1)*pageSize).Take(pageSize).ToList();
		//		}
		//		else{
		//			icons = icons.Skip((page-1)*pageSize).Take(pageSize).ToList();
		//		}
		//		var result = new EntityRecord();

		//		result["results"] = icons;
		//		result["pagination"] = new EntityRecord(); // more => true, false
		//		var moreRecord = new EntityRecord();
		//		moreRecord["more"] = false;

		//		if(iconTotal > page*pageSize){
		//			moreRecord["more"] = true;
		//		}

		//		result["pagination"] = moreRecord;


		//		response.Object = result;
		//		response.Success = true;
		//		response.Message = "";
		//	}
		//	catch (Exception ex)
		//	{
		//		response.Success = false;
		//		response.Message = ex.Message;
		//	}
		//	return Json(response);
		//}

		//[AllowAnonymous]
		//[Route("api/v3.0/p/core/framework.css")]
		//[ResponseCache(NoStore = false, Duration = 30 * 24 * 3600)]
		//[HttpGet]
		//public ContentResult FrameworkCss()
		//{
		//	try
		//	{
		//		var cssContent = "";

		//		if (String.IsNullOrWhiteSpace(ErpAppContext.Current.StyleFrameworkContent))
		//		{
		//			new ThemeService().GenerateStyleFrameworkContent();
		//		}

		//		cssContent = ErpAppContext.Current.StyleFrameworkContent;
		//		return Content(cssContent, "text/css");
		//	}
		//	catch (Exception ex)
		//	{
		//		new Log().Create(LogType.Error, "FrameworkCss API Method Error", ex);
		//		throw ex;
		//	}
		//}


		#region << UI component support >>

		[Produces("application/json")]
		[Route("api/v3.0/p/core/related-field-multiselect")]
		[AcceptVerbs("GET", "POST")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult RelatedFieldMultiSelect(string entityName, string fieldName, string search = "", int page = 1)
		{
			try
			{
				var response = new TypeaheadResponse();
				var errorResponse = new ResponseModel();
				var recMan = new RecordManager();
				if (String.IsNullOrWhiteSpace(entityName))
				{
					errorResponse.Message = "entity name is required";
					Response.StatusCode = (int)HttpStatusCode.BadRequest;
					return Json(errorResponse);
				}
				if (String.IsNullOrWhiteSpace(fieldName))
				{
					errorResponse.Message = "field name is required";
					Response.StatusCode = (int)HttpStatusCode.BadRequest;
					return Json(errorResponse);
				}

				var query = BuildRelatedFieldQuery(entityName, fieldName, search, page);

				var findResult = recMan.Find(query);
				if (!findResult.Success)
				{
					errorResponse.Message = findResult.Message;
					Response.StatusCode = (int)HttpStatusCode.BadRequest;
					return Json(errorResponse);
				}

				PopulateRelatedFieldResults(response, findResult.Object.Data, entityName, fieldName);
				return new JsonResult(response);
			}
			catch (Exception ex)
			{
				new Log().Create(LogType.Error, "RelatedFieldMultiSelect API Method Error", ex);
				throw;
			}
		}

		[Produces("application/json")]
		[Route("api/v3.0/p/core/select-field-add-option")]
		[AcceptVerbs("PUT")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult SelectFieldAddOption([FromBody] JObject submitObj)
		{
			var response = new ResponseModel();
			var recMan = new RecordManager();
			var entMan = new EntityManager();
			var entityName = "";
			var fieldName = "";
			var optionValue = "";
			try
			{
				ParseSelectFieldAddOptionSubmit(submitObj, ref entityName, ref fieldName, ref optionValue);
				var fieldMeta = ResolveSelectFieldMeta(entMan, entityName, fieldName, out var entityMeta);
				var optionExists = SelectFieldOptionExists(fieldMeta, optionValue);

				if (optionExists)
				{
					throw new Exception("Record not found!");
				}

				AddSelectFieldOption(entMan, entityMeta, fieldMeta, optionValue);

				response.Success = true;
				response.Message = "Record created successfully";
			}
			catch (Exception ex)
			{
				new Log().Create(LogType.Error, "RelatedFieldMultiSelect API Method Error", ex);
				response.Success = false;
				response.Message = ex.Message;
			}
			return new JsonResult(response);
		}

		[Produces("text/html")]
		[Route("api/v3.0/{lang}/p/core/ui/field-table-data/generate/preview")]
		[AcceptVerbs("POST")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult FieldTableDataPreview([FromRoute] string lang, [FromBody] JObject submitObj)
		{
			var hasHeader = true;
			var hasHeaderColumn = false;
			string csvData = "";
			string delimiterName = "";
			#region << Init SubmitObj >>
			ParseFieldTableDataPreviewSubmit(submitObj, ref hasHeader, ref hasHeaderColumn, ref csvData, ref delimiterName);

			var records = new List<dynamic>();
			try
			{
				records = WebVella.TagHelpers.Utilities.WvHelpers.GetCsvData(csvData, hasHeader, delimiterName);
			}
			//catch (CsvHelperException ex)
			//{
			//	//ex.Data.Values has more info...

			//	if (lang == "bg")
			//	{
			//		return Content("<div class='alert alert-danger p-2'>Грешен формат на данните. Опитайте с друг разделител.</div>");
			//	}
			//	else
			//	{
			//		return Content("<div class='alert alert-danger p-2'>Error in parsing data. Check another delimiter</div>");
			//	}
			//}
			catch
			{
				if (lang == "bg")
				{
					return Content("<div class='alert alert-danger p-2'>Грешен формат на данните. Опитайте с друг разделител.</div>");
				}
				else
				{
					return Content("<div class='alert alert-danger p-2'>Error in parsing data. Check another delimiter</div>");
				}
			}

			#endregion

			var result = new EntityRecord();
			result["hasHeader"] = hasHeader;
			result["hasHeaderColumn"] = hasHeaderColumn;
			result["data"] = records;
			result["lang"] = lang;
			return PartialView("FieldTableDataPreview", result);
		}



		#endregion

		#region << Entity Meta >>

		// Get all entity definitions
		// GET: api/v3/en_US/meta/entity/list/
		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/meta/entity/list")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetEntityMetaList(string hash = null)
		{
			var bo = entMan.ReadEntities();

			//check hash and clear data if hash match
			if (bo.Success && bo.Object != null && !string.IsNullOrWhiteSpace(hash) && bo.Hash == hash)
				bo.Object = null;

			return DoResponse(bo);
		}

		// Get entity meta
		// GET: api/v3/en_US/meta/entity/id/{entityId}/
		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/meta/entity/id/{entityId}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetEntityMetaById(Guid entityId)
		{
			return DoResponse(entMan.ReadEntity(entityId));
		}

		// Get entity meta
		// GET: api/v3/en_US/meta/entity/{name}/
		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/meta/entity/{Name}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetEntityMeta(string Name)
		{
			return DoResponse(entMan.ReadEntity(Name));
		}


		// Create an entity
		// POST: api/v3/en_US/meta/entity
		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/meta/entity")]
		[ResponseCache(NoStore = true, Duration = 0)]
		[Authorize(Roles = "administrator")]
		public IActionResult CreateEntity([FromBody] InputEntity submitObj)
		{
			var entity = new InputEntity
			{
				Name = submitObj.Name,
				Label = submitObj.Label,
				LabelPlural = submitObj.LabelPlural,
				System = submitObj.System,
				IconName = submitObj.IconName,
				//Weight = submitObj.Weight,
				RecordPermissions = submitObj.RecordPermissions
			};

			return DoResponse(entMan.CreateEntity(entity));
		}

		// Create an entity
		// POST: api/v3/en_US/meta/entity
		[AcceptVerbs(new[] { "PATCH" }, Route = "api/v3/en_US/meta/entity/{StringId}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		[Authorize(Roles = "administrator")]
		public IActionResult PatchEntity(string StringId, [FromBody] JObject submitObj)
		{
			FieldResponse response = new FieldResponse();
			InputEntity entity = new InputEntity();

			try
			{
				if (!Guid.TryParse(StringId, out Guid entityId))
				{
					response.Errors.Add(new ErrorModel("id", StringId, "id parameter is not valid Guid value"));
					return DoResponse(response);
				}

				DbEntity storageEntity = DbContext.Current.EntityRepository.Read(entityId);
				if (storageEntity == null)
				{
					response.Timestamp = DateTime.UtcNow;
					response.Success = false;
					response.Message = "Entity with such Name does not exist!";
					return DoBadRequestResponse(response);
				}
				entity = storageEntity.MapTo<Entity>().MapTo<InputEntity>();

				Type inputEntityType = entity.GetType();

				ValidatePatchEntityProperties(submitObj, inputEntityType, response);

				if (response.Errors.Count > 0)
					return DoBadRequestResponse(response);

				InputEntity inputEntity = submitObj.ToObject<InputEntity>();

				ApplyPatchEntityProperties(entity, inputEntity, submitObj);
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:PatchEntity", e);
				return DoBadRequestResponse(response, "Input object is not in valid format! It cannot be converted.", e);
			}

			return DoResponse(entMan.UpdateEntity(entity));
		}


		// Delete an entity
		// DELETE: api/v3/en_US/meta/entity/{id}
		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "DELETE" }, Route = "api/v3/en_US/meta/entity/{StringId}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult DeleteEntity(string StringId)
		{
			EntityResponse response = new EntityResponse();

			// Parse each string representation.
			Guid id = Guid.Empty;
			if (Guid.TryParse(StringId, out Guid newGuid))
			{
				response = entMan.DeleteEntity(newGuid);
			}
			else
			{
				response.Success = false;
				response.Message = "The entity Id should be a valid Guid";
				HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
			}
			return DoResponse(response);
		}

		#endregion

		#region << Entity Fields >>

		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/meta/entity/{Id}/field")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult CreateField(string Id, [FromBody] JObject submitObj)
		{
			FieldResponse response = new FieldResponse();

			if (!Guid.TryParse(Id, out Guid entityId))
			{
				response.Errors.Add(new ErrorModel("id", Id, "id parameter is not valid Guid value"));
				return DoResponse(response);
			}

			InputField field = new InputGuidField();
			try
			{
				field = InputField.ConvertField(submitObj);
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:CreateField", e);
				return DoBadRequestResponse(response, "Input object is not in valid format! It cannot be converted.", e);
			}

			return DoResponse(entMan.CreateField(entityId, field));
		}

		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "PUT" }, Route = "api/v3/en_US/meta/entity/{Id}/field/{FieldId}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult UpdateField(string Id, string FieldId, [FromBody] JObject submitObj)
		{
			FieldResponse response = new FieldResponse();

			if (!Guid.TryParse(Id, out Guid entityId))
			{
				response.Errors.Add(new ErrorModel("id", Id, "id parameter is not valid Guid value"));
				return DoResponse(response);
			}

			if (!Guid.TryParse(FieldId, out Guid fieldId))
			{
				response.Errors.Add(new ErrorModel("id", FieldId, "FieldId parameter is not valid Guid value"));
				return DoResponse(response);
			}

			InputField field = new InputGuidField();
			FieldType fieldType = FieldType.GuidField;

			var fieldTypeProp = submitObj.Properties().SingleOrDefault(k => k.Name.ToLower() == "fieldtype");
			if (fieldTypeProp != null)
			{
				fieldType = (FieldType)Enum.ToObject(typeof(FieldType), fieldTypeProp.Value.ToObject<int>());
			}

			Type inputFieldType = InputField.GetFieldType(fieldType);

			foreach (var prop in submitObj.Properties())
			{
				if (prop.Name.ToLower() == "entityname")
					continue;

				int count = inputFieldType.GetProperties().Where(n => n.Name.ToLower() == prop.Name.ToLower()).Count();
				if (count < 1)
					response.Errors.Add(new ErrorModel(prop.Name, prop.Value.ToString(), "Input object contains property that is not part of the object model."));
			}

			if (response.Errors.Count > 0)
				return DoBadRequestResponse(response);

			try
			{
				field = InputField.ConvertField(submitObj);
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:UpdateField", e);
				return DoBadRequestResponse(response, "Input object is not in valid format! It cannot be converted.", e);
			}

			return DoResponse(entMan.UpdateField(entityId, field));
		}

		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "PATCH" }, Route = "api/v3/en_US/meta/entity/{Id}/field/{FieldId}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult PatchField(string Id, string FieldId, [FromBody] JObject submitObj)
		{
			FieldResponse response = new FieldResponse();
			Entity entity = new Entity();
			InputField field = new InputGuidField();

			try
			{
				var entityResult = ResolvePatchFieldEntity(Id, FieldId, response, ref entity);
				if (entityResult != null)
					return entityResult;

				var fieldTypeResult = ResolvePatchFieldType(submitObj, response, out var fieldType);
				if (fieldTypeResult != null)
					return fieldTypeResult;

				ValidatePatchFieldProperties(submitObj, fieldType, response);

				if (response.Errors.Count > 0)
					return DoBadRequestResponse(response);

				InputField inputField = InputField.ConvertField(submitObj);

				field = ApplyPatchFieldProperties(submitObj, fieldType, inputField);
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:PatchField", e);
				return DoBadRequestResponse(response, "Input object is not in valid format! It cannot be converted.", e);
			}

			return DoResponse(entMan.UpdateField(entity, field));
		}

		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "DELETE" }, Route = "api/v3/en_US/meta/entity/{Id}/field/{FieldId}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult DeleteField(string Id, string FieldId)
		{
			FieldResponse response = new FieldResponse();

			if (!Guid.TryParse(Id, out Guid entityId))
			{
				response.Errors.Add(new ErrorModel("id", Id, "id parameter is not valid Guid value"));
				return DoResponse(response);
			}

			if (!Guid.TryParse(FieldId, out Guid fieldId))
			{
				response.Errors.Add(new ErrorModel("id", FieldId, "FieldId parameter is not valid Guid value"));
				return DoResponse(response);
			}

			return DoResponse(entMan.DeleteField(entityId, fieldId));
		}

		#endregion

		#region << Relation Meta >>
		// Get all entity relation definitions
		// GET: api/v3/en_US/meta/relation/list/
		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/meta/relation/list")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetEntityRelationMetaList(string hash = null)
		{
			var response = new EntityRelationManager().Read();

			//check hash and clear data if hash match
			if (response.Success && response.Object != null && !string.IsNullOrWhiteSpace(hash) && response.Hash == hash)
				response.Object = null;

			return DoResponse(response);
		}

		// Get entity relation meta
		// GET: api/v3/en_US/meta/relation/{name}/
		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/meta/relation/{name}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetEntityRelationMeta(string name)
		{
			return DoResponse(new EntityRelationManager().Read(name));
		}


		// Create an entity relation
		// POST: api/v3/en_US/meta/relation
		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/meta/relation")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult CreateEntityRelation([FromBody] JObject submitObj)
		{
			try
			{
				if (submitObj["id"].IsNullOrEmpty())
					submitObj["id"] = Guid.NewGuid();
				var relation = submitObj.ToObject<EntityRelation>();
				return DoResponse(new EntityRelationManager().Create(relation));
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:CreateEntityRelation", e);
				return DoBadRequestResponse(new EntityRelationResponse(), null, e);
			}
		}

		// Update an entity relation
		// PUT: api/v3/en_US/meta/relation/id
		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "PUT" }, Route = "api/v3/en_US/meta/relation/{RelationIdString}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult UpdateEntityRelation(string RelationIdString, [FromBody] JObject submitObj)
		{
			FieldResponse response = new FieldResponse();

			if (!Guid.TryParse(RelationIdString, out Guid relationId))
			{
				response.Errors.Add(new ErrorModel("id", RelationIdString, "id parameter is not valid Guid value"));
				return DoResponse(response);
			}

			try
			{
				var relation = submitObj.ToObject<EntityRelation>();
				return DoResponse(new EntityRelationManager().Update(relation));
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:UpdateEntityRelation", e);
				return DoBadRequestResponse(new EntityRelationResponse(), null, e);
			}
		}

		// Delete an entity relation
		// DELETE: api/v3/en_US/meta/relation/{idToken}
		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "DELETE" }, Route = "api/v3/en_US/meta/relation/{idToken}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult DeleteEntityRelation(string idToken)
		{
			Guid id = Guid.Empty;
			if (Guid.TryParse(idToken, out Guid newGuid))
			{
				return DoResponse(new EntityRelationManager().Delete(newGuid));
			}
			else
			{
				return DoBadRequestResponse(new EntityRelationResponse(), "The entity relation Id should be a valid Guid", null);
			}

		}

		#endregion

		#region << Records >>

		// Update an entity record relation records for origin record
		// POST: api/v3/en_US/record/relation
		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/record/relation")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult UpdateEntityRelationRecord([FromBody] InputEntityRelationRecordUpdateModel model)
		{

			var recMan = new RecordManager();
			var entMan = new EntityManager();
			BaseResponseModel response = new BaseResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };

			EntityRelation relation = null;
			var relationValidation = ValidateUpdateRelationModel(model, response, ref relation);
			if (relationValidation != null)
				return relationValidation;

			var originEntity = entMan.ReadEntity(relation.OriginEntityId).Object;
			var targetEntity = entMan.ReadEntity(relation.TargetEntityId).Object;
			var originField = originEntity.Fields.Single(x => x.Id == relation.OriginFieldId);
			var targetField = targetEntity.Fields.Single(x => x.Id == relation.TargetFieldId);

			if (model.DetachTargetFieldRecordIds != null && model.DetachTargetFieldRecordIds.Any() && targetField.Required && relation.RelationType != EntityRelationType.ManyToMany)
			{
				response.Errors.Add(new ErrorModel { Message = "Cannot detach records, when target field is required.", Key = "originFieldRecordId" });
				response.Success = false;
				return DoResponse(response);
			}

			EntityQuery query = new EntityQuery(originEntity.Name, "id," + originField.Name, EntityQuery.QueryEQ("id", model.OriginFieldRecordId), null, null, null);
			QueryResponse result = recMan.Find(query);
			if (result.Object.Data.Count == 0)
			{
				response.Errors.Add(new ErrorModel { Message = "Origin record was not found. Id=[" + model.OriginFieldRecordId + "]", Key = "originFieldRecordId" });
				response.Success = false;
				return DoResponse(response);
			}

			var originRecord = result.Object.Data[0];
			object originValue = originRecord[originField.Name];

			var attachTargetRecords = new List<EntityRecord>();
			var detachTargetRecords = new List<EntityRecord>();

			var attachValidation = CollectAttachTargetRecords(model, response, recMan, targetEntity, targetField, attachTargetRecords);
			if (attachValidation != null)
				return attachValidation;

			var detachValidation = CollectDetachTargetRecords(model, response, recMan, targetEntity, targetField, detachTargetRecords);
			if (detachValidation != null)
				return detachValidation;

			var applyResult = ApplyUpdateRelationChanges(response, recMan, relation, targetEntity, targetField, originValue, attachTargetRecords, detachTargetRecords);
			if (applyResult != null)
				return applyResult;

			return DoResponse(response);
		}


		// Update an entity record relation records for target record
		// POST: api/v3/en_US/record/relation/reverse
		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/record/relation/reverse")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult UpdateEntityRelationRecordReverse([FromBody] InputEntityRelationRecordReverseUpdateModel model)
		{

			var recMan = new RecordManager();
			var entMan = new EntityManager();
			BaseResponseModel response = new BaseResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };

			EntityRelation relation = null;
			var relationValidation = ValidateUpdateRelationReverseModel(model, response, ref relation);
			if (relationValidation != null)
				return relationValidation;

			var originEntity = entMan.ReadEntity(relation.OriginEntityId).Object;
			var targetEntity = entMan.ReadEntity(relation.TargetEntityId).Object;
			var originField = originEntity.Fields.Single(x => x.Id == relation.OriginFieldId);
			var targetField = targetEntity.Fields.Single(x => x.Id == relation.TargetFieldId);

			if (model.DetachOriginFieldRecordIds != null && model.DetachOriginFieldRecordIds.Any() && originField.Required && relation.RelationType != EntityRelationType.ManyToMany)
			{
				response.Errors.Add(new ErrorModel { Message = "Cannot detach records, when origin field is required.", Key = "originFieldRecordId" });
				response.Success = false;
				return DoResponse(response);
			}

			EntityQuery query = new EntityQuery(targetEntity.Name, "id," + targetField.Name, EntityQuery.QueryEQ("id", model.TargetFieldRecordId), null, null, null);
			QueryResponse result = recMan.Find(query);
			if (result.Object.Data.Count == 0)
			{
				response.Errors.Add(new ErrorModel { Message = "Target record was not found. Id=[" + model.TargetFieldRecordId + "]", Key = "targetFieldRecordId" });
				response.Success = false;
				return DoResponse(response);
			}

			var targetRecord = result.Object.Data[0];
			object targetValue = targetRecord[targetField.Name];

			var attachOriginRecords = new List<EntityRecord>();
			var detachOriginRecords = new List<EntityRecord>();

			var attachValidation = CollectAttachOriginRecords(model, response, recMan, originEntity, originField, attachOriginRecords);
			if (attachValidation != null)
				return attachValidation;

			var detachValidation = CollectDetachOriginRecords(model, response, recMan, originEntity, originField, detachOriginRecords);
			if (detachValidation != null)
				return detachValidation;

			var applyResult = ApplyUpdateRelationReverseChanges(response, recMan, relation, originEntity, originField, targetValue, attachOriginRecords, detachOriginRecords);
			if (applyResult != null)
				return applyResult;

			return DoResponse(response);
		}


		// Get an entity record list
		// GET: api/v3/en_US/record/{entityName}/list
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/record/{entityName}/{recordId}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetRecord(Guid recordId, string entityName, string fields = "*")
		{
			QueryObject filterObj = EntityQuery.QueryEQ("id", recordId);

			EntityQuery query = new EntityQuery(entityName, fields, filterObj, null, null, null);

			QueryResponse result = recMan.Find(query);
			if (!result.Success)
				return DoResponse(result);

			return Json(result);
		}

		// Get an entity record list
		// GET: api/v3/en_US/record/{entityName}/list
		[AcceptVerbs(new[] { "DELETE" }, Route = "api/v3/en_US/record/{entityName}/{recordId}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult DeleteRecord(Guid recordId, string entityName)
		{
			//Create transaction
			var result = new QueryResponse();
			using (var connection = DbContext.Current.CreateConnection())
			{
				try
				{
					connection.BeginTransaction();
					result = recMan.DeleteRecord(entityName, recordId);
					connection.CommitTransaction();
				}
				catch (Exception ex)
				{
					connection.RollbackTransaction();
					new LogService().Create(Diagnostics.LogType.Error, "TErpApi:DeleteRecord", ex);
					var response = new ResponseModel
					{
						Success = false,
						Timestamp = DateTime.UtcNow,
						Message = "Error while delete the record: " + ex.Message,
						Object = null
					};
					return Json(response);
				}
			}

			return DoResponse(result);
		}

		// Get an entity records by field and regex
		// GET: api/v3/en_US/record/{entityName}/regex
		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/record/{entityName}/regex/{fieldName}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetRecordsByFieldAndRegex(string fieldName, string entityName, [FromBody] EntityRecord patternObj)
		{

			QueryObject filterObj = EntityQuery.QueryRegex(fieldName, patternObj["pattern"]);

			EntityQuery query = new EntityQuery(entityName, "*", filterObj, null, null, null);

			QueryResponse result = recMan.Find(query);
			if (!result.Success)
				return DoResponse(result);
			return Json(result);
		}


		// Create an entity record
		// POST: api/v3/en_US/record/{entityName}
		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/record/{entityName}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult CreateEntityRecord(string entityName, [FromBody] EntityRecord postObj)
		{
			//Find and change properties starting with _$ to $$ - angular does not post $$ propery names
			postObj = Helpers.FixDoubleDollarSignProblem(postObj);

			if (!postObj.GetProperties().Any(x => x.Key == "id"))
				postObj["id"] = Guid.NewGuid();
			else if (string.IsNullOrEmpty(postObj["id"] as string))
				postObj["id"] = Guid.NewGuid();


			//Create transaction
			var result = new QueryResponse();
			using (var connection = DbContext.Current.CreateConnection())
			{
				try
				{
					connection.BeginTransaction();
					result = recMan.CreateRecord(entityName, postObj);
					connection.CommitTransaction();
				}
				catch (Exception ex)
				{
					connection.RollbackTransaction();
					new LogService().Create(Diagnostics.LogType.Error, "TErpApi:CreateEntityRecord", ex);
					var response = new ResponseModel
					{
						Success = false,
						Timestamp = DateTime.UtcNow,
						Message = "Error while saving the record: " + ex.Message,
						Object = null
					};
					return Json(response);
				}
			}

			return DoResponse(result);
		}

		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/record/{entityName}/with-relation/{relationName}/{relatedRecordId}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult CreateEntityRecordWithRelation(string entityName, string relationName, Guid relatedRecordId, [FromBody] EntityRecord postObj)
		{
			var validationErrors = new List<ErrorModel>();

			EntityRelation relation = null;
			var relatedRecord = new EntityRecord();
			ValidateCreateRelationInput(entityName, relationName, relatedRecordId, validationErrors, ref relation, ref relatedRecord);


			if (postObj == null)
				postObj = new EntityRecord();

			if (validationErrors.Count > 0)
			{
				var response = new ResponseModel
				{
					Success = false,
					Timestamp = DateTime.UtcNow,
					Errors = validationErrors,
					Message = "Validation error occurred!",
					Object = null
				};
				return Json(response);
			}

			if (!postObj.GetProperties().Any(x => x.Key == "id"))
				postObj["id"] = Guid.NewGuid();
			else if (string.IsNullOrEmpty(postObj["id"] as string))
				postObj["id"] = Guid.NewGuid();


			return ApplyCreateRelationTransaction(relation, entityName, relatedRecordId, postObj, relatedRecord);
		}


		// Update an entity record
		// PUT: api/v3/en_US/record/{entityName}/{recordId}
		[AcceptVerbs(new[] { "PUT" }, Route = "api/v3/en_US/record/{entityName}/{recordId}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult UpdateEntityRecord(string entityName, Guid recordId, [FromBody] EntityRecord postObj)
		{
			//Find and change properties starting with _$ to $$ - angular does not post $$ propery names
			postObj = Helpers.FixDoubleDollarSignProblem(postObj);


			if (!postObj.Properties.ContainsKey("id"))
			{
				postObj["id"] = recordId;
			}

			//clear authentication cache
			if (entityName == "user")
			{
				throw new Exception("Management of user record should be implemented");
				//WebSecurityUtil.RemoveIdentityFromCache(recordId);
			}
			//Create transaction
			var result = new QueryResponse();
			using (var connection = DbContext.Current.CreateConnection())
			{
				try
				{
					connection.BeginTransaction();
					result = recMan.UpdateRecord(entityName, postObj);
					connection.CommitTransaction();
				}
				catch (Exception ex)
				{
					connection.RollbackTransaction();
					new LogService().Create(Diagnostics.LogType.Error, "TErpApi:UpdateEntityRecord", ex);
					var response = new ResponseModel
					{
						Success = false,
						Timestamp = DateTime.UtcNow,
						Message = "Error while saving the record: " + ex.Message,
						Object = null
					};
					return Json(response);
				}
			}

			return DoResponse(result);
		}

		// Patch an entity record
		// PATCH: api/v3/en_US/record/{entityName}/{recordId}
		[AcceptVerbs(new[] { "PATCH" }, Route = "api/v3/en_US/record/{entityName}/{recordId}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult PatchEntityRecord(string entityName, Guid recordId, [FromBody] EntityRecord postObj)
		{
			//clear authentication cache
			if (entityName == "user")
			{
				throw new Exception("Management of user record should be implemented");
				//WebSecurityUtil.RemoveIdentityFromCache(recordId);
			}
			postObj["id"] = recordId;

			//Create transaction
			var result = new QueryResponse();
			using (var connection = DbContext.Current.CreateConnection())
			{
				try
				{
					connection.BeginTransaction();
					result = recMan.UpdateRecord(entityName, postObj);
					connection.CommitTransaction();
				}
				catch (Exception ex)
				{
					connection.RollbackTransaction();
					new LogService().Create(Diagnostics.LogType.Error, "TErpApi:PatchEntityRecord", ex);
					var response = new ResponseModel
					{
						Success = false,
						Timestamp = DateTime.UtcNow,
						Message = "Error while saving the record: " + ex.Message,
						Object = null
					};
					return Json(response);
				}
			}

			return DoResponse(result);
		}

		// Shared configuration and helpers for the bulk record actions below.

		// Maximum number of records a single bulk request may carry. Selection stays scoped to the
		// rendered grid page, so a fixed upper bound protects the server from oversized or abusive batches.
		private const int BulkActionMaxRecords = 1000;

		// The approved archive field. The bulk-archive action writes only a field named in the trusted
		// allowlist below, so a caller cannot redirect the write to another field on the entity.
		private const string BulkArchiveApprovedField = "is_archived";
		private static readonly HashSet<string> BulkArchiveAllowedFields =
			new HashSet<string>(StringComparer.OrdinalIgnoreCase) { BulkArchiveApprovedField };

		// Confirm a destructive bulk request comes from this application, not a cross-site page. Cookie
		// authentication alone does not stop a cross-site request forgery, so every bulk route checks the
		// request origin before it changes data. The method reads the Origin header first, falls back to the
		// Referer, compares the host against the request host, and rejects a request whose host differs or
		// that carries neither header. The grid posts from the same origin, so a legitimate call always passes.
		private bool IsBulkRequestOriginTrusted()
		{
			var requestHost = HttpContext.Request.Host.Host;
			if (string.IsNullOrEmpty(requestHost))
				return false;

			var candidate = HttpContext.Request.Headers["Origin"].ToString();
			if (string.IsNullOrWhiteSpace(candidate))
				candidate = HttpContext.Request.Headers["Referer"].ToString();

			if (string.IsNullOrWhiteSpace(candidate))
				return false;

			if (!Uri.TryCreate(candidate, UriKind.Absolute, out var candidateUri))
				return false;

			return string.Equals(candidateUri.Host, requestHost, StringComparison.OrdinalIgnoreCase);
		}

		// Build a fixed 403 response for a bulk request the server refuses on authorization grounds, such as a
		// cross-site origin or a missing field-level update right. The response reuses the bulk envelope with an
		// empty result list and a safe message, so a rejected request never looks like a completed one.
		private IActionResult BulkForbidden(string message)
		{
			var response = new ResponseModel
			{
				Success = false,
				Timestamp = DateTime.UtcNow,
				Message = message,
				Object = new List<BulkRecordActionResultItem>()
			};
			HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
			return Json(response);
		}

		// Confirm the current user may update a field-secured field, not only the entity. RecordManager checks
		// entity-level Update permission, yet a field can carry its own security. When the archive field turns on
		// security, the caller needs a role in the field update allowlist to write it. The administrator and the
		// system user always qualify. A caller who lacks the field right cannot archive through the bulk route,
		// even when the caller holds entity Update permission.
		private bool CurrentUserCanUpdateSecuredField(List<Guid> canUpdateRoleIds)
		{
			var user = SecurityContext.CurrentUser;
			if (user == null)
				return false;

			if (user.Id == SystemIds.SystemUserId || user.IsAdmin)
				return true;

			if (canUpdateRoleIds == null || canUpdateRoleIds.Count == 0)
				return false;

			return user.Roles.Any(role => canUpdateRoleIds.Any(id => id == role.Id));
		}

		// Build a per-record success result with a stable outcome code.
		private static BulkRecordActionResultItem BulkOk(Guid id, string message)
		{
			return new BulkRecordActionResultItem { RecordId = id, Success = true, Code = "ok", Message = message };
		}

		// Build a per-record failure result with a stable outcome code and a safe, fixed message.
		private static BulkRecordActionResultItem BulkFail(Guid id, string code, string message)
		{
			return new BulkRecordActionResultItem { RecordId = id, Success = false, Code = code, Message = message };
		}

		// Translate a data-layer failure into a safe per-record result. A permission denial reports a
		// distinct, stable code, and every other failure reports a generic code. Internal error text
		// never reaches the client.
		private static BulkRecordActionResultItem MapBulkFailure(Guid id, QueryResponse response, string genericMessage)
		{
			if (response != null && response.StatusCode == HttpStatusCode.Forbidden)
				return BulkFail(id, "forbidden", "You do not have permission for this record.");
			return BulkFail(id, "error", genericMessage);
		}

		// Extract the internal failure reason for server-side logging only. The returned text stays in
		// protected logs and never travels to the client.
		private static string DescribeBulkFailure(QueryResponse response)
		{
			if (response == null)
				return "Unknown failure.";
			if (response.Errors != null && response.Errors.Count > 0 && !string.IsNullOrWhiteSpace(response.Errors[0].Message))
				return response.Errors[0].Message;
			return response.Message;
		}

		// Log a bulk per-record failure with safe, non-sensitive context: the action, the entity name,
		// the record id, and the request correlation id. Payloads, secrets, and personal data never
		// enter the log.
		private void LogBulkFailure(string action, string entityName, Guid id, string detail, Exception ex)
		{
			var correlationId = HttpContext != null ? HttpContext.TraceIdentifier : string.Empty;
			var context = "action=" + action + "; entity=" + (entityName ?? string.Empty) + "; recordId=" + id + "; correlationId=" + correlationId;
			var source = "TErpApi:" + action;
			if (ex != null)
				new LogService().Create(Diagnostics.LogType.Error, source, context, ex);
			else
				new LogService().Create(Diagnostics.LogType.Error, source, context, detail ?? string.Empty);
		}

		// Roll back a per-record transaction without masking the original failure. The rollback runs only
		// when a transaction actually started, sits in its own try/catch, and logs any rollback failure
		// on its own so the batch continues to the next record.
		private void SafeRollbackBulk(DbConnection connection, ref bool transactionStarted, string action, string entityName, Guid id)
		{
			if (!transactionStarted)
				return;
			transactionStarted = false;
			try
			{
				connection.RollbackTransaction();
			}
			catch (Exception rollbackEx)
			{
				var correlationId = HttpContext != null ? HttpContext.TraceIdentifier : string.Empty;
				var context = "action=" + action + "; entity=" + (entityName ?? string.Empty) + "; recordId=" + id + "; correlationId=" + correlationId;
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:" + action + ":Rollback", context, rollbackEx);
			}
		}

		// Build a bad-request response for a malformed bulk request. The response returns a safe message,
		// an empty result list, and a 400 status, so a caller cannot mistake a rejected request for a
		// completed one.
		private IActionResult BulkBadRequest(string message)
		{
			var response = new ResponseModel
			{
				Success = false,
				Timestamp = DateTime.UtcNow,
				Message = message,
				Object = new List<BulkRecordActionResultItem>()
			};
			HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
			return Json(response);
		}

		// Build a safe response for an unexpected failure raised while validating the request or resolving
		// entity metadata before the per-record loop. The internal detail goes to the server log only, the
		// client receives a fixed generic message with an empty result list, and the status stays 500, so a
		// pre-loop exception never surfaces a stack trace to the caller.
		private IActionResult BulkPreflightError(string action, string actionNoun, Exception ex)
		{
			var correlationId = HttpContext != null ? HttpContext.TraceIdentifier : string.Empty;
			var context = "action=" + action + "; correlationId=" + correlationId;
			new LogService().Create(Diagnostics.LogType.Error, "TErpApi:" + action + ":Preflight", context, ex);
			var response = new ResponseModel
			{
				Success = false,
				Timestamp = DateTime.UtcNow,
				Message = "The bulk " + actionNoun + " request could not be processed.",
				Object = new List<BulkRecordActionResultItem>()
			};
			HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
			return Json(response);
		}

		// Build the aggregate bulk response with a truthful HTTP status: 200 when every record succeeded,
		// 207 when some succeeded and some failed, and 422 when every record failed. The per-record
		// results always travel in the envelope Object so the client can report each outcome.
		private IActionResult BuildBulkResponse(List<BulkRecordActionResultItem> results, string pastTenseAction)
		{
			var total = results.Count;
			var succeeded = results.Count(x => x.Success);
			var failed = total - succeeded;

			var response = new ResponseModel
			{
				Timestamp = DateTime.UtcNow,
				Object = results
			};

			if (failed == 0)
			{
				response.Success = true;
				response.Message = "All " + total + " record(s) " + pastTenseAction + ".";
				HttpContext.Response.StatusCode = (int)HttpStatusCode.OK;
			}
			else if (succeeded == 0)
			{
				response.Success = false;
				response.Message = "No record could be " + pastTenseAction + ".";
				HttpContext.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
			}
			else
			{
				response.Success = false;
				response.Message = succeeded + " of " + total + " record(s) " + pastTenseAction + ". " + failed + " failed.";
				HttpContext.Response.StatusCode = (int)HttpStatusCode.MultiStatus;
			}

			return Json(response);
		}

		// Validate and normalize a bulk request before any transaction opens. The method rejects a
		// missing body, a blank entity name, a default record id, and an oversized batch, removes
		// duplicate ids so each unique record runs its hooks and transaction once, and confirms the
		// entity exists in trusted metadata. A rejected request yields a safe 400 error result.
		private bool TryNormalizeBulkRequest(BulkRecordActionModel model, string action, out List<Guid> normalizedIds, out Entity entityMeta, out IActionResult errorResult)
		{
			normalizedIds = null;
			entityMeta = null;
			errorResult = null;

			if (model == null || model.RecordIds == null || model.RecordIds.Count == 0)
			{
				errorResult = BulkBadRequest("The request carries no records to " + action + ".");
				return false;
			}

			if (string.IsNullOrWhiteSpace(model.EntityName))
			{
				errorResult = BulkBadRequest("The request does not name an entity.");
				return false;
			}

			if (model.RecordIds.Any(x => x == Guid.Empty))
			{
				errorResult = BulkBadRequest("The request contains an invalid record id.");
				return false;
			}

			normalizedIds = model.RecordIds.Distinct().ToList();

			if (normalizedIds.Count > BulkActionMaxRecords)
			{
				errorResult = BulkBadRequest("The request exceeds the maximum of " + BulkActionMaxRecords + " records.");
				return false;
			}

			entityMeta = entMan.ReadEntity(model.EntityName)?.Object;
			if (entityMeta == null)
			{
				errorResult = BulkBadRequest("The named entity does not exist.");
				return false;
			}

			return true;
		}

		// Delete a set of records in one request, giving each record its own transaction so one failure
		// rolls back only that record and the batch continues past it.
		// POST: api/v3/en_US/record/bulk/delete
		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/record/bulk/delete")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult BulkDeleteRecords([FromBody] BulkRecordActionModel model)
		{
			// Reject a cross-site request before any record changes. The check runs first so a forged
			// cross-origin post never reaches the delete loop.
			if (!IsBulkRequestOriginTrusted())
				return BulkForbidden("The request origin is not allowed.");

			List<Guid> recordIds;
			// Validate the request and resolve entity metadata inside a guard so an unexpected failure
			// before the per-record loop returns a safe response instead of a framework stack trace.
			try
			{
				if (!TryNormalizeBulkRequest(model, "delete", out recordIds, out _, out var errorResult))
					return errorResult;
			}
			catch (Exception ex)
			{
				return BulkPreflightError("BulkDelete", "delete", ex);
			}

			var results = new List<BulkRecordActionResultItem>();

			foreach (var id in recordIds)
			{
				using (var connection = DbContext.Current.CreateConnection())
				{
					bool transactionStarted = false;
					try
					{
						connection.BeginTransaction();
						transactionStarted = true;
						var r = recMan.DeleteRecord(model.EntityName, id);
						if (r.Success)
						{
							connection.CommitTransaction();
							transactionStarted = false;
							results.Add(BulkOk(id, "Record deleted."));
						}
						else
						{
							SafeRollbackBulk(connection, ref transactionStarted, "BulkDelete", model.EntityName, id);
							LogBulkFailure("BulkDelete", model.EntityName, id, DescribeBulkFailure(r), null);
							results.Add(MapBulkFailure(id, r, "The record could not be deleted."));
						}
					}
					catch (Exception ex)
					{
						SafeRollbackBulk(connection, ref transactionStarted, "BulkDelete", model.EntityName, id);
						LogBulkFailure("BulkDelete", model.EntityName, id, null, ex);
						results.Add(BulkFail(id, "error", "The record could not be deleted."));
					}
				}
			}

			return BuildBulkResponse(results, "deleted");
		}

		// Archive a set of records in one request by setting the approved boolean field to true, giving
		// each record its own transaction so one failure rolls back only that record.
		// POST: api/v3/en_US/record/bulk/archive
		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/record/bulk/archive")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult BulkArchiveRecords([FromBody] BulkRecordActionModel model)
		{
			// Reject a cross-site request before any record changes. The check runs first so a forged
			// cross-origin post never reaches the archive loop.
			if (!IsBulkRequestOriginTrusted())
				return BulkForbidden("The request origin is not allowed.");

			List<Guid> recordIds;
			// The bulk-archive write target is fixed to the approved field; a request cannot redirect it.
			var archiveFieldName = BulkArchiveApprovedField;

			// Validate the request and resolve archive-field metadata inside a guard so an unexpected
			// failure before the per-record loop returns a safe response instead of a framework stack trace.
			try
			{
				if (!TryNormalizeBulkRequest(model, "archive", out recordIds, out var entityMeta, out var errorResult))
					return errorResult;

				// Resolve the archive field from the trusted server-side allowlist. A request that names a
				// field outside the allowlist gets rejected, so a caller cannot redirect the write.
				var requestedField = string.IsNullOrWhiteSpace(model.ArchiveFieldName) ? BulkArchiveApprovedField : model.ArchiveFieldName.Trim();
				if (!BulkArchiveAllowedFields.Contains(requestedField))
					return BulkBadRequest("The requested archive field is not allowed.");

				// Confirm the approved field exists on the entity and is a checkbox (boolean). A missing or
				// wrong-type field fails the whole request, so the server never reports a false archive.
				var archiveField = entityMeta.Fields != null ? entityMeta.Fields.FirstOrDefault(f => f.Name == archiveFieldName) : null;
				if (archiveField == null || archiveField.GetFieldType() != FieldType.CheckboxField)
					return BulkBadRequest("The archive field is missing or is not a checkbox field on this entity.");

				// Enforce field-level update permission. RecordManager checks entity Update permission, but the
				// archive field can carry its own security. When the field turns on security, the caller needs a
				// role in the field update allowlist to write it, so a caller denied the field right cannot archive
				// through the bulk route even with entity Update permission.
				if (archiveField.EnableSecurity && !CurrentUserCanUpdateSecuredField(archiveField.Permissions?.CanUpdate))
					return BulkForbidden("You do not have permission to update the archive field.");
			}
			catch (Exception ex)
			{
				return BulkPreflightError("BulkArchive", "archive", ex);
			}

			var results = new List<BulkRecordActionResultItem>();

			foreach (var id in recordIds)
			{
				using (var connection = DbContext.Current.CreateConnection())
				{
					bool transactionStarted = false;
					try
					{
						connection.BeginTransaction();
						transactionStarted = true;
						var rec = new EntityRecord();
						rec["id"] = id;
						rec[archiveFieldName] = true;
						var r = recMan.UpdateRecord(model.EntityName, rec);
						if (r.Success)
						{
							connection.CommitTransaction();
							transactionStarted = false;
							results.Add(BulkOk(id, "Record archived."));
						}
						else
						{
							SafeRollbackBulk(connection, ref transactionStarted, "BulkArchive", model.EntityName, id);
							LogBulkFailure("BulkArchive", model.EntityName, id, DescribeBulkFailure(r), null);
							results.Add(MapBulkFailure(id, r, "The record could not be archived."));
						}
					}
					catch (Exception ex)
					{
						SafeRollbackBulk(connection, ref transactionStarted, "BulkArchive", model.EntityName, id);
						LogBulkFailure("BulkArchive", model.EntityName, id, null, ex);
						results.Add(BulkFail(id, "error", "The record could not be archived."));
					}
				}
			}

			return BuildBulkResponse(results, "archived");
		}

		// GET: api/v3/en_US/record/{entityName}/list
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/record/{entityName}/list")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetRecordsByEntityName(string entityName, string ids = "", string fields = "", int? limit = null)
		{
			var response = new QueryResponse();
			var recordIdList = new List<Guid>();
			var fieldList = new List<string>();

			ParseRecordIds(ids, response, recordIdList);

			ParseRequestedFields(fields, fieldList);

			var query = BuildRecordsQuery(entityName, recordIdList, fieldList, limit);

			var queryResponse = recMan.Find(query);
			if (!queryResponse.Success)
			{
				response.Message = queryResponse.Message;
				response.Timestamp = DateTime.UtcNow;
				response.Success = false;
				response.Object = null;
				return DoResponse(response);
			}


			response.Message = "Success";
			response.Timestamp = DateTime.UtcNow;
			response.Success = true;
			response.Object.Data = queryResponse.Object.Data;
			return DoResponse(response);
		}

		private QueryResponse CreateErrorResponse(string message)
		{
			var response = new QueryResponse
			{
				Success = false,
				Timestamp = DateTime.UtcNow,
				Message = message,
				Object = null
			};
			return response;
		}

		// Import list records to csv
		// POST: api/v3/en_US/record/{entityName}/list/{listName}/import
		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/record/{entityName}/import")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult ImportEntityRecordsFromCsv(string entityName, [FromBody] JObject postObject)
		{
			string fileTempPath = "";

			if (!postObject.IsNullOrEmpty() && postObject.Properties().Any(p => p.Name == "fileTempPath"))
			{
				fileTempPath = postObject["fileTempPath"].ToString();
			}

			ImportExportManager ieManager = new ImportExportManager();
			ResponseModel response = ieManager.ImportEntityRecordsFromCsv(entityName, fileTempPath);

			return DoResponse(response);

		}


		// Import list records to csv
		// POST: api/v3/en_US/record/{entityName}/list/{listName}/import
		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/record/{entityName}/import-evaluate")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult EvaluateImportEntityRecordsFromCsv(string entityName, [FromBody] JObject postObject)
		{
			ImportExportManager ieManager = new ImportExportManager();
			ResponseModel response = ieManager.EvaluateImportEntityRecordsFromCsv(entityName, postObject, controller: this);

			return DoResponse(response);
		}

		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/quick-search")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetQuickSearch(string query = "", string entityName = "", string lookupFieldsCsv = "", string sortField = "", string sortType = "asc", string returnFieldsCsv = "",
				string matchMethod = "EQ", bool matchAllFields = false, int skipRecords = 0, int limitRecords = 5, string findType = "records", string forceFiltersCsv = "")
		{
			//forceFiltersCsv -> should be in the format "fieldName1:dataType1:eqValue1,fieldName2:dataType2:eqValue2"
			var response = new ResponseModel();
			var responseObject = new EntityRecord();
			try
			{
				if (String.IsNullOrWhiteSpace(entityName) || String.IsNullOrWhiteSpace(lookupFieldsCsv) || String.IsNullOrWhiteSpace(query) || String.IsNullOrWhiteSpace(returnFieldsCsv))
				{
					throw new Exception("missing params. All params are required");
				}

				var lookupFieldsList = new List<string>();
				foreach (var field in lookupFieldsCsv.Split(','))
				{
					lookupFieldsList.Add(field);
				}

				var matchesFilter = BuildQuickSearchMatchFilter(matchMethod, lookupFieldsList, query, matchAllFields);

				BuildQuickSearchForceFilters(forceFiltersCsv, ref matchesFilter);


				var sortsList = BuildQuickSearchSorts(sortField, sortType);

				ExecuteQuickSearchFind(findType, entityName, returnFieldsCsv, matchesFilter, sortsList, skipRecords, limitRecords, responseObject);



				response.Success = true;
				response.Message = "Quick search success";
				response.Object = responseObject;
				return Json(response);
			}
			catch (Exception ex)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:GetQuickSearch", ex);
				response.Success = false;
				response.Message = ex.Message;
				response.Object = null;
				return Json(response);
			}
		}

		#endregion

		#region << Files >>

		[HttpGet]
		[Route("/fs/{fileName}")]
		[Route("/fs/{root}/{fileName}")]
		[Route("/fs/{root}/{root2}/{fileName}")]
		[Route("/fs/{root}/{root2}/{root3}/{fileName}")]
		[Route("/fs/{root}/{root2}/{root3}/{root4}/{fileName}")]
		public IActionResult Download([FromRoute] string root, [FromRoute] string root2, [FromRoute] string root3, [FromRoute] string root4, [FromRoute] string fileName)
		{
			//we added ROOT routing parameter as workaround for conflict with razorpages routing and wildcard controller routing
			//in particular we have problem with ApplicationNodePage where routing pattern is  "/{AppName}/{AreaName}/{NodeName}/a/{PageName?}"

			if (string.IsNullOrWhiteSpace(fileName))
				return DoPageNotFoundResponse();

			var filePath = BuildDownloadFilePath(root, root2, root3, root4, fileName);

			DbFileRepository fsRepository = new DbFileRepository();
			var file = fsRepository.Find(filePath);

			if (file == null)
			{
				return DoPageNotFoundResponse();
			}
			var notModified = CheckDownloadNotModified(file);
			if (notModified != null)
				return notModified;
			var cultureInfo = new CultureInfo("en-US");
			HttpContext.Response.Headers.Add("last-modified", file.LastModificationDate.ToString(cultureInfo));
			const int durationInSeconds = 60 * 60 * 24 * 30; //30 days caching of these resources
			HttpContext.Response.Headers[HeaderNames.CacheControl] = "public,max-age=" + durationInSeconds;

			var extension = Path.GetExtension(filePath).ToLowerInvariant();
			new FileExtensionContentTypeProvider().Mappings.TryGetValue(extension, out string mimeType);


			ParseDownloadRequestOptions(extension);

			return File(file.GetBytes(), mimeType);
		}


		[AcceptVerbs(new[] { "POST" }, Route = "/fs/upload/")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult UploadFile([FromForm] IFormFile file)
		{
			//var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"').ToLowerInvariant();
			//Trim('"') was removed from Core2
			var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.ToString().Trim().ToLowerInvariant();
			if (fileName.StartsWith("\"", StringComparison.InvariantCulture))
				fileName = fileName.Substring(1);

			if (fileName.EndsWith("\"", StringComparison.InvariantCulture))
				fileName = fileName.Substring(0, fileName.Length - 1);

			DbFileRepository fsRepository = new DbFileRepository();
			var createdFile = fsRepository.CreateTempFile(fileName, ReadFully(file.OpenReadStream()));

			return DoResponse(new FSResponse(new FSResult { Url = createdFile.FilePath, Filename = fileName }));

		}

		[AcceptVerbs(new[] { "POST" }, Route = "/fs/move/")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult MoveFile([FromBody] JObject submitObj)
		{
			string source = submitObj["source"].Value<string>();
			string target = submitObj["target"].Value<string>();
			bool overwrite = false;
			if (submitObj["overwrite"] != null)
				overwrite = submitObj["overwrite"].Value<bool>();

			source = source.ToLowerInvariant();
			target = target.ToLowerInvariant();

			var fileName = target.Split(new char[] { '/' }).LastOrDefault();

			DbFileRepository fsRepository = new DbFileRepository();
			var sourceFile = fsRepository.Find(source);

			var movedFile = fsRepository.Move(source, target, overwrite);
			return DoResponse(new FSResponse(new FSResult { Url = movedFile.FilePath, Filename = fileName }));

		}

		[AcceptVerbs(new[] { "DELETE" }, Route = "{*filepath}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult DeleteFile([FromRoute] string filepath)
		{
			filepath = filepath.ToLowerInvariant();

			var fileName = filepath.Split(new char[] { '/' }).LastOrDefault();

			DbFileRepository fsRepository = new DbFileRepository();
			var sourceFile = fsRepository.Find(filepath);

			fsRepository.Delete(filepath);
			return DoResponse(new FSResponse(new FSResult { Url = filepath, Filename = fileName }));
		}

		private static byte[] ReadFully(Stream input)
		{
			byte[] buffer = new byte[16 * 1024];
			using (MemoryStream ms = new MemoryStream())
			{
				int read;
				while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
				{
					ms.Write(buffer, 0, read);
				}
				return ms.ToArray();
			}
		}

		#endregion

		#region << Plugins >>
		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/plugin/list")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetPlugins()
		{
			var responseObj = new ResponseModel
			{
				Object = erpService.Plugins,
				Success = true,
				Timestamp = DateTime.UtcNow
			};
			return DoResponse(responseObj);
		}
		#endregion

		#region << Jobs >>

		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/jobs")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetJobs(DateTime? startFromDate = null, DateTime? startToDate = null, DateTime? finishedFromDate = null,
			DateTime? finishedToDate = null, string typeName = null, int? status = null, int? priority = null, Guid? schedulePlanId = null, int? page = null, int? pageSize = null)
		{
			ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };

			try
			{
				int totalCount;
				response.Object = JobManager.Current.GetJobs(out totalCount, startFromDate, startToDate, finishedFromDate, finishedToDate,
					typeName, status, priority, schedulePlanId, page, pageSize);
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "ErpApi:GetJobs", e);
				response.Success = false;
				response.Message = e.Message + e.StackTrace;
			}

			return DoResponse(response);
		}



		#endregion

		#region << SchedulePlans >>

		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "PUT" }, Route = "api/v3/en_US/scheduleplan/{planId}")]
		public IActionResult UpdateSchedulePlan(Guid planId, [FromBody] JObject postObject)
		{
			ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };

			try
			{
				SchedulePlan schedulePlan = ScheduleManager.Current.GetSchedulePlan(planId);

				if (schedulePlan == null)
				{
					response.Errors.Add(new ErrorModel { Message = $"Schedule plan with such id was not found. Id[{planId}]." });
					response.Success = false;
					return DoResponse(response);
				}

				if (postObject.IsNullOrEmpty())
				{
					response.Errors.Add(new ErrorModel { Message = $"Schedule plan with such id was not found. Id[{planId}]." });
					response.Success = false;
					return DoResponse(response);
				}

				var validationResult = ValidateAndApplySchedulePlan(postObject, schedulePlan, response);
				if (validationResult != null)
					return validationResult;

				schedulePlan.NextTriggerTime = ScheduleManager.Current.FindSchedulePlanNextTriggerDate(schedulePlan);
				ScheduleManager.Current.UpdateSchedulePlan(schedulePlan);
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:UpdateSchedulePlan", e);
				response.Success = false;
				response.Timestamp = DateTime.UtcNow;
				response.Message = e.Message + e.StackTrace;
			}

			response.Success = true;
			response.Timestamp = DateTime.UtcNow;
			var responseRecord = new EntityRecord();
			var responseList = new List<SchedulePlan> {
				ScheduleManager.Current.GetSchedulePlan(planId)
			};
			responseRecord["data"] = responseList;
			response.Object = responseRecord;
			response.Message = "Schedule plan updated successfully";

			return DoResponse(response);
		}

		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/scheduleplan/{planId}/trigger")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult TriggerNowSchedulePlan(Guid planId)
		{
			BaseResponseModel response = new BaseResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };

			try
			{
				var schedulePlan = ScheduleManager.Current.GetSchedulePlan(planId);

				if (schedulePlan == null)
				{
					response.Errors.Add(new ErrorModel { Message = $"Schedule plan with such id was not found. Id[{planId}]." });
					response.Success = false;
					return DoResponse(response);
				}

				ScheduleManager.Current.TriggerNowSchedulePlan(schedulePlan);
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:TriggerNowSchedulePlan", e);
				response.Success = false;
				response.Message = e.Message + e.StackTrace;
			}

			response.Success = true;
			response.Timestamp = DateTime.UtcNow;
			response.Message = "Schedule plan triggered successfully";
			return DoResponse(response);
		}

		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/scheduleplan/list")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetSchedulePlansList()
		{
			ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };

			try
			{
				var responseRecord = new EntityRecord();
				responseRecord["data"] = ScheduleManager.Current.GetSchedulePlans().MapTo<OutputSchedulePlan>();
				response.Object = responseRecord;
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:GetSchedulePlansList", e);
				response.Success = false;
				response.Message = e.Message + e.StackTrace;
			}

			return DoResponse(response);
		}

		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/scheduleplan/{planId}")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetSchedulePlan(Guid planId)
		{
			ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };

			try
			{
				var schedulePlan = ScheduleManager.Current.GetSchedulePlan(planId);

				if (schedulePlan == null)
				{
					response.Errors.Add(new ErrorModel { Message = $"Schedule plan with such id was not found. Id[{planId}]." });
					response.Success = false;
					return DoResponse(response);
				}

				var responseRecord = new EntityRecord();
				responseRecord["data"] = schedulePlan.MapTo<OutputSchedulePlan>();
				response.Object = responseRecord;
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:GetSchedulePlan", e);
				response.Success = false;
				response.Message = e.Message + e.StackTrace;
			}

			return DoResponse(response);
		}

		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/scheduleplan/test")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult CreateTestSchedulePlan(Guid planId)
		{
			ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };

			try
			{
				Guid offerSchedulePlanId = Guid.NewGuid();
				SchedulePlan offerSchedulePlan = ScheduleManager.Current.GetSchedulePlan(offerSchedulePlanId);

				if (offerSchedulePlan == null)
				{
					offerSchedulePlan = new SchedulePlan
					{
						Id = offerSchedulePlanId,
						Name = "Offer schedule plan Test",
						Type = SchedulePlanType.Daily,
						StartDate = DateTime.UtcNow,
						EndDate = null,
						ScheduledDays = new SchedulePlanDaysOfWeek()
						{
							ScheduledOnMonday = true,
							ScheduledOnTuesday = true,
							ScheduledOnWednesday = true,
							ScheduledOnThursday = true,
							ScheduledOnFriday = true,
							ScheduledOnSaturday = true,
							ScheduledOnSunday = true
						},
						//IntervalInMinutes = 1,
						//StartTimespan = 0,
						//EndTimespan = 1440,
						JobTypeId = new Guid("70f06b11-2aee-40d5-b8ef-de1a2d8bbb59"),
						JobAttributes = null,
						Enabled = true,
						LastModifiedBy = null
					};

					ScheduleManager.Current.CreateSchedulePlan(offerSchedulePlan);
				}
				response.Object = offerSchedulePlan.MapTo<OutputSchedulePlan>();
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:CreateTestSchedulePlan", e);
				response.Success = false;
				response.Message = e.Message + e.StackTrace;
			}

			return DoResponse(response);
		}

		#endregion

		#region << System log >>
		[Authorize(Roles = "administrator")]
		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/system-log")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetSystemLog(DateTime? fromDate = null, DateTime? untilDate = null, string type = "",
			string source = "", string message = "", string notificationStatus = "", int page = 1, int pageSize = 15)
		{
			ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };
			var recMan = new RecordManager();
			var skipRecords = (page - 1) * pageSize;
			try
			{
				//Filters
				var filterList = BuildSystemLogFilters(fromDate, untilDate, type, source, message, notificationStatus);

				var selectFilters = EntityQuery.QueryAND(filterList.ToArray());

				//Sort
				var sortList = new List<QuerySortObject> {
					new QuerySortObject("created_on", QuerySortType.Descending)
				};

				//Fields
				var columns = "*";

				//Query
				var query = new EntityQuery("system_log", columns, selectFilters, sortList.ToArray(), skipRecords, pageSize);
				var queryResponse = recMan.Find(query);
				if (!queryResponse.Success)
				{
					throw new Exception("Error getting the records: " + queryResponse.Message);
				}
				response.Object = queryResponse.Object.Data;
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:GetSystemLog", e);
				response.Success = false;
				response.Message = e.Message + e.StackTrace;
			}

			return DoResponse(response);
		}
		#endregion

		#region << UserFile >>

		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/user_file")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult GetUserFileList(string type = "", string search = "", int sort = 1, int page = 1, int pageSize = 30)
		{
			ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };

			try
			{
				response.Object = new UserFileService().GetFilesList(type, search, sort, page, pageSize);
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:GetUserFileList", e);
				response.Success = false;
				response.Message = e.Message + e.StackTrace;
			}

			return DoResponse(response);
		}

		[AcceptVerbs(new[] { "POST" }, Route = "api/v3/en_US/user_file")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult UploadUserFile([FromBody] JObject submitObj)
		{
			ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };
			var filePath = "";
			var fileAlt = "";
			var fileCaption = "";
			#region << Init SubmitObj >>
			foreach (var prop in submitObj.Properties())
			{
				switch (prop.Name.ToLower())
				{
					case "path":
						if (!string.IsNullOrWhiteSpace(prop.Value.ToString()))
							filePath = prop.Value.ToString();
						else
						{
							throw new Exception("File path is required");
						}
						break;
					case "alt":
						if (!string.IsNullOrWhiteSpace(prop.Value.ToString()))
							fileAlt = prop.Value.ToString();
						else
						{
							fileAlt = null;
						}
						break;
					case "caption":
						if (!string.IsNullOrWhiteSpace(prop.Value.ToString()))
							fileCaption = prop.Value.ToString();
						else
						{
							fileCaption = null;
						}
						break;
				}
			}

			#endregion
			try
			{
				response.Object = new UserFileService().CreateUserFile(filePath, fileAlt, fileCaption);
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:UploadUserFile", e);
				response.Success = false;
				response.Message = e.Message + e.StackTrace;
			}

			return DoResponse(response);
		}


		[AcceptVerbs(new[] { "POST" }, Route = "/ckeditor/drop-upload-url")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult UploadDropCKEditor(IFormFile upload)
		{
			var response = new EntityRecord();
			byte[] fileBytes = null;
			try
			{
				if (upload != null)
				{
					using (var ms = new MemoryStream())
					{
						upload.CopyTo(ms);
						fileBytes = ms.ToArray();
					}
					var tempPath = "tmp/" + Guid.NewGuid() + "/" + upload.FileName;
					var tempFile = new DbFileRepository().Create(tempPath, fileBytes, null, null);

					var newFile = new UserFileService().CreateUserFile(tempFile.FilePath, null, null);

					string url = "/fs" + newFile.Path;

					response["uploaded"] = 1;
					response["fileName"] = upload.FileName;
					response["url"] = url;
					return Json(response);

				}
				else
				{
					return Json(response);
				}
			}
			catch (Exception ex)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:UploadDropCKEditor", ex);
				response["uploaded"] = 0;
				response["error"] = new EntityRecord();
				var message = new EntityRecord();
				message["message"] = ex.Message;
				response["error"] = message;
				return Json(response);
			}

		}


		[AcceptVerbs(new[] { "POST" }, Route = "/ckeditor/image-upload-url")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult UploadFileManagerCKEditor(IFormFile upload)
		{
			byte[] fileBytes = null;
			string CKEditorFuncNum = HttpContext.Request.Query["CKEditorFuncNum"].ToString();
			try
			{
				using (var ms = new MemoryStream())
				{
					upload.CopyTo(ms);
					fileBytes = ms.ToArray();
				}
				var tempPath = "tmp/" + Guid.NewGuid() + "/" + upload.FileName;
				var tempFile = new DbFileRepository().Create(tempPath, fileBytes, null, null);

				var newFile = new UserFileService().CreateUserFile(tempFile.FilePath, null, null);

				string url = "/fs" + newFile.Path;
				string vMessage = "";
				var vOutput = @"<html><body><script>window.parent.CKEDITOR.tools.callFunction(" + CKEditorFuncNum + ", \"" + url + "\", \"" + vMessage + "\");</script></body></html>";

				return Content(vOutput, "text/html");
			}
			catch (Exception ex)
			{
				new LogService().Create(Diagnostics.LogType.Error, "TErpApi:UploadFileManagerCKEditor", ex);
				var vOutput = @"<html><body><script>window.parent.CKEDITOR.tools.callFunction(" + CKEditorFuncNum + ", \"\", \"" + ex.Message + "\");</script></body></html>";
				return Content(vOutput, "text/html");
			}
		}

		[AcceptVerbs(new[] { "POST" }, Route = "/fs/upload-user-file-multiple/")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult UploadUserFileMultiple([FromForm] List<IFormFile> files)
		{

			var resultRecords = new List<EntityRecord>();
			var response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };

			using (var connection = DbContext.Current.CreateConnection())
			{
				connection.BeginTransaction();

				try
				{

					var currentUser = AuthService.GetUser(User);

					foreach (var file in files)
					{
						ProcessUserFileUpload(file, currentUser, resultRecords);
					}
					connection.CommitTransaction();
					response.Success = true;
					response.Object = resultRecords;
					return DoResponse(response);
				}
				catch (Exception ex)
				{
					connection.RollbackTransaction();
					response.Success = false;
					response.Message = ex.Message;
					return DoResponse(response);
				}
			}
		}

		[AcceptVerbs(new[] { "POST" }, Route = "/fs/upload-file-multiple/")]
		[ResponseCache(NoStore = true, Duration = 0)]
		public IActionResult UploadFileMultiple([FromForm] List<IFormFile> files)
		{

			var resultRecords = new List<EntityRecord>();
			var response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };

			using (var connection = DbContext.Current.CreateConnection())
			{
				connection.BeginTransaction();

				try
				{
					foreach (var file in files)
					{
						ProcessFileUpload(file, resultRecords);
					}

					connection.CommitTransaction();
					response.Success = true;
					response.Object = resultRecords;
					return DoResponse(response);
				}
				catch (Exception ex)
				{
					connection.RollbackTransaction();
					response.Success = false;
					response.Message = ex.Message;
					return DoResponse(response);
				}
			}
		}


		#endregion

		#region << Utils >>

		public static Stream GenerateStreamFromString(string s)
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter(stream);
			writer.Write(s);
			writer.Flush();
			stream.Position = 0;
			return stream;
		}
		#endregion

		#region <== Snippets ===>

		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/snippets")]
		public IActionResult GetSnippetNames(string search = "", int page = 1, int pageSize = 30)
		{
			var response = new TypeaheadResponse();
			var snippets = SnippetService.Snippets.Keys.OrderBy(x => x).ToList();
			if (string.IsNullOrWhiteSpace(search))
				return new JsonResult(snippets.Skip(page - 1).Take(pageSize).ToList());
			else
				return new JsonResult(snippets.Where(x => x.ToLowerInvariant().Contains(search.ToLowerInvariant())).Skip(page - 1).Take(pageSize).ToList());
		}

		[AcceptVerbs(new[] { "GET" }, Route = "api/v3/en_US/snippet")]
		public IActionResult GetSnippetText([FromQuery] string name)
		{
			ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };

			try
			{
				var snippet = SnippetService.GetSnippet(name);
				if (snippet == null)
					throw new Exception($"Snippet '{name}' is not found.");
				else
					response.Object = snippet.GetText();
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "GetSnippetNames", e);
				response.Success = false;
				response.Message = e.Message + e.StackTrace;
			}

			return DoResponse(response);
		}

		#endregion

		#region <=== JWT Token Auth ===>


		[AllowAnonymous]
		[Route("api/v3/en_US/auth/jwt/token")]
		[HttpPost]
		public async Task<IActionResult> GetJwtToken([FromBody] JwtTokenLoginModel model)
		{
			ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };
			try
			{
				response.Object = await AuthService.GetTokenAsync(model.Email, model.Password);
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "GetJwtToken", e);
				response.Success = false;
				response.Message = e.Message + e.StackTrace;
			}
			return DoResponse(response);
		}

		[AllowAnonymous]
		[Route("api/v3/en_US/auth/jwt/token/refresh")]
		[HttpPost]
		public async Task<IActionResult> GetNewJwtToken([FromBody] JwtTokenModel model)
		{
			ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };
			try
			{
				response.Object = await AuthService.GetNewTokenAsync(model.Token);
			}
			catch (Exception e)
			{
				new LogService().Create(Diagnostics.LogType.Error, "GetNewJwtToken", e);
				response.Success = false;
				response.Message = e.Message + e.StackTrace;
			}
			return DoResponse(response);
		}

		#endregion

		#region << Refactor: extracted private helpers >>

		private EqlDataSourceQuery BuildEqlDataSourceQueryFromSubmit(JObject submitObj)
		{
			EqlDataSourceQuery model = new EqlDataSourceQuery();
			foreach (var prop in submitObj.Properties())
			{
				switch (prop.Name.ToLower())
				{
					case "name":
						if (!string.IsNullOrWhiteSpace(prop.Value.ToString()))
							model.Name = prop.Value.ToString();
						else
						{
							throw new Exception("DataSource Name is required");
						}
						break;
					case "parameters":
						var jParams = (JArray)prop.Value;
						model.Parameters = new List<EqlParameter>();
						foreach (JObject jParam in jParams)
						{
							var name = jParam["name"].ToString();
							var value = jParam["value"].ToString();
							var eqlParam = new EqlParameter(name, value);
							model.Parameters.Add(eqlParam);
						}
						break;
				}
			}
			return model;
		}

		private ActionResult JsonFromEqlException(ResponseModel response, EqlException eqlEx)
		{
			response.Success = false;
			foreach (var eqlError in eqlEx.Errors)
			{
				response.Errors.Add(new ErrorModel("eql", "", eqlError.Message));
			}
			return Json(response);
		}

		private ActionResult JsonFromException(ResponseModel response, Exception ex)
		{
			response.Success = false;
			response.Message = ex.Message;
			return Json(response);
		}

		private ContentResult LogErrorAndReturn500(Exception exception, string label)
		{
			new Log().Create(LogType.Error, label, exception);
			return new ContentResult
			{
				Content = $"Error: {exception.Message}",
				ContentType = "text/plain",
				// change to whatever status code you want to send out
				StatusCode = 500
			};
		}

		private ActionResult ValidateRenderRequest(string renderMode, Guid? pid)
		{
			if (string.IsNullOrWhiteSpace(renderMode))
				return NotFound();

			//if (nid == null)
			//	return BadRequest("The node Id is required to be set as query parameter 'nid', when requesting this component");

			if (pid == null)
				return BadRequest("The page Id is required to be set as query parameter 'pid', when requesting this component");

			return null;
		}

		private Type ResolvePageComponentType(string fullComponentName)
		{
			return FileService.GetType(fullComponentName);
		}

		private void ApplySimulatedRouteData(Guid? entityId, Guid? pid, Guid? recordId)
		{
			erpRequestContext.SetSimulatedRouteData(entityId: entityId, pageId: pid, recordId: recordId);
		}

		private PageDataModel BuildSimulationPageModel(ErpPage page, Guid? entityId, Guid? recordId)
		{
			//erpRequestContext
			if (page != null)
			{
				//Override 
				if (entityId != null)
					page.EntityId = entityId;

				if (page.AppId == null && page.EntityId != null)
				{
					ResolveSimulatedAppFromAttachedApps(page);
				}

				if (page.AppId != null)
				{
					ApplySimulatedAppAreaNodeEntity(page, recordId);
				}
			}

			//currentUser
			var currentUser = AuthService.GetUser(User);


			var baseErpPageMode = BaseErpPageModel.CreatePageModelSimulation(
				erpRequestContext: erpRequestContext,
				currentUser: currentUser
			);

			return baseErpPageMode.DataModel;
		}

		private ActionResult DispatchComponentView(Type type, string renderMode, PageBodyNode pagebodyNode, PageDataModel pageModel, JObject options)
		{
			switch (renderMode)
			{
				case "display":
					var pcContextDisplay = new PageComponentContext(pagebodyNode, pageModel, ComponentMode.Design, options);
					return ViewComponent(type, new { context = pcContextDisplay });
				case "design":
					var pcContextDesign = new PageComponentContext(pagebodyNode, pageModel, ComponentMode.Design, options);
					return ViewComponent(type, new { context = pcContextDesign });
				case "options":
					pageModel.SafeCodeDataVariable = true;
					var pcContextOptions = new PageComponentContext(pagebodyNode, pageModel, ComponentMode.Options, options);
					return ViewComponent(type, new { context = pcContextOptions });
				case "help":
					var pcContextReadme = new PageComponentContext(pagebodyNode, pageModel, ComponentMode.Help, options);
					return ViewComponent(type, new { context = pcContextReadme });
			}

			return NotFound();
		}

		private List<Guid> ResolveNodeIdsFromComponentData(EntityRecord componentData, string key)
		{
			var nodeIds = new List<Guid>();
			if (componentData.Properties.ContainsKey(key) && componentData[key] != null)
			{
				if (componentData[key] is string)
				{
					try
					{
						nodeIds = JsonConvert.DeserializeObject<List<Guid>>((string)componentData[key]);
					}
					catch
					{
						throw new Exception($"WebVella.Erp.Web.Components.PcSection component data object in user preferences not in the correct format. {key} should be List<Guid>");
					}
				}
				else if (componentData[key] is List<Guid>)
				{
					nodeIds = (List<Guid>)componentData[key];
				}
				else if (componentData[key] is JArray)
				{
					nodeIds = ((JArray)componentData[key]).ToObject<List<Guid>>();
				}
				else
				{
					throw new Exception($"Unknown format of {key}");
				}
			}
			return nodeIds;
		}

		private List<EntityRecord> MapRecordsToSelect2Items(List<EntityRecord> records)
		{
			var processedRecords = new List<EntityRecord>();
			foreach (var record in records)
			{
				var procRec = new EntityRecord();
				if (record.Properties.ContainsKey("id"))
				{
					procRec["id"] = record["id"].ToString();
				}
				else
				{
					procRec["id"] = "no-id-" + Guid.NewGuid();
				}
				if (record.Properties.ContainsKey("text"))
				{
					procRec["text"] = record["text"].ToString();
				}
				else if (record.Properties.ContainsKey("label"))
				{
					procRec["text"] = record["label"].ToString();
				}
				else if (record.Properties.ContainsKey("name"))
				{
					procRec["text"] = record["name"].ToString();
				}
				else
				{
					procRec["text"] = procRec["id"].ToString();
				}
				processedRecords.Add(procRec);
			}
			return processedRecords;
		}
		private int ResolveSelect2Page(EqlDataSourceQuery model)
		{
			var page = 1;
			if (model.Parameters.Count > 0)
			{
				var pageParam = model.Parameters.FirstOrDefault(x => x.ParameterName == "page");
				if (pageParam != null)
				{
					if (int.TryParse(pageParam.Value?.ToString(), out int outInt))
					{
						page = outInt;
					}
				}
			}
			return page;
		}
		private ActionResult ExecuteSelect2DataSource(EqlDataSourceQuery model, ref List<EntityRecord> records, ref int? total)
		{
			DataSourceManager dsMan = new DataSourceManager();
			var dataSources = dsMan.GetAll();
			var ds = dataSources.SingleOrDefault(x => x.Name == model.Name);
			if (ds == null)
			{
				return BadRequest();
			}

			if (ds is DatabaseDataSource)
			{
				var list = (EntityRecordList)dsMan.Execute(ds.Id, model.Parameters);
				records = (List<EntityRecord>)list;
				total = list.TotalCount;
			}
			else if (ds is CodeDataSource)
			{
				Dictionary<string, object> arguments = new Dictionary<string, object>();
				foreach (var par in model.Parameters)
					arguments[par.ParameterName] = par.Value;

				var dsResult = ((CodeDataSource)ds).Execute(arguments);
				if (dsResult is EntityRecordList)
				{

					records = (List<EntityRecord>)((EntityRecordList)dsResult);
					total = ((EntityRecordList)dsResult).TotalCount;
				}
				else if (dsResult is List<EntityRecord>)
				{
					records = (List<EntityRecord>)dsResult;
					total = null;
				}
				else
				{
					return Json(dsResult);
				}
			}
			else
			{
				return BadRequest();
			}
			return null;
		}
		private void ApplyToggleSectionState(bool isCollapsed, Guid? nodeId, ref List<Guid> collapsedNodeIds, ref List<Guid> uncollapsedNodeIds)
		{
			if (isCollapsed)
			{
				//new state is collapsed
				//1. remove if it is in uncollapsed
				uncollapsedNodeIds = uncollapsedNodeIds.FindAll(x => x != nodeId.Value).ToList();
				//2. add to collapsed
				if (!collapsedNodeIds.Contains(nodeId.Value))
					collapsedNodeIds.Add(nodeId.Value);
			}
			else
			{
				//new state is uncollapsed
				//1. remove it is in collapsed
				collapsedNodeIds = collapsedNodeIds.FindAll(x => x != nodeId.Value).ToList();
				//2. add to uncollapsed
				if (!uncollapsedNodeIds.Contains(nodeId.Value))
					uncollapsedNodeIds.Add(nodeId.Value);
			}
		}
		private void ResolveSimulatedAppFromAttachedApps(ErpPage page)
		{
			#region << Try to get one of the attached apps >>
			var allApps = new AppService().GetAllApplications();
			foreach (var appInstance in allApps)
			{
				foreach (var areaInstance in appInstance.Sitemap.Areas)
				{
					foreach (var nodeInstance in areaInstance.Nodes)
					{
						if (nodeInstance.EntityId == page.EntityId)
						{
							page.AppId = appInstance.Id;
							if (page.Type == PageType.RecordCreate || page.Type == PageType.RecordDetails ||
							page.Type == PageType.RecordList || page.Type == PageType.RecordManage)
							{
								page.AreaId = areaInstance.Id;
								page.NodeId = nodeInstance.Id;
							}
						}
					}
				}
			}

			#endregion
		}
		private void ApplySimulatedAppAreaNodeEntity(ErpPage page, Guid? recordId)
		{
			App app = null;
			SitemapArea area = null;
			SitemapNode node = null;
			Entity entity = null;
			app = new AppService().GetApplication(page.AppId ?? Guid.Empty);
			erpRequestContext.App = app;
			if (app != null)
			{
				if (page.AreaId != null)
				{
					area = app.Sitemap.Areas.FirstOrDefault(x => x.Id == page.AreaId);
					erpRequestContext.SitemapArea = area;
					if (area != null && page.NodeId != null)
					{
						node = area.Nodes.FirstOrDefault(x => x.Id == page.NodeId);
						erpRequestContext.SitemapNode = node;
					}
				}

				if (page.EntityId != null)
				{
					entity = new EntityManager().ReadEntity(page.EntityId ?? Guid.Empty).Object;
					erpRequestContext.Entity = entity;

					FindSimulationRecord(entity, recordId);
				}
			}
		}
		private void FindSimulationRecord(Entity entity, Guid? recordId)
		{
			//Get the first record as simulation
			if (entity != null)
			{
				QueryObject filter = null;
				if (recordId != null)
				{
					filter = EntityQuery.QueryEQ("id", recordId.Value);
				}
				var sortsList = new List<QuerySortObject>();
				sortsList.Add(new QuerySortObject("id", QuerySortType.Ascending));
				var findRecordResponse = new RecordManager().Find(new EntityQuery(entity.Name, "*", filter, sortsList.ToArray(), 0, 1));
				if (!findRecordResponse.Success)
					throw new Exception(findRecordResponse.Message);
				if (findRecordResponse.Object != null && findRecordResponse.Object.Data.Any())
				{
					var record = findRecordResponse.Object.Data.First();
					erpRequestContext.RecordId = (Guid)record["id"];
				}
			}
		}
		private EntityQuery BuildRelatedFieldQuery(string entityName, string fieldName, string search, int page)
		{
			var pageSize = 5 + 1; //the extra record will tell us if there are more records
			var skipPages = (page - 1) * pageSize;
			var sortList = new List<QuerySortObject>();
			sortList.Add(new QuerySortObject(fieldName, QuerySortType.Ascending));

			var query = new EntityQuery(entityName, fieldName, null, sortList.ToArray(), skipPages, pageSize);
			if (!String.IsNullOrWhiteSpace(search))
			{
				query = new EntityQuery(entityName, fieldName, EntityQuery.QueryContains(fieldName, search), sortList.ToArray(), skipPages, pageSize);
			}
			return query;
		}
		private void PopulateRelatedFieldResults(TypeaheadResponse response, List<EntityRecord> data, string entityName, string fieldName)
		{
			var resultRecords = new List<EntityRecord>();
			if (data.Count > 0)
			{
				if (data.Count == 6)
				{
					response.Pagination.More = true;
					resultRecords = data.Take(5).ToList();
				}
				else
				{
					resultRecords = data;
				}

				var entity = new EntityManager().ReadEntity(entityName).Object;
				foreach (var record in resultRecords)
				{
					response.Results.Add(new TypeaheadResponseRow
					{
						Id = record[fieldName].ToString(),
						Text = record[fieldName].ToString(),
						FieldName = fieldName,
						EntityName = entity.Label,
						Color = entity.Color,
						IconName = entity.IconName
					});
				}
			}
		}
		private void ParseSelectFieldAddOptionSubmit(JObject submitObj, ref string entityName, ref string fieldName, ref string optionValue)
		{
			#region << Init SubmitObj >>
			foreach (var prop in submitObj.Properties())
			{
				switch (prop.Name.ToLower())
				{
					case "entityname":
						if (!string.IsNullOrWhiteSpace(prop.Value.ToString()))
							entityName = prop.Value.ToString();
						else
						{
							throw new Exception("EntityName is required");
						}
						break;
					case "fieldname":
						if (!string.IsNullOrWhiteSpace(prop.Value.ToString()))
							fieldName = prop.Value.ToString();
						else
						{
							throw new Exception("Field name is required");
						}
						break;
					case "value":
						if (!string.IsNullOrWhiteSpace(prop.Value.ToString()))
							optionValue = prop.Value.ToString();
						else
						{
							throw new Exception("Option value is required");
						}
						break;
				}
			}
			#endregion
		}
		private Field ResolveSelectFieldMeta(EntityManager entMan, string entityName, string fieldName, out Entity entityMeta)
		{
			entityMeta = entMan.ReadEntity(entityName).Object;
			if (entityMeta == null)
			{
				throw new Exception("Entity not found by the provided entityName: " + entityName);
			}
			var fieldMeta = entityMeta.Fields.FirstOrDefault(x => x.Name == fieldName);
			if (fieldMeta == null)
			{
				throw new Exception("Field not found by the provided fieldName: " + fieldMeta + " in entity " + entityName);
			}
			return fieldMeta;
		}
		private bool SelectFieldOptionExists(Field fieldMeta, string optionValue)
		{
			var optionExists = false;
			if (fieldMeta.GetFieldType() == FieldType.SelectField)
			{
				var fieldOptions = ((SelectField)fieldMeta).Options.FirstOrDefault(x => x.Value.ToLowerInvariant() == optionValue.ToLowerInvariant());
				if (fieldOptions != null)
				{
					optionExists = true;
				}
			}
			else if (fieldMeta.GetFieldType() == FieldType.MultiSelectField)
			{
				var fieldOptions = ((MultiSelectField)fieldMeta).Options.FirstOrDefault(x => x.Value.ToLowerInvariant() == optionValue.ToLowerInvariant());
				if (fieldOptions != null)
				{
					optionExists = true;
				}
			}
			return optionExists;
		}
		private void AddSelectFieldOption(EntityManager entMan, Entity entityMeta, Field fieldMeta, string optionValue)
		{
			if (fieldMeta.GetFieldType() == FieldType.SelectField)
			{
				var newOption = new SelectOption
				{
					Value = optionValue,
					Label = optionValue
				};
				var newFieldMeta = (SelectField)fieldMeta;
				newFieldMeta.Options.Add(newOption);
				var updateResponse = entMan.UpdateField(entityMeta, newFieldMeta.MapTo<InputField>());
				if (!updateResponse.Success)
				{
					throw new Exception(updateResponse.Message);
				}
			}
			else if (fieldMeta.GetFieldType() == FieldType.MultiSelectField)
			{
				var newOption = new SelectOption
				{
					Value = optionValue,
					Label = optionValue
				};
				var newFieldMeta = (MultiSelectField)fieldMeta;
				newFieldMeta.Options.Add(newOption);
				var updateResponse = entMan.UpdateField(entityMeta, newFieldMeta.MapTo<InputField>());
				if (!updateResponse.Success)
				{
					throw new Exception(updateResponse.Message);
				}
			}
		}
		private void ParseFieldTableDataPreviewSubmit(JObject submitObj, ref bool hasHeader, ref bool hasHeaderColumn, ref string csvData, ref string delimiterName)
		{
			foreach (var prop in submitObj.Properties())
			{
				switch (prop.Name.ToLower())
				{
					case "hasheader":
						if (!string.IsNullOrWhiteSpace(prop.Value.ToString()))
						{
							var hasHeaderString = prop.Value.ToString();
							if (hasHeaderString.ToLowerInvariant() == "false")
							{
								hasHeader = false;
							}
						}
						break;
					case "hasheadercolumn":
						if (!string.IsNullOrWhiteSpace(prop.Value.ToString()))
						{
							var hasHeaderColumnString = prop.Value.ToString();
							if (hasHeaderColumnString.ToLowerInvariant() == "true")
							{
								hasHeaderColumn = true;
							}
						}
						break;
					case "csv":
						if (!string.IsNullOrWhiteSpace(prop.Value.ToString()))
						{
							csvData = prop.Value.ToString();
						}
						break;
					case "delimiter":
						if (!string.IsNullOrWhiteSpace(prop.Value.ToString()))
						{
							delimiterName = prop.Value.ToString(); //Does not work if first checked for empty string
						}
						break;
				}
			}
		}
		private void ValidatePatchEntityProperties(JObject submitObj, Type inputEntityType, FieldResponse response)
		{
			foreach (var prop in submitObj.Properties())
			{
				int count = inputEntityType.GetProperties().Where(n => n.Name.ToLower() == prop.Name.ToLower()).Count();
				if (count < 1)
					response.Errors.Add(new ErrorModel(prop.Name, prop.Value.ToString(), "Input object contains property that is not part of the object model."));
			}
		}
		private void ApplyPatchEntityProperties(InputEntity entity, InputEntity inputEntity, JObject submitObj)
		{
			foreach (var prop in submitObj.Properties())
			{
				if (prop.Name.ToLower() == "label")
					entity.Label = inputEntity.Label;
				if (prop.Name.ToLower() == "labelplural")
					entity.LabelPlural = inputEntity.LabelPlural;
				if (prop.Name.ToLower() == "system")
					entity.System = inputEntity.System;
				if (prop.Name.ToLower() == "iconname")
					entity.IconName = inputEntity.IconName;
				if (prop.Name.ToLower() == "color")
					entity.Color = inputEntity.Color;
				//if (prop.Name.ToLower() == "weight")
				//	entity.Weight = inputEntity.Weight;
				if (prop.Name.ToLower() == "recordpermissions")
					entity.RecordPermissions = inputEntity.RecordPermissions;
				if (prop.Name.ToLower() == "recordscreenidfield")
					entity.RecordScreenIdField = inputEntity.RecordScreenIdField;
			}
		}
		private IActionResult ResolvePatchFieldEntity(string Id, string FieldId, FieldResponse response, ref Entity entity)
		{
			if (!Guid.TryParse(Id, out Guid entityId))
			{
				response.Errors.Add(new ErrorModel("Id", Id, "id parameter is not valid Guid value"));
				return DoBadRequestResponse(response, "Field was not updated!");
			}

			if (!Guid.TryParse(FieldId, out Guid fieldId))
			{
				response.Errors.Add(new ErrorModel("FieldId", FieldId, "FieldId parameter is not valid Guid value"));
				return DoBadRequestResponse(response, "Field was not updated!");
			}

			DbEntity storageEntity = DbContext.Current.EntityRepository.Read(entityId);
			if (storageEntity == null)
			{
				response.Errors.Add(new ErrorModel("Id", Id, "Entity with such Id does not exist!"));
				return DoBadRequestResponse(response, "Field was not updated!");
			}
			entity = storageEntity.MapTo<Entity>();

			Field updatedField = entity.Fields.FirstOrDefault(f => f.Id == fieldId);
			if (updatedField == null)
			{
				response.Errors.Add(new ErrorModel("FieldId", FieldId, "Field with such Id does not exist!"));
				return DoBadRequestResponse(response, "Field was not updated!");
			}
			return null;
		}
		private IActionResult ResolvePatchFieldType(JObject submitObj, FieldResponse response, out FieldType fieldType)
		{
			fieldType = FieldType.GuidField;

			var fieldTypeProp = submitObj.Properties().SingleOrDefault(k => k.Name.ToLower() == "fieldtype");
			if (fieldTypeProp != null)
			{
				fieldType = (FieldType)Enum.ToObject(typeof(FieldType), fieldTypeProp.Value.ToObject<int>());
			}
			else
			{
				response.Errors.Add(new ErrorModel("fieldType", null, "fieldType is required!"));
				return DoBadRequestResponse(response, "Field was not updated!");
			}
			return null;
		}
		private void ValidatePatchFieldProperties(JObject submitObj, FieldType fieldType, FieldResponse response)
		{
			Type inputFieldType = InputField.GetFieldType(fieldType);
			foreach (var prop in submitObj.Properties())
			{
				if (prop.Name.ToLower() == "entityname")
					continue;

				int count = inputFieldType.GetProperties().Where(n => n.Name.ToLower() == prop.Name.ToLower()).Count();
				if (count < 1)
					response.Errors.Add(new ErrorModel(prop.Name, prop.Value.ToString(), "Input object contains property that is not part of the object model."));
			}
		}
		private InputField ApplyPatchFieldProperties(JObject submitObj, FieldType fieldType, InputField inputField)
		{
			InputField field = new InputGuidField();
			foreach (var prop in submitObj.Properties())
			{
				field = ResolveTypedFieldProperty(fieldType, prop, field, inputField);
				ApplyCommonFieldProperty(prop, field, inputField);
			}
			return field;
		}
		private InputField ResolveTypedFieldProperty(FieldType fieldType, JProperty prop, InputField field, InputField inputField)
		{
			switch (fieldType)
			{
				case FieldType.AutoNumberField: field = ApplyAutoNumberFieldProperty(prop, inputField); break;
				case FieldType.CheckboxField: field = ApplyCheckboxFieldProperty(prop, inputField); break;
				case FieldType.CurrencyField: field = ApplyCurrencyFieldProperty(prop, inputField); break;
				case FieldType.DateField: field = ApplyDateFieldProperty(prop, inputField); break;
				case FieldType.DateTimeField: field = ApplyDateTimeFieldProperty(prop, inputField); break;
				case FieldType.EmailField: field = ApplyEmailFieldProperty(prop, inputField); break;
				case FieldType.FileField: field = ApplyFileFieldProperty(prop, inputField); break;
				case FieldType.HtmlField: field = ApplyHtmlFieldProperty(prop, inputField); break;
				case FieldType.ImageField: field = ApplyImageFieldProperty(prop, inputField); break;
				case FieldType.MultiLineTextField: field = ApplyMultiLineTextFieldProperty(prop, inputField); break;
				case FieldType.GeographyField: field = ApplyGeographyFieldProperty(prop, inputField); break;
				case FieldType.MultiSelectField: field = ApplyMultiSelectFieldProperty(prop, inputField); break;
				case FieldType.NumberField: field = ApplyNumberFieldProperty(prop, inputField); break;
				case FieldType.PasswordField: field = ApplyPasswordFieldProperty(prop, inputField); break;
				case FieldType.PercentField: field = ApplyPercentFieldProperty(prop, inputField); break;
				case FieldType.PhoneField: field = ApplyPhoneFieldProperty(prop, inputField); break;
				case FieldType.GuidField: field = ApplyGuidFieldProperty(prop, inputField); break;
				case FieldType.SelectField: field = ApplySelectFieldProperty(prop, inputField); break;
				case FieldType.TextField: field = ApplyTextFieldProperty(prop, inputField); break;
				case FieldType.UrlField: field = ApplyUrlFieldProperty(prop, inputField); break;
			}
			return field;
		}
		private InputField ApplyAutoNumberFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputAutoNumberField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputAutoNumberField)field).DefaultValue = ((InputAutoNumberField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "U")
				((InputAutoNumberField)field).DisplayFormat = ((InputAutoNumberField)inputField).DisplayFormat;
			if (prop.Name.ToLower() == "startingnumber")
				((InputAutoNumberField)field).StartingNumber = ((InputAutoNumberField)inputField).StartingNumber;
			return field;
		}
		private InputField ApplyCheckboxFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputCheckboxField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputCheckboxField)field).DefaultValue = ((InputCheckboxField)inputField).DefaultValue;
			return field;
		}
		private InputField ApplyCurrencyFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputCurrencyField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputCurrencyField)field).DefaultValue = ((InputCurrencyField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "minvalue")
				((InputCurrencyField)field).MinValue = ((InputCurrencyField)inputField).MinValue;
			if (prop.Name.ToLower() == "maxvalue")
				((InputCurrencyField)field).MaxValue = ((InputCurrencyField)inputField).MaxValue;
			if (prop.Name.ToLower() == "currency")
				((InputCurrencyField)field).Currency = ((InputCurrencyField)inputField).Currency;
			return field;
		}
		private InputField ApplyDateFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputDateField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputDateField)field).DefaultValue = ((InputDateField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "format")
				((InputDateField)field).Format = ((InputDateField)inputField).Format;
			if (prop.Name.ToLower() == "usecurrenttimeasdefaultvalue")
				((InputDateField)field).UseCurrentTimeAsDefaultValue = ((InputDateField)inputField).UseCurrentTimeAsDefaultValue;
			return field;
		}
		private InputField ApplyDateTimeFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputDateTimeField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputDateTimeField)field).DefaultValue = ((InputDateTimeField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "format")
				((InputDateTimeField)field).Format = ((InputDateTimeField)inputField).Format;
			if (prop.Name.ToLower() == "usecurrenttimeasdefaultvalue")
				((InputDateTimeField)field).UseCurrentTimeAsDefaultValue = ((InputDateTimeField)inputField).UseCurrentTimeAsDefaultValue;
			return field;
		}
		private InputField ApplyEmailFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputEmailField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputEmailField)field).DefaultValue = ((InputEmailField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "maxlength")
				((InputEmailField)field).MaxLength = ((InputEmailField)inputField).MaxLength;
			return field;
		}
		private InputField ApplyFileFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputFileField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputFileField)field).DefaultValue = ((InputFileField)inputField).DefaultValue;
			return field;
		}
		private InputField ApplyHtmlFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputHtmlField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputHtmlField)field).DefaultValue = ((InputHtmlField)inputField).DefaultValue;
			return field;
		}
		private InputField ApplyImageFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputImageField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputImageField)field).DefaultValue = ((InputImageField)inputField).DefaultValue;
			return field;
		}
		private InputField ApplyMultiLineTextFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputMultiLineTextField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputMultiLineTextField)field).DefaultValue = ((InputMultiLineTextField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "maxlength")
				((InputMultiLineTextField)field).MaxLength = ((InputMultiLineTextField)inputField).MaxLength;
			if (prop.Name.ToLower() == "visiblelinenumber")
				((InputMultiLineTextField)field).VisibleLineNumber = ((InputMultiLineTextField)inputField).VisibleLineNumber;
			return field;
		}
		private InputField ApplyGeographyFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputGeographyField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputGeographyField)field).DefaultValue = ((InputGeographyField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "maxlength")
				((InputGeographyField)field).MaxLength = ((InputGeographyField)inputField).MaxLength;
			if (prop.Name.ToLower() == "visiblelinenumber")
				((InputGeographyField)field).VisibleLineNumber = ((InputGeographyField)inputField).VisibleLineNumber;
			if (prop.Name.ToLower() == "format")
				((InputGeographyField)field).Format = ((InputGeographyField)inputField).Format;
			if (prop.Name.ToLower() == "srid")
				((InputGeographyField)field).SRID = ((InputGeographyField)inputField).SRID;
			return field;
		}
		private InputField ApplyMultiSelectFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputMultiSelectField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputMultiSelectField)field).DefaultValue = ((InputMultiSelectField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "options")
				((InputMultiSelectField)field).Options = ((InputMultiSelectField)inputField).Options;
			return field;
		}
		private InputField ApplyNumberFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputNumberField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputNumberField)field).DefaultValue = ((InputNumberField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "minvalue")
				((InputNumberField)field).MinValue = ((InputNumberField)inputField).MinValue;
			if (prop.Name.ToLower() == "maxvalue")
				((InputNumberField)field).MaxValue = ((InputNumberField)inputField).MaxValue;
			if (prop.Name.ToLower() == "decimalplaces")
				((InputNumberField)field).DecimalPlaces = ((InputNumberField)inputField).DecimalPlaces;
			return field;
		}
		private InputField ApplyPasswordFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputPasswordField();
			if (prop.Name.ToLower() == "maxlength")
				((InputPasswordField)field).MaxLength = ((InputPasswordField)inputField).MaxLength;
			if (prop.Name.ToLower() == "minlength")
				((InputPasswordField)field).MinLength = ((InputPasswordField)inputField).MinLength;
			if (prop.Name.ToLower() == "encrypted")
				((InputPasswordField)field).Encrypted = ((InputPasswordField)inputField).Encrypted;
			return field;
		}
		private InputField ApplyPercentFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputPercentField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputPercentField)field).DefaultValue = ((InputPercentField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "minvalue")
				((InputPercentField)field).MinValue = ((InputPercentField)inputField).MinValue;
			if (prop.Name.ToLower() == "maxvalue")
				((InputPercentField)field).MaxValue = ((InputPercentField)inputField).MaxValue;
			if (prop.Name.ToLower() == "decimalplaces")
				((InputPercentField)field).DecimalPlaces = ((InputPercentField)inputField).DecimalPlaces;
			return field;
		}
		private InputField ApplyPhoneFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputPhoneField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputPhoneField)field).DefaultValue = ((InputPhoneField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "format")
				((InputPhoneField)field).Format = ((InputPhoneField)inputField).Format;
			if (prop.Name.ToLower() == "maxlength")
				((InputPhoneField)field).MaxLength = ((InputPhoneField)inputField).MaxLength;
			return field;
		}
		private InputField ApplyGuidFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputGuidField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputGuidField)field).DefaultValue = ((InputGuidField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "generatenewid")
				((InputGuidField)field).GenerateNewId = ((InputGuidField)inputField).GenerateNewId;
			return field;
		}
		private InputField ApplySelectFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputSelectField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputSelectField)field).DefaultValue = ((InputSelectField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "options")
				((InputSelectField)field).Options = ((InputSelectField)inputField).Options;
			return field;
		}
		private InputField ApplyTextFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputTextField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputTextField)field).DefaultValue = ((InputTextField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "maxlength")
				((InputTextField)field).MaxLength = ((InputTextField)inputField).MaxLength;
			return field;
		}
		private InputField ApplyUrlFieldProperty(JProperty prop, InputField inputField)
		{
			InputField field = new InputUrlField();
			if (prop.Name.ToLower() == "defaultvalue")
				((InputUrlField)field).DefaultValue = ((InputUrlField)inputField).DefaultValue;
			if (prop.Name.ToLower() == "maxlength")
				((InputUrlField)field).MaxLength = ((InputUrlField)inputField).MaxLength;
			if (prop.Name.ToLower() == "opentargetinnewwindow")
				((InputUrlField)field).OpenTargetInNewWindow = ((InputUrlField)inputField).OpenTargetInNewWindow;
			return field;
		}
		private void ApplyCommonFieldProperty(JProperty prop, InputField field, InputField inputField)
		{
			if (prop.Name.ToLower() == "label")
				field.Label = inputField.Label;
			else if (prop.Name.ToLower() == "placeholdertext")
				field.PlaceholderText = inputField.PlaceholderText;
			else if (prop.Name.ToLower() == "description")
				field.Description = inputField.Description;
			else if (prop.Name.ToLower() == "helptext")
				field.HelpText = inputField.HelpText;
			else if (prop.Name.ToLower() == "required")
				field.Required = inputField.Required;
			else if (prop.Name.ToLower() == "unique")
				field.Unique = inputField.Unique;
			else if (prop.Name.ToLower() == "searchable")
				field.Searchable = inputField.Searchable;
			else if (prop.Name.ToLower() == "auditable")
				field.Auditable = inputField.Auditable;
			else if (prop.Name.ToLower() == "system")
				field.System = inputField.System;
		}
		private IActionResult ValidateUpdateRelationModel(InputEntityRelationRecordUpdateModel model, BaseResponseModel response, ref EntityRelation relation)
		{
			if (model == null)
			{
				response.Errors.Add(new ErrorModel { Message = "Invalid model." });
				response.Success = false;
				return DoResponse(response);
			}

			if (string.IsNullOrWhiteSpace(model.RelationName))
			{
				response.Errors.Add(new ErrorModel { Message = "Invalid relation name.", Key = "relationName" });
				response.Success = false;
				return DoResponse(response);
			}
			else
			{
				relation = new EntityRelationManager().Read(model.RelationName).Object;
				if (relation == null)
				{
					response.Errors.Add(new ErrorModel { Message = "Invalid relation name. No relation with that name.", Key = "relationName" });
					response.Success = false;
					return DoResponse(response);
				}
			}
			return null;
		}
		private IActionResult CollectAttachTargetRecords(InputEntityRelationRecordUpdateModel model, BaseResponseModel response, RecordManager recMan, Entity targetEntity, Field targetField, List<EntityRecord> attachTargetRecords)
		{
			EntityQuery query;
			QueryResponse result;
			foreach (var targetId in model.AttachTargetFieldRecordIds)
			{
				query = new EntityQuery(targetEntity.Name, "id," + targetField.Name, EntityQuery.QueryEQ("id", targetId), null, null, null);
				result = recMan.Find(query);
				if (result.Object.Data.Count == 0)
				{
					response.Errors.Add(new ErrorModel { Message = "Attach target record was not found. Id=[" + targetEntity.Id + "]", Key = "targetRecordId" });
					response.Success = false;
					return DoResponse(response);
				}
				else if (attachTargetRecords.Any(x => (Guid)x["id"] == targetId))
				{
					response.Errors.Add(new ErrorModel { Message = "Attach target id was duplicated. Id=[" + targetEntity.Id + "]", Key = "targetRecordId" });
					response.Success = false;
					return DoResponse(response);
				}
				attachTargetRecords.Add(result.Object.Data[0]);
			}
			return null;
		}
		private IActionResult CollectDetachTargetRecords(InputEntityRelationRecordUpdateModel model, BaseResponseModel response, RecordManager recMan, Entity targetEntity, Field targetField, List<EntityRecord> detachTargetRecords)
		{
			EntityQuery query;
			QueryResponse result;
			foreach (var targetId in model.DetachTargetFieldRecordIds)
			{
				query = new EntityQuery(targetEntity.Name, "id," + targetField.Name, EntityQuery.QueryEQ("id", targetId), null, null, null);
				result = recMan.Find(query);
				if (result.Object.Data.Count == 0)
				{
					response.Errors.Add(new ErrorModel { Message = "Detach target record was not found. Id=[" + targetEntity.Id + "]", Key = "targetRecordId" });
					response.Success = false;
					return DoResponse(response);
				}
				else if (detachTargetRecords.Any(x => (Guid)x["id"] == targetId))
				{
					response.Errors.Add(new ErrorModel { Message = "Detach target id was duplicated. Id=[" + targetEntity.Id + "]", Key = "targetRecordId" });
					response.Success = false;
					return DoResponse(response);
				}
				detachTargetRecords.Add(result.Object.Data[0]);
			}
			return null;
		}
		private IActionResult ApplyUpdateRelationChanges(BaseResponseModel response, RecordManager recMan, EntityRelation relation, Entity targetEntity, Field targetField, object originValue, List<EntityRecord> attachTargetRecords, List<EntityRecord> detachTargetRecords)
		{
			using (var connection = DbContext.Current.CreateConnection())
			{
				connection.BeginTransaction();

				try
				{
					switch (relation.RelationType)
					{
						case EntityRelationType.OneToOne:
						case EntityRelationType.OneToMany:
							{
								var oneToManyResult = ApplyOneToManyRelationChanges(recMan, connection, response, targetEntity, targetField, originValue, attachTargetRecords, detachTargetRecords);
								if (oneToManyResult != null)
									return oneToManyResult;
							}
							break;
						case EntityRelationType.ManyToMany:
							{
								var manyToManyResult = ApplyManyToManyRelationChanges(recMan, connection, response, relation, targetField, originValue, attachTargetRecords, detachTargetRecords);
								if (manyToManyResult != null)
									return manyToManyResult;
							}
							break;
						default:
							{
								connection.RollbackTransaction();
								throw new Exception("Not supported relation type");
							}
					}

					connection.CommitTransaction();
				}
				catch (Exception ex)
				{
					connection.RollbackTransaction();
					new LogService().Create(Diagnostics.LogType.Error, "TErpApi:UpdateEntityRelationRecord", ex);
					response.Success = false;
					response.Message = ex.Message;
					return DoResponse(response);
				}
			}
			return null;
		}
		private IActionResult ApplyOneToManyRelationChanges(RecordManager recMan, DbConnection connection, BaseResponseModel response, Entity targetEntity, Field targetField, object originValue, List<EntityRecord> attachTargetRecords, List<EntityRecord> detachTargetRecords)
		{
			foreach (var record in detachTargetRecords)
			{
				record[targetField.Name] = null;

				var updResult = recMan.UpdateRecord(targetEntity, record);
				if (!updResult.Success)
				{
					connection.RollbackTransaction();
					response.Errors = updResult.Errors;
					response.Message = "Target record id=[" + record["id"] + "] detach operation failed.";
					response.Success = false;
					return DoResponse(response);
				}
			}

			foreach (var record in attachTargetRecords)
			{
				var patchObject = new EntityRecord();
				patchObject["id"] = (Guid)record["id"];
				patchObject[targetField.Name] = originValue;

				var updResult = recMan.UpdateRecord(targetEntity, patchObject);
				if (!updResult.Success)
				{
					connection.RollbackTransaction();
					response.Errors = updResult.Errors;
					response.Message = "Target record id=[" + record["id"] + "] attach operation failed.";
					response.Success = false;
					return DoResponse(response);
				}
			}
			return null;
		}
		private IActionResult ApplyManyToManyRelationChanges(RecordManager recMan, DbConnection connection, BaseResponseModel response, EntityRelation relation, Field targetField, object originValue, List<EntityRecord> attachTargetRecords, List<EntityRecord> detachTargetRecords)
		{
			foreach (var record in detachTargetRecords)
			{
				QueryResponse updResult = recMan.RemoveRelationManyToManyRecord(relation.Id, (Guid)originValue, (Guid)record[targetField.Name]);

				if (!updResult.Success)
				{
					connection.RollbackTransaction();
					response.Errors = updResult.Errors;
					response.Message = "Target record id=[" + record["id"] + "] detach operation failed.";
					response.Success = false;
					return DoResponse(response);
				}
			}

			foreach (var record in attachTargetRecords)
			{
				QueryResponse updResult = recMan.CreateRelationManyToManyRecord(relation.Id, (Guid)originValue, (Guid)record[targetField.Name]);

				if (!updResult.Success)
				{
					connection.RollbackTransaction();
					response.Errors = updResult.Errors;
					response.Message = "Target record id=[" + record["id"] + "] attach  operation failed.";
					response.Success = false;
					return DoResponse(response);
				}
			}
			return null;
		}
		private IActionResult ValidateUpdateRelationReverseModel(InputEntityRelationRecordReverseUpdateModel model, BaseResponseModel response, ref EntityRelation relation)
		{
			if (model == null)
			{
				response.Errors.Add(new ErrorModel { Message = "Invalid model." });
				response.Success = false;
				return DoResponse(response);
			}

			if (string.IsNullOrWhiteSpace(model.RelationName))
			{
				response.Errors.Add(new ErrorModel { Message = "Invalid relation name.", Key = "relationName" });
				response.Success = false;
				return DoResponse(response);
			}
			else
			{
				relation = new EntityRelationManager().Read(model.RelationName).Object;
				if (relation == null)
				{
					response.Errors.Add(new ErrorModel { Message = "Invalid relation name. No relation with that name.", Key = "relationName" });
					response.Success = false;
					return DoResponse(response);
				}
			}
			return null;
		}
		private IActionResult CollectAttachOriginRecords(InputEntityRelationRecordReverseUpdateModel model, BaseResponseModel response, RecordManager recMan, Entity originEntity, Field originField, List<EntityRecord> attachOriginRecords)
		{
			EntityQuery query;
			QueryResponse result;
			foreach (var originId in model.AttachOriginFieldRecordIds)
			{
				query = new EntityQuery(originEntity.Name, "id," + originField.Name, EntityQuery.QueryEQ("id", originId), null, null, null);
				result = recMan.Find(query);
				if (result.Object.Data.Count == 0)
				{
					response.Errors.Add(new ErrorModel { Message = "Attach origin record was not found. Id=[" + originEntity.Id + "]", Key = "originRecordId" });
					response.Success = false;
					return DoResponse(response);
				}
				else if (attachOriginRecords.Any(x => (Guid)x["id"] == originId))
				{
					response.Errors.Add(new ErrorModel { Message = "Attach origin id was duplicated. Id=[" + originEntity.Id + "]", Key = "originRecordId" });
					response.Success = false;
					return DoResponse(response);
				}
				attachOriginRecords.Add(result.Object.Data[0]);
			}
			return null;
		}
		private IActionResult CollectDetachOriginRecords(InputEntityRelationRecordReverseUpdateModel model, BaseResponseModel response, RecordManager recMan, Entity originEntity, Field originField, List<EntityRecord> detachOriginRecords)
		{
			EntityQuery query;
			QueryResponse result;
			foreach (var originId in model.DetachOriginFieldRecordIds)
			{
				query = new EntityQuery(originEntity.Name, "id," + originField.Name, EntityQuery.QueryEQ("id", originId), null, null, null);
				result = recMan.Find(query);
				if (result.Object.Data.Count == 0)
				{
					response.Errors.Add(new ErrorModel { Message = "Detach origin record was not found. Id=[" + originEntity.Id + "]", Key = "originRecordId" });
					response.Success = false;
					return DoResponse(response);
				}
				else if (detachOriginRecords.Any(x => (Guid)x["id"] == originId))
				{
					response.Errors.Add(new ErrorModel { Message = "Detach origin id was duplicated. Id=[" + originEntity.Id + "]", Key = "originRecordId" });
					response.Success = false;
					return DoResponse(response);
				}
				detachOriginRecords.Add(result.Object.Data[0]);
			}
			return null;
		}
		private IActionResult ApplyUpdateRelationReverseChanges(BaseResponseModel response, RecordManager recMan, EntityRelation relation, Entity originEntity, Field originField, object targetValue, List<EntityRecord> attachOriginRecords, List<EntityRecord> detachOriginRecords)
		{
			using (var connection = DbContext.Current.CreateConnection())
			{
				connection.BeginTransaction();

				try
				{
					switch (relation.RelationType)
					{
						case EntityRelationType.OneToOne:
						case EntityRelationType.OneToMany:
							{
								var oneToManyResult = ApplyOneToManyRelationReverseChanges(recMan, connection, response, originEntity, originField, targetValue, attachOriginRecords, detachOriginRecords);
								if (oneToManyResult != null)
									return oneToManyResult;
							}
							break;
						case EntityRelationType.ManyToMany:
							{
								var manyToManyResult = ApplyManyToManyRelationReverseChanges(recMan, connection, response, relation, originField, targetValue, attachOriginRecords, detachOriginRecords);
								if (manyToManyResult != null)
									return manyToManyResult;
							}
							break;
						default:
							{
								connection.RollbackTransaction();
								throw new Exception("Not supported relation type");
							}
					}

					connection.CommitTransaction();
				}
				catch (Exception ex)
				{
					connection.RollbackTransaction();
					new LogService().Create(Diagnostics.LogType.Error, "TErpApi:UpdateEntityRelationRecordReverse", ex);
					response.Success = false;
					response.Message = ex.Message;
					return DoResponse(response);
				}
			}
			return null;
		}
		private IActionResult ApplyOneToManyRelationReverseChanges(RecordManager recMan, DbConnection connection, BaseResponseModel response, Entity originEntity, Field originField, object targetValue, List<EntityRecord> attachOriginRecords, List<EntityRecord> detachOriginRecords)
		{
			foreach (var record in detachOriginRecords)
			{
				record[originField.Name] = null;

				var updResult = recMan.UpdateRecord(originEntity, record);
				if (!updResult.Success)
				{
					connection.RollbackTransaction();
					response.Errors = updResult.Errors;
					response.Message = "Origin record id=[" + record["id"] + "] detach operation failed.";
					response.Success = false;
					return DoResponse(response);
				}
			}

			foreach (var record in attachOriginRecords)
			{
				var patchObject = new EntityRecord();
				patchObject["id"] = (Guid)record["id"];
				patchObject[originField.Name] = targetValue;

				var updResult = recMan.UpdateRecord(originEntity, patchObject);
				if (!updResult.Success)
				{
					connection.RollbackTransaction();
					response.Errors = updResult.Errors;
					response.Message = "Origin record id=[" + record["id"] + "] attach operation failed.";
					response.Success = false;
					return DoResponse(response);
				}
			}
			return null;
		}
		private IActionResult ApplyManyToManyRelationReverseChanges(RecordManager recMan, DbConnection connection, BaseResponseModel response, EntityRelation relation, Field originField, object targetValue, List<EntityRecord> attachOriginRecords, List<EntityRecord> detachOriginRecords)
		{
			foreach (var record in detachOriginRecords)
			{
				QueryResponse updResult = recMan.RemoveRelationManyToManyRecord(relation.Id, (Guid)record[originField.Name], (Guid)targetValue);

				if (!updResult.Success)
				{
					connection.RollbackTransaction();
					response.Errors = updResult.Errors;
					response.Message = "Origin record id=[" + record["id"] + "] detach operation failed.";
					response.Success = false;
					return DoResponse(response);
				}
			}

			foreach (var record in attachOriginRecords)
			{
				QueryResponse updResult = recMan.CreateRelationManyToManyRecord(relation.Id, (Guid)record[originField.Name], (Guid)targetValue);

				if (!updResult.Success)
				{
					connection.RollbackTransaction();
					response.Errors = updResult.Errors;
					response.Message = "Origin record id=[" + record["id"] + "] attach  operation failed.";
					response.Success = false;
					return DoResponse(response);
				}
			}
			return null;
		}
		private void ValidateCreateRelationInput(string entityName, string relationName, Guid relatedRecordId, List<ErrorModel> validationErrors, ref EntityRelation relation, ref EntityRecord relatedRecord)
		{
			//1.Validate relationName
			//1.1. Relation exists
			relation = relMan.Read().Object.SingleOrDefault(x => x.Name == relationName);
			string targetEntityName = String.Empty;
			string targetFieldName = String.Empty;
			relatedRecord = new EntityRecord();
			var relatedRecordResponse = new QueryResponse();
			if (relation == null)
			{
				var error = new ErrorModel
				{
					Key = "relationName",
					Value = relationName,
					Message = "A relation with this name, does not exist"
				};
				validationErrors.Add(error);
			}
			else
			{
				//1.2. Relation is correct - entityName is part of this relation
				if (relation.OriginEntityName != entityName && relation.TargetEntityName != entityName)
				{
					var error = new ErrorModel
					{
						Key = "relationName",
						Value = relationName,
						Message = "This is not the correct relation, as it does not include the requested entity: " + entityName
					};
					validationErrors.Add(error);
				}
				else
				{
					if (relation.OriginEntityName == entityName)
					{
						relatedRecordResponse = recMan.Find(new EntityQuery(relation.TargetEntityName, "*", EntityQuery.QueryEQ("id", relatedRecordId)));
						targetFieldName = relation.TargetFieldName;
					}
					else
					{
						relatedRecordResponse = recMan.Find(new EntityQuery(relation.OriginEntityName, "*", EntityQuery.QueryEQ("id", relatedRecordId)));
						targetFieldName = relation.OriginFieldName;
					}
					//2. Validate parentRecordId
					//2.1. parentRecordId exists

					ValidateParentRecordField(relatedRecordResponse, ref relatedRecord, targetFieldName, validationErrors, entityName, relatedRecordId);
				}
			}
		}
		private void ValidateParentRecordField(QueryResponse relatedRecordResponse, ref EntityRecord relatedRecord, string targetFieldName, List<ErrorModel> validationErrors, string entityName, Guid relatedRecordId)
		{
			if (!relatedRecordResponse.Object.Data.Any())
			{
				var error = new ErrorModel
				{
					Key = "parentRecordId",
					Value = relatedRecordId.ToString(),
					Message = "There is no parent record with this Id in the entity: " + entityName
				};
				validationErrors.Add(error);
			}
			else
			{
				relatedRecord = relatedRecordResponse.Object.Data.First();
				//2.2. Record has value in the related field		
				if (!relatedRecord.Properties.ContainsKey(targetFieldName) || relatedRecord[targetFieldName] == null)
				{
					var error = new ErrorModel
					{
						Key = "parentRecordId",
						Value = relatedRecordId.ToString(),
						Message = "The parent record does not have field " + targetFieldName + " or its value is null"
					};
					validationErrors.Add(error);
				}
			}
		}
		private IActionResult ApplyCreateRelationTransaction(EntityRelation relation, string entityName, Guid relatedRecordId, EntityRecord postObj, EntityRecord relatedRecord)
		{
			//Create transaction
			var result = new QueryResponse();
			using (var connection = DbContext.Current.CreateConnection())
			{
				try
				{
					connection.BeginTransaction();

					//Add the relation field value if the relation is 1:1 or 1:N
					if (relation.RelationType == EntityRelationType.OneToOne || relation.RelationType == EntityRelationType.OneToMany)
					{
						//if currentEntity is origin -> update the parent record
						if (relation.OriginEntityName == entityName)
						{
							throw new Exception("We need a case to finish this");
						}
						else
						{
							//if currentEntity is target -> get the target field and assing the correct id value of the origin 
							postObj[relation.TargetFieldName] = relatedRecord[relation.OriginFieldName];
						}
					}

					result = recMan.CreateRecord(entityName, postObj);

					//Create a relation record if it is N:N
					if (relation.RelationType == EntityRelationType.ManyToMany)
					{
						ApplyManyToManyRelationLink(relation, entityName, relatedRecordId, postObj);
					}

					connection.CommitTransaction();
				}
				catch (Exception ex)
				{
					connection.RollbackTransaction();
					new LogService().Create(Diagnostics.LogType.Error, "TErpApi:CreateEntityRecordWithRelation", ex);
					var response = new ResponseModel
					{
						Success = false,
						Timestamp = DateTime.UtcNow,
						Message = "Error while saving the record: " + ex.Message,
						Object = null
					};
					return Json(response);
				}
			}

			return DoResponse(result);
		}
		private void ApplyManyToManyRelationLink(EntityRelation relation, string entityName, Guid relatedRecordId, EntityRecord postObj)
		{
			var response = new QueryResponse();
			if (relation.OriginEntityName == entityName && relation.TargetEntityName == entityName)
			{
				throw new Exception("current entity is both target and origin, cannot find relation direction. Probably needs to be extended");
			}
			else if (relation.TargetEntityName == entityName)
			{
				//if current is target -> create relation
				response = recMan.CreateRelationManyToManyRecord(relation.Id, relatedRecordId, (Guid)postObj["id"]);
			}
			else
			{
				//if current is origin -> create relation	
				response = recMan.CreateRelationManyToManyRecord(relation.Id, (Guid)postObj["id"], relatedRecordId);
			}
			if (!response.Success)
			{
				throw new Exception(response.Message);
			}
		}
		private void ParseRecordIds(string ids, QueryResponse response, List<Guid> recordIdList)
		{
			if (!String.IsNullOrWhiteSpace(ids) && ids != "null")
			{
				var idStringList = ids.Split(',');
				var outGuid = Guid.Empty;
				foreach (var idString in idStringList)
				{
					if (Guid.TryParse(idString, out outGuid))
					{
						recordIdList.Add(outGuid);
					}
					else
					{
						response.Message = "One of the record ids is not a Guid";
						response.Timestamp = DateTime.UtcNow;
						response.Success = false;
						response.Object.Data = null;
					}
				}
			}
		}
		private void ParseRequestedFields(string fields, List<string> fieldList)
		{
			if (!String.IsNullOrWhiteSpace(fields) && fields != "null")
			{
				var fieldsArray = fields.Split(',');
				var hasId = false;
				foreach (var fieldName in fieldsArray)
				{
					if (fieldName == "id")
					{
						hasId = true;
					}
					fieldList.Add(fieldName);
				}
				if (!hasId)
				{
					fieldList.Add("id");
				}
			}
		}
		private EntityQuery BuildRecordsQuery(string entityName, List<Guid> recordIdList, List<string> fieldList, int? limit)
		{
			var QueryList = new List<QueryObject>();
			foreach (var recordId in recordIdList)
			{
				QueryList.Add(EntityQuery.QueryEQ("id", recordId));
			}

			QueryObject recordsFilterObj = null;
			if (QueryList.Count > 0)
			{
				recordsFilterObj = EntityQuery.QueryOR(QueryList.ToArray());
			}

			var columns = "*";
			if (fieldList.Count > 0)
			{
				if (!fieldList.Contains("id"))
				{
					fieldList.Add("id");
				}
				columns = String.Join(",", fieldList.Select(x => x.ToString()).ToArray());
			}

			//var sortRulesList = new List<QuerySortObject>();
			//var sortRule = new QuerySortObject("id",QuerySortType.Descending);
			//sortRulesList.Add(sortRule);
			//EntityQuery query = new EntityQuery(entityName, columns, recordsFilterObj, sortRulesList.ToArray(), null, null);

			EntityQuery query = new EntityQuery(entityName, columns, recordsFilterObj, null, null, null);
			if (limit != null && limit > 0)
			{
				query = new EntityQuery(entityName, columns, recordsFilterObj, null, null, limit);
			}
			return query;
		}
		private QueryObject BuildQuickSearchContainsFilter(List<string> lookupFieldsList, string query, bool matchAllFields)
		{
			QueryObject matchesFilter = null;
			if (lookupFieldsList.Count > 1)
			{
				var filterList = new List<QueryObject>();
				foreach (var field in lookupFieldsList)
				{
					filterList.Add(EntityQuery.QueryContains(field, query));
				}
				if (matchAllFields)
				{
					matchesFilter = EntityQuery.QueryAND(filterList.ToArray());
				}
				else
				{
					matchesFilter = EntityQuery.QueryOR(filterList.ToArray());
				}

			}
			else
			{
				matchesFilter = EntityQuery.QueryContains(lookupFieldsList[0], query);
			}
			return matchesFilter;
		}
		private QueryObject BuildQuickSearchStartsWithFilter(List<string> lookupFieldsList, string query, bool matchAllFields)
		{
			QueryObject matchesFilter = null;
			if (lookupFieldsList.Count > 1)
			{
				var filterList = new List<QueryObject>();
				foreach (var field in lookupFieldsList)
				{
					filterList.Add(EntityQuery.QueryStartsWith(field, query));
				}
				if (matchAllFields)
				{
					matchesFilter = EntityQuery.QueryAND(filterList.ToArray());
				}
				else
				{
					matchesFilter = EntityQuery.QueryOR(filterList.ToArray());
				}

			}
			else
			{
				matchesFilter = EntityQuery.QueryStartsWith(lookupFieldsList[0], query);
			}
			return matchesFilter;
		}
		private QueryObject BuildQuickSearchFtsFilter(List<string> lookupFieldsList, string query, bool matchAllFields)
		{
			QueryObject matchesFilter = null;
			if (lookupFieldsList.Count > 1)
			{
				var filterList = new List<QueryObject>();
				foreach (var field in lookupFieldsList)
				{
					filterList.Add(EntityQuery.QueryFTS(field, query));
				}
				if (matchAllFields)
				{
					matchesFilter = EntityQuery.QueryAND(filterList.ToArray());
				}
				else
				{
					matchesFilter = EntityQuery.QueryOR(filterList.ToArray());
				}

			}
			else
			{
				matchesFilter = EntityQuery.QueryFTS(lookupFieldsList[0], query);
			}
			return matchesFilter;
		}
		private QueryObject BuildQuickSearchEqFilter(List<string> lookupFieldsList, string query, bool matchAllFields)
		{
			QueryObject matchesFilter = null;
			if (lookupFieldsList.Count > 1)
			{
				var filterList = new List<QueryObject>();
				foreach (var field in lookupFieldsList)
				{
					filterList.Add(EntityQuery.QueryEQ(field, query));
				}
				if (matchAllFields)
				{
					matchesFilter = EntityQuery.QueryAND(filterList.ToArray());
				}
				else
				{
					matchesFilter = EntityQuery.QueryOR(filterList.ToArray());
				}

			}
			else
			{
				matchesFilter = EntityQuery.QueryEQ(lookupFieldsList[0], query);
			}
			return matchesFilter;
		}
		private QueryObject BuildQuickSearchMatchFilter(string matchMethod, List<string> lookupFieldsList, string query, bool matchAllFields)
		{
			QueryObject matchesFilter = null;
			#region <<Generate filters >>
			switch (matchMethod.ToLowerInvariant())
			{
				case "contains":
					matchesFilter = BuildQuickSearchContainsFilter(lookupFieldsList, query, matchAllFields);
					break;
				case "startswith":
					matchesFilter = BuildQuickSearchStartsWithFilter(lookupFieldsList, query, matchAllFields);
					break;
				case "fts":
					matchesFilter = BuildQuickSearchFtsFilter(lookupFieldsList, query, matchAllFields);
					break;
				default: // EQ
					matchesFilter = BuildQuickSearchEqFilter(lookupFieldsList, query, matchAllFields);
					break;

			}
			#endregion
			return matchesFilter;
		}
		private void BuildQuickSearchForceFilters(string forceFiltersCsv, ref QueryObject matchesFilter)
		{
			#region << Generate force filters >>
			var forceFilters = new List<QueryObject>();
			if (!String.IsNullOrWhiteSpace(forceFiltersCsv))
			{
				foreach (var forceFilter in forceFiltersCsv.Split(','))
				{
					var filterArray = forceFilter.Split(':');
					if (filterArray.Length == 3)
					{
						switch (filterArray[1].ToLowerInvariant())
						{
							case "guid":
								var filterValueGuid = new Guid(filterArray[2]);
								forceFilters.Add(EntityQuery.QueryEQ(filterArray[0], filterValueGuid));
								break;
							case "bool":
								if (filterArray[2] == "true")
								{
									forceFilters.Add(EntityQuery.QueryEQ(filterArray[0], true));
								}
								else
								{
									forceFilters.Add(EntityQuery.QueryEQ(filterArray[0], false));
								}
								break;
							case "datetime":
								var filterValueDate = Convert.ToDateTime(filterArray[2]);
								forceFilters.Add(EntityQuery.QueryEQ(filterArray[0], filterValueDate));
								break;
							case "int":
								var filterValueInt = Convert.ToInt64(filterArray[2]);
								forceFilters.Add(EntityQuery.QueryEQ(filterArray[0], filterValueInt));
								break;
							case "string":
								forceFilters.Add(EntityQuery.QueryEQ(filterArray[0], filterArray[2]));
								break;
							default:
								break;

						}
					}
				}

			}

			if (forceFilters.Count > 0)
			{
				var forceFilterQuery = EntityQuery.QueryAND(forceFilters.ToArray());
				matchesFilter = EntityQuery.QueryAND(forceFilterQuery, matchesFilter);
			}

			#endregion
		}
		private List<QuerySortObject> BuildQuickSearchSorts(string sortField, string sortType)
		{
			var sortsList = new List<QuerySortObject>();
			#region << Generate Sorts >>
			if (!String.IsNullOrWhiteSpace(sortField))
			{
				if (sortType.ToLowerInvariant() == "desc")
				{
					sortsList.Add(new QuerySortObject(sortField, QuerySortType.Descending));
				}
				else
				{
					sortsList.Add(new QuerySortObject(sortField, QuerySortType.Ascending));
				}
			}

			#endregion
			return sortsList;
		}
		private void ExecuteQuickSearchFind(string findType, string entityName, string returnFieldsCsv, QueryObject matchesFilter, List<QuerySortObject> sortsList, int skipRecords, int limitRecords, EntityRecord responseObject)
		{
			if (findType.ToLowerInvariant() == "records" || findType.ToLowerInvariant() == "records-and-count" || findType.ToLowerInvariant() == "records&count")
			{
				var matchQueryResponse = recMan.Find(new EntityQuery(entityName, returnFieldsCsv, matchesFilter, sortsList.ToArray(), skipRecords, limitRecords));
				if (!matchQueryResponse.Success)
				{
					throw new Exception(matchQueryResponse.Message);
				}
				responseObject["records"] = matchQueryResponse.Object.Data;
			}

			if (findType.ToLowerInvariant() == "count" || findType.ToLowerInvariant() == "records-and-count" || findType.ToLowerInvariant() == "records&count")
			{
				var matchQueryResponse = recMan.Count(new EntityQuery(entityName, returnFieldsCsv, matchesFilter));
				if (!matchQueryResponse.Success)
				{
					throw new Exception(matchQueryResponse.Message);
				}
				responseObject["count"] = matchQueryResponse.Object;
			}
		}
		private string BuildDownloadFilePath(string root, string root2, string root3, string root4, string fileName)
		{
			var filePathArray = new List<string>();
			if (root != null) filePathArray.Add(root);
			if (root2 != null) filePathArray.Add(root2);
			if (root3 != null) filePathArray.Add(root3);
			if (root4 != null) filePathArray.Add(root4);

			var filePath = "/" + String.Join("/", filePathArray) + "/" + fileName;

			filePath = filePath.ToLowerInvariant();
			return filePath;
		}
		private IActionResult CheckDownloadNotModified(DbFile file)
		{
			//check for modification
			string headerModifiedSince = Request.Headers["If-Modified-Since"];
			if (headerModifiedSince != null)
			{
				if (DateTime.TryParse(headerModifiedSince, out DateTime isModifiedSince))
				{
					if (isModifiedSince <= file.LastModificationDate)
					{
						Response.StatusCode = 304;
						return new EmptyResult();
					}
				}
			}
			return null;
		}
		private void ParseDownloadRequestOptions(string extension)
		{
			IDictionary<string, StringValues> queryCollection = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(HttpContext.Request.QueryString.ToString());
			string action = queryCollection.Keys.Any(x => x == "action") ? ((string)queryCollection["action"]).ToLowerInvariant() : "";
			string requestedMode = queryCollection.Keys.Any(x => x == "mode") ? ((string)queryCollection["mode"]).ToLowerInvariant() : "";
			string width = queryCollection.Keys.Any(x => x == "width") ? ((string)queryCollection["width"]).ToLowerInvariant() : "";
			string height = queryCollection.Keys.Any(x => x == "height") ? ((string)queryCollection["height"]).ToLowerInvariant() : "";
			bool isImage = extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".gif";

			int widthInt = 0;
			if (!String.IsNullOrWhiteSpace(width) && int.TryParse(width, out int outWidthInt))
			{
				widthInt = outWidthInt;
			}
			int heightInt = 0;
			if (!String.IsNullOrWhiteSpace(height) && int.TryParse(height, out int outHeightInt))
			{
				heightInt = outHeightInt;
			}
		}
		private IActionResult ValidateAndApplySchedulePlan(JObject postObject, SchedulePlan schedulePlan, ResponseModel response)
		{
			#region << Validate >>

			foreach (var prop in postObject.Properties())
			{
				ApplySchedulePlanProperty(prop, postObject, schedulePlan, response);
			}

			if (schedulePlan.StartDate >= schedulePlan.EndDate)
			{
				if (postObject.Properties().Any(p => p.Name == "start_date"))
					response.Errors.Add(new ErrorModel("start_date", postObject["start_date"].ToString(), "Start date must be before end date."));
				else
					response.Errors.Add(new ErrorModel("end_date", postObject["end_date"].ToString(), "End date must be greater than start date."));
			}

			if ((schedulePlan.Type == SchedulePlanType.Daily || schedulePlan.Type == SchedulePlanType.Interval) && !schedulePlan.ScheduledDays.HasOneSelectedDay())
				response.Errors.Add(new ErrorModel("schedule_days", postObject["schedule_days"].ToString(), "At least one day have to be selected for schedule days field."));

			if (schedulePlan.Type == SchedulePlanType.Interval && schedulePlan.IntervalInMinutes <= 0 || schedulePlan.IntervalInMinutes >= 1440)
				response.Errors.Add(new ErrorModel("interval_in_minutes", postObject["interval_in_minutes"].ToString(), "The value of Interval in minutes field must be greater than 0 and less or  equal than 1440."));

			if (response.Errors.Count > 0)
			{
				response.Success = false;
				return DoResponse(response);
			}

			#endregion
			return null;
		}
		private void ApplySchedulePlanProperty(JProperty prop, JObject postObject, SchedulePlan schedulePlan, ResponseModel response)
		{
			switch (prop.Name)
			{
				case "name":
					{
						ApplySchedulePlanName(postObject, schedulePlan, response);
					}
					break;
				case "type":
					{
						ApplySchedulePlanType(postObject, schedulePlan, response);
					}
					break;
				case "job_type_id":
					{
						ApplySchedulePlanJobTypeId(postObject, schedulePlan, response);
					}
					break;
				case "start_date":
					{
						ApplySchedulePlanStartDate(postObject, schedulePlan, response);
					}
					break;
				case "end_date":
					{
						ApplySchedulePlanEndDate(postObject, schedulePlan, response);
					}
					break;
				case "schedule_days":
					{
						ApplySchedulePlanScheduleDays(postObject, schedulePlan, response);
					}
					break;
				case "interval_in_minutes":
					{
						ApplySchedulePlanIntervalInMinutes(postObject, schedulePlan, response);
					}
					break;
				case "start_timespan":
					{
						ApplySchedulePlanStartTimespan(postObject, schedulePlan, response);
					}
					break;
				case "end_timespan":
					{
						ApplySchedulePlanEndTimespan(postObject, schedulePlan, response);
					}
					break;
				case "enabled":
					{
						ApplySchedulePlanEnabled(postObject, schedulePlan, response);
					}
					break;
			}
		}
		private void ApplySchedulePlanName(JObject postObject, SchedulePlan schedulePlan, ResponseModel response)
		{
			if (!string.IsNullOrWhiteSpace((string)postObject["name"]))
			{
				schedulePlan.Name = (string)postObject["name"];
			}
			else
			{
				response.Errors.Add(new ErrorModel("name", (string)postObject["name"], "Name is required field and cannot be empty."));
			}
		}
		private void ApplySchedulePlanType(JObject postObject, SchedulePlan schedulePlan, ResponseModel response)
		{
			if (!string.IsNullOrWhiteSpace(postObject["type"].ToString()))
			{
				if (int.TryParse(postObject["type"].ToString(), out int type))
				{
					if (type >= 1 && type <= 4)
						schedulePlan.Type = (SchedulePlanType)type;
					else
						response.Errors.Add(new ErrorModel("type", postObject["type"].ToString(), "The value of the type is out of range of valid values."));
				}
				else
					response.Errors.Add(new ErrorModel("type", postObject["type"].ToString(), "Type is invalid integer value."));
			}
			else
			{
				response.Errors.Add(new ErrorModel("type", postObject["type"].ToString(), "Type is required field and cannot be empty."));
			}
		}
		private void ApplySchedulePlanJobTypeId(JObject postObject, SchedulePlan schedulePlan, ResponseModel response)
		{
			if (Guid.TryParse(postObject["job_type_id"].ToString(), out Guid jobTypeId))
			{
				if (JobManager.JobTypes.Any(t => t.Id == jobTypeId))
				{
					schedulePlan.JobTypeId = jobTypeId;
				}
				else
				{
					response.Errors.Add(new ErrorModel("job_type_id", postObject["job_type_id"].ToString(), "There is no job type with such id."));
				}
			}
			else
			{
				response.Errors.Add(new ErrorModel("job_type_id", postObject["job_type_id"].ToString(), "Job type id is not valid."));
			}
		}
		private void ApplySchedulePlanStartDate(JObject postObject, SchedulePlan schedulePlan, ResponseModel response)
		{
			schedulePlan.StartDate = DateTime.UtcNow;

			if (!string.IsNullOrWhiteSpace(postObject["start_date"].ToString()))
			{
				if (DateTime.TryParse(postObject["start_date"].ToString(), out DateTime startDate))
				{
					startDate = (DateTime)postObject["start_date"];
					schedulePlan.StartDate = startDate.ToUniversalTime();
				}
				else
				{
					response.Errors.Add(new ErrorModel("start_date", postObject["start_date"].ToString(), "The value of start date field is not valid."));
				}
			}
		}
		private void ApplySchedulePlanEndDate(JObject postObject, SchedulePlan schedulePlan, ResponseModel response)
		{
			if (!string.IsNullOrWhiteSpace(postObject["end_date"].ToString()))
			{
				if (DateTime.TryParse(postObject["end_date"].ToString(), out DateTime endDate))
				{
					endDate = (DateTime)postObject["end_date"];
					schedulePlan.StartDate = endDate.ToUniversalTime();
				}
				else
				{
					response.Errors.Add(new ErrorModel("end_date", postObject["end_date"].ToString(), "The value of end date field is not valid."));
				}
			}
		}
		private void ApplySchedulePlanScheduleDays(JObject postObject, SchedulePlan schedulePlan, ResponseModel response)
		{
			string days = postObject["schedule_days"].ToString();
			if (!string.IsNullOrWhiteSpace(days))
			{
				schedulePlan.ScheduledDays = JsonConvert.DeserializeObject<SchedulePlanDaysOfWeek>(postObject["schedule_days"].ToString());
			}
			else
			{
				response.Errors.Add(new ErrorModel("schedule_days", postObject["schedule_days"].ToString(), "Schedule days is required field and cannot be empty."));
			}
		}
		private void ApplySchedulePlanIntervalInMinutes(JObject postObject, SchedulePlan schedulePlan, ResponseModel response)
		{
			if (int.TryParse(postObject["interval_in_minutes"].ToString(), out int interval))
			{
				schedulePlan.IntervalInMinutes = interval;
			}
			else
			{
				response.Errors.Add(new ErrorModel("interval_in_minutes", postObject["interval_in_minutes"].ToString(), "The value of Interval in minutes field is not valid."));
			}
		}
		private void ApplySchedulePlanStartTimespan(JObject postObject, SchedulePlan schedulePlan, ResponseModel response)
		{
			if (DateTime.TryParse(postObject["start_timespan"].ToString(), out DateTime startTimespan))
			{
				startTimespan = ((DateTime)postObject["start_timespan"]);
				schedulePlan.StartTimespan = startTimespan.Hour * 60 + startTimespan.Minute;
			}
			else
			{
				response.Errors.Add(new ErrorModel("start_timespan", postObject["start_timespan"].ToString(), "The value of start timespan is not valid."));
			}
		}
		private void ApplySchedulePlanEndTimespan(JObject postObject, SchedulePlan schedulePlan, ResponseModel response)
		{
			if (DateTime.TryParse(postObject["end_timespan"].ToString(), out DateTime endTimespan))
			{
				endTimespan = ((DateTime)postObject["end_timespan"]);
				schedulePlan.EndTimespan = endTimespan.Hour * 60 + endTimespan.Minute;
				if (schedulePlan.EndTimespan == 0) //that's mean 12PM
					schedulePlan.EndTimespan = 1440;
			}
			else
			{
				response.Errors.Add(new ErrorModel("end_timespan", postObject["end_timespan"].ToString(), "The value of end timespan is not valid."));
			}
		}
		private void ApplySchedulePlanEnabled(JObject postObject, SchedulePlan schedulePlan, ResponseModel response)
		{
			schedulePlan.Enabled = (bool)postObject["enabled"];
		}
		private List<QueryObject> BuildSystemLogFilters(DateTime? fromDate, DateTime? untilDate, string type, string source, string message, string notificationStatus)
		{
			var filterList = new List<QueryObject>();
			if (fromDate != null)
			{
				filterList.Add(EntityQuery.QueryGT("created_on", fromDate));
			}
			if (untilDate != null)
			{
				filterList.Add(EntityQuery.QueryLT("created_on", untilDate));
			}
			if (!String.IsNullOrWhiteSpace(type))
			{
				filterList.Add(EntityQuery.QueryEQ("type", type));
			}
			if (!String.IsNullOrWhiteSpace(source))
			{
				filterList.Add(EntityQuery.QueryContains("source", source));
			}
			if (!String.IsNullOrWhiteSpace(message))
			{
				filterList.Add(EntityQuery.QueryContains("message", message));
			}
			if (!String.IsNullOrWhiteSpace(notificationStatus))
			{
				filterList.Add(EntityQuery.QueryEQ("notificationStatus", notificationStatus));
			}
			return filterList;
		}
		private void ProcessUserFileUpload(IFormFile file, ErpUser currentUser, List<EntityRecord> resultRecords)
		{
			var fileBuffer = ReadFully(file.OpenReadStream());
			var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.ToString().Trim().ToLowerInvariant();
			if (fileName.StartsWith("\"", StringComparison.InvariantCulture))
				fileName = fileName.Substring(1);

			if (fileName.EndsWith("\"", StringComparison.InvariantCulture))
				fileName = fileName.Substring(0, fileName.Length - 1);

			var recMan = new RecordManager();
			DbFileRepository fsRepository = new DbFileRepository();
			string section = Guid.NewGuid().ToString().Replace("-", "").ToLowerInvariant();
			var filePath = "/user_file/" + currentUser.Id + "/" + section + "/" + fileName;
			var createdFile = fsRepository.Create(filePath, fileBuffer, DateTime.Now, currentUser.Id);
			var userFileId = Guid.NewGuid();

			var userFileRecord = new EntityRecord();
			#region << record fill >>
			userFileRecord["id"] = userFileId;
			userFileRecord["created_on"] = DateTime.Now;
			userFileRecord["name"] = fileName;
			userFileRecord["size"] = Math.Round((decimal)(file.Length / 1024), 0);
			userFileRecord["path"] = filePath;

			FillUserFileRecordType(userFileRecord, filePath, fileBuffer);
			#endregion

			var recordCreateResult = recMan.CreateRecord("user_file", userFileRecord);
			if (!recordCreateResult.Success)
			{
				throw new Exception(recordCreateResult.Message);
			}
			resultRecords.Add(userFileRecord);
		}
		private void FillUserFileRecordType(EntityRecord userFileRecord, string filePath, byte[] fileBuffer)
		{
			var mimeType = MimeMapping.MimeUtility.GetMimeMapping(filePath);
			var fileExtension = Path.GetExtension(filePath);
			if (mimeType.StartsWith("image"))
			{
				var dimensionsRecord = Helpers.GetImageDimension(fileBuffer);
				userFileRecord["width"] = (decimal)dimensionsRecord["width"];
				userFileRecord["height"] = (decimal)dimensionsRecord["height"];
				userFileRecord["type"] = "image";
			}
			else if (mimeType.StartsWith("video"))
			{
				userFileRecord["type"] = "video";
			}
			else if (mimeType.StartsWith("audio"))
			{
				userFileRecord["type"] = "audio";
			}
			else if (fileExtension == ".doc" || fileExtension == ".docx" || fileExtension == ".odt" || fileExtension == ".rtf"
			 || fileExtension == ".txt" || fileExtension == ".pdf" || fileExtension == ".html" || fileExtension == ".htm" || fileExtension == ".ppt"
			  || fileExtension == ".pptx" || fileExtension == ".xls" || fileExtension == ".xlsx" || fileExtension == ".ods" || fileExtension == ".odp")
			{
				userFileRecord["type"] = "document";
			}
			else
			{
				userFileRecord["type"] = "other";
			}
		}
		private void ProcessFileUpload(IFormFile file, List<EntityRecord> resultRecords)
		{
			var fileBuffer = ReadFully(file.OpenReadStream());
			var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.ToString().Trim().ToLowerInvariant();
			if (fileName.StartsWith("\"", StringComparison.InvariantCulture))
				fileName = fileName.Substring(1);

			if (fileName.EndsWith("\"", StringComparison.InvariantCulture))
				fileName = fileName.Substring(0, fileName.Length - 1);

			var recMan = new RecordManager();
			DbFileRepository fsRepository = new DbFileRepository();
			DbFile dbFile = fsRepository.CreateTempFile(fileName, fileBuffer);

			var resultRec = new EntityRecord();

			resultRec["id"] = dbFile.Id;
			resultRec["created_on"] = DateTime.Now;
			resultRec["name"] = fileName;
			resultRec["size"] = Math.Round((decimal)(file.Length / 1024), 0);
			resultRec["path"] = dbFile.FilePath;

			FillFileRecordType(resultRec, dbFile, fileBuffer);

			resultRecords.Add(resultRec);
		}
		private void FillFileRecordType(EntityRecord resultRec, DbFile dbFile, byte[] fileBuffer)
		{
			var mimeType = MimeMapping.MimeUtility.GetMimeMapping(dbFile.FilePath);
			var fileExtension = Path.GetExtension(dbFile.FilePath);
			if (mimeType.StartsWith("image"))
			{
				var dimensionsRecord = Helpers.GetImageDimension(fileBuffer);
				resultRec["width"] = (decimal)dimensionsRecord["width"];
				resultRec["height"] = (decimal)dimensionsRecord["height"];
				resultRec["type"] = "image";
			}
			else if (mimeType.StartsWith("video"))
			{
				resultRec["type"] = "video";
			}
			else if (mimeType.StartsWith("audio"))
			{
				resultRec["type"] = "audio";
			}
			else if (fileExtension == ".doc" || fileExtension == ".docx" || fileExtension == ".odt" || fileExtension == ".rtf"
			 || fileExtension == ".txt" || fileExtension == ".pdf" || fileExtension == ".html" || fileExtension == ".htm" || fileExtension == ".ppt"
			  || fileExtension == ".pptx" || fileExtension == ".xls" || fileExtension == ".xlsx" || fileExtension == ".ods" || fileExtension == ".odp")
			{
				resultRec["type"] = "document";
			}
			else
			{
				resultRec["type"] = "other";
			}
		}
		#endregion
	}
}
