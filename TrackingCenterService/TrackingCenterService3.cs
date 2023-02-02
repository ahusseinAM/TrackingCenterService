namespace GSS.TrackingCenterService
{
	using System;
	using System.ServiceModel;
	using System.ServiceModel.Activation;
	using System.Linq;
	using Entities;
	using Interfaces;
	using Utility;
	using Common.Logging;
	using System.Collections.Generic;
	using Newtonsoft.Json;

	[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
	[AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
	[ServiceKnownType(typeof(Response))]
	[ServiceKnownType(typeof(Fault))]
	public class GSSTrackingCenterService3 : ITrackingCenter3
	{
		#region Variables

		static bool initialized = false;
		static object initialize = new object();
		private static LongCounter cnt = new LongCounter("TrackingCenterService", 0L);
		private static readonly ILog log = LogManager.GetCurrentClassLogger();
		private static readonly string serviceVersion = "TRACKINGCENTER 3.0";

		#endregion

		#region Constructor

		public GSSTrackingCenterService3()
			: base()
		{
			Initialize();
		}

		#endregion

		#region Service Initialize

		public static void Initialize()
		{
			lock (initialize)
			{
				if (!initialized)
				{
					initialized = true;
				}
			}
		}

		#endregion

		#region Service Operations

		/// <summary>
		/// Add new alert on ExternalID and queue them in ProcessAlertQueue table in TC database.
		/// </summary>
		public AlertQueueResponse AddAlertQueueItem(string processID, string subject, string message, AlertType alertType, string documentID = null)
		{
			var response = new AlertQueueResponse();

			try
			{
				using (var trackingProcessor = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response = trackingProcessor.AddAlertQueueItem(processID.ConvertToCheckID(), subject, message, alertType, documentID);
				}
			}
			catch (Exception ex)
			{
				string errorMessage = $"MethodName:AddAlertQueueItem processID:{processID} subject:{subject} message:{message} alertType:{alertType} documentID:{documentID}";

				log.Error(errorMessage, ex);
				response = (AlertQueueResponse)FaultUtility.InitFault(response, ex, serviceVersion, errorMessage);
			}

			return response;
		}

		/// <summary>
		/// Method will return list of Note and Document alerts which are not processed by PI service to BPM.
		/// </summary>
		public GetQueuesResponse GetProcessAlerts()
		{
			var response = new GetQueuesResponse();

			try
			{
				using (var tcprocessor = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.ProcessAlerts = tcprocessor.GetProcessAlerts();
				}
			}
			catch (Exception ex)
			{
				string message = string.Format("MethodName:{0} Message:{1}", "GetProcessAlerts", ex.Message);

				log.Error(message, ex);
				response = (GetQueuesResponse)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Method will updated Processed flag from 0 to 1 in ProcessAlertQueue table.
		/// </summary>
		public UpdateQueuesResponse UpdateAlertsAsProcessedInBatch(List<UpdateProcessAlertStatus> alertRequests)
		{
			UpdateQueuesResponse response = null;

			try
			{
				if (alertRequests != null && alertRequests.Any())
				{
					using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
					{
						response = tTracking.UpdateAlertsAsProcessedInBatch(alertRequests);
					}
				}
			}
			catch (Exception ex)
			{
				response = response ?? new UpdateQueuesResponse();
				string message = string.Format("MethodName:{0} Message:{1}", "UpdateAlertsAsProcessedInBatch", ex.Message);

				log.Error(message, ex);
				response = (UpdateQueuesResponse)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Add new note on item (i.e., ExternalID) with additional display setting in BPM/eWM/Access.
		/// </summary>
		public Response<int> AddWorkItemNote(string processID, string userID, string noteText, int displayMode, int noteFormat, bool advisorVisible, string source)
		{
			var response = new Response<int>();

			try
			{
				if (string.IsNullOrWhiteSpace(noteText))
				{
					throw new ArgumentNullException(nameof(noteText));
				}

				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.AddWorkItemNote(processID.ConvertToCheckID(),
															userID,
															null,
															(int)NoteUserType.InternalUser,
															noteText,
															displayMode,
															noteFormat,
															advisorVisible,
															source);
				}
			}
			catch (Exception ex)
			{
				string message = string.Format("MethodName:{0} ProcessID:{1} UserId:{2} NoteText:{3} Source:{4} Message:{5}", "AddWorkItemNote", processID, userID, noteText, source, ex.Message);

				log.Error(message, ex);
				response = (Response<int>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Add note on workitem (i.e., ExternalID) with advisorFlag visible and will be avaible in eWM/Access.
		/// </summary>
		public Response<int> AddWorkItemNoteWeb(string processID, string userID, string userName, string noteText, int displayMode, int noteFormat)
		{
			var response = new Response<int>();

			try
			{
				if (string.IsNullOrWhiteSpace(noteText))
				{
					throw new ArgumentNullException(nameof(noteText));
				}

				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.AddWorkItemNote(processID.ConvertToCheckID(),
															userID,
															userName,
															(int)NoteUserType.ExternalUser,
															noteText,
															displayMode,
															noteFormat,
															true,
															WorkItemSource.TrackingCenter.ToString());
				}
			}
			catch (Exception ex)
			{
				string message = string.Format("MethodName:{0} ProcessID:{1} UserId:{2} NoteText:{3} Message:{4}", "AddWorkItemNoteWeb", processID, userID, noteText, ex.Message);

				log.Error(message, ex);
				response = (Response<int>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// UpdateNoteAdvisorVisible will update AdvisorVisible flag from 0 to 1 or vice versa in ItemNote table.
		/// </summary>
		public WorkItemNoteResponse UpdateNoteAdvisorVisible(string processID, string userID, int NoteID, bool AdvisorVisible)
		{
			var response = new WorkItemNoteResponse();

			try
			{
				if (string.IsNullOrEmpty(processID))
				{
					throw new ArgumentNullException(nameof(processID));
				}

				if (NoteID <= 0)
				{
					throw new ArgumentNullException(nameof(NoteID));
				}

				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.WorkItemNote = tTracking.UpdateNoteAdvisorVisible(processID.ConvertToCheckID(), userID, NoteID, AdvisorVisible);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:UpdateNoteAdvisorVisible processID:{processID} userID:{userID} NoteID:{NoteID} AdvisorVisible:{AdvisorVisible} message:{ex.Message}";

				log.Error(message, ex);
				response = (WorkItemNoteResponse)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Operation will return all Notes for that item (i.e., ExternalID)  
		/// </summary>
		public WorkItemNotesResponse GetWorkItemNotes(string processID, string userID, long noteID, string source = "")
		{
			var response = new WorkItemNotesResponse();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.WorkItemNotes = tTracking.GetWorkItemNotes(processID.ConvertToCheckID(), userID, noteID, source);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetWorkItemNotes processID:{processID} userID:{userID} noteID:{noteID} source:{source} message:{ex.Message}";

				log.Error(message, ex);
				response = (WorkItemNotesResponse)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Upload document from eWM TrackingCenter to Filebound for D#/B#.
		/// </summary>
		public WQDocumentUploadResponse DocumentUpload(string userid, string username, string FileName, string ExternalID, string status, byte[] fileContent)
		{
			var response = new WQDocumentUploadResponse();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response = tTracking.DocumentUpload(userid, username, FileName, ExternalID, status, fileContent);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:DocumentUpload userid:{userid} username:{username} FileName:{FileName} ExternalID:{ExternalID} status:{status} message:{ex.Message}";

				log.Error(message, ex);
				response = (WQDocumentUploadResponse)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Add new document action to track history of Images/Document (i.e., Doc_ID) or ExternalID.
		/// </summary>
		public Response<int> AddDocumentAction(string doc_id, string externalID, int? processID, int? applicationID, int actionType, string userID, string actionDescription)
		{
			var response = new Response<int>();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.AddDocumentAction(doc_id, externalID, processID, applicationID, actionType, userID, actionDescription);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:AddDocumentAction docId:{doc_id} externalID:{externalID} processID:{processID} appId:{applicationID} actionType:{actionType} userID:{userID} actionDescription:{actionDescription} message:{ex.Message}";

				log.Error(message, ex);
				response = (Response<int>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Method is used to determine status and action on document from BPM.
		/// </summary>
		public DocumentStatusResponse GetDocumentStatus(string externalID, int? bundleID)
		{
			var response = new DocumentStatusResponse();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.DocStatus = tTracking.GetDocumentStatus(externalID, bundleID);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetEnvelopeID externalID:{externalID} bundleID:{bundleID} message:{ex.Message}";

				log.Error(message, ex);
				response = (DocumentStatusResponse)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Method will add new EnvelopeID in TrackingCenter database.
		/// </summary>
		public EnvelopIDResponse GetEnvelopeID(string userID, string agentID, string clientID, string accountID)
		{
			var response = new EnvelopIDResponse();

			try
			{
				using (var tcprocessor = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.EnvelopeID = tcprocessor.AddEnvelope(null);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetEnvelopeID userID:{userID} agentID:{agentID} clientID:{clientID} accountID:{accountID} message:{ex.Message}";

				log.Error(message, ex);
				response = (EnvelopIDResponse)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Update StandAlone (i.e. Regular) Client and Account level work-item
		/// </summary>
		public WorkItemResponse3 UpdateWorkItem(string processID, string userID, WorkItem3 workItem)
		{
			var response = new WorkItemResponse3();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.WorkItem = tTracking.UpdateWorkItem(0, processID.ConvertToCheckID(), userID, workItem);
				}
			}
			catch (Exception ex)
			{
				string message = string.Format("MethodName:{0} ProcessID:{1} Request:{2} Message:{3}", "UpdateWorkItem", processID, JsonConvert.SerializeObject(workItem), ex.Message);

				log.Error(message, ex);
				response = (WorkItemResponse3)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Update ENAX Bundle (i.e. Parent) client level work-item
		/// </summary>
		public Response<string> UpdateParentWorkItem(string processID, string userID, ParentWorkItem parentWorkItem)
		{
			var response = new Response<string>();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.UpdateParentWorkItem(0, processID.ConvertToCheckID(), userID, parentWorkItem);
				}
			}
			catch (Exception ex)
			{
				string message = string.Format("MethodName:{0} ProcessID:{1} Request:{2}", "UpdateParentWorkItem", processID, JsonConvert.SerializeObject(parentWorkItem));

				log.Error(message, ex);
				response = (Response<string>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Update ENAX Component (i.e., Child) client ot account level work-item.
		/// </summary>
		public Response<string> UpdateChildWorkItem(string processID, string userID, ChildWorkItem childWorkItem)
		{
			var response = new Response<string>();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.UpdateChildWorkItem(processID, userID, childWorkItem);
				}
			}
			catch (Exception ex)
			{
				string message = string.Format("MethodName:{0} ProcessID:{1} Request:{2}", "UpdateChildWorkItem", processID, JsonConvert.SerializeObject(childWorkItem));

				log.Error(message, ex);
				response = (Response<string>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// AddItemToBundle operation will add new component item to bundle.
		/// </summary>
		public Response<bool> AddItemToBundle(string externalID, string parentExternalID, string shortExternalDescription, int rankOrder)
		{
			var response = new Response<bool>();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.AddItemToBundle(externalID, parentExternalID, shortExternalDescription, rankOrder);
				}
			}
			catch (Exception ex)
			{
				string message = string.Format("MethodName:{0} ExternalID:{1} ParentExternalID:{2} ShortExternalDescription:{3} RankOrder:{4}", "AddItemToBundle", externalID, parentExternalID, shortExternalDescription, rankOrder);

				log.Error(message, ex);
				response = (Response<bool>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Service operation will add/remove Component item from Bundle.
		/// </summary>
		public Response<int> UpdateItemToBundle(string processID, string userID, ChildWorkItem childWorkItem)
		{
			var response = new Response<int>();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.UpdateItemToBundle(processID, userID, childWorkItem);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:UpdateItemToBundle ProcessID:{processID} UserID:{userID} Request:{JsonConvert.SerializeObject(childWorkItem)}";

				log.Error(message, ex);
				response = (Response<int>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Service operation will update account association details for StandAlone, Bundle (i.e.,Parent), Component (i.e., Child) items.
		/// </summary>
		public Response<int> UpdateItemAssociation(string externalID, int itemAssociationID, string accountID, string portfolioID, string fundingAccNo, string custodialAccNo, string custodian, string itemName, ItemStatus? itemStatus, bool isActive, string userID)
		{
			var response = new Response<int>();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.UpdateItemAssociation(externalID.ConvertToCheckID(), itemAssociationID, accountID,  portfolioID, fundingAccNo, custodialAccNo, custodian, itemName, itemStatus, isActive);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:UpdateItemAssociation externalID:{externalID} itemAssociationID:{itemAssociationID} accountID:{accountID} fundingAccNo:{fundingAccNo} custodialAccNo:{custodialAccNo} custodian:{custodian} itemName:{itemName} itemName:{itemStatus} isActive:{isActive} userID:{userID} message:{ex.Message}";

				log.Error(message, ex);
				response = (Response<int>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Service operation will update Item name details for Component (i.e., Child) items.
		/// </summary>
		public Response<bool> UpdateItemAssociationName(string parentExternalID, IDictionary<string, string> itemAssociation, string userID)
		{
			var response = new Response<bool>();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.UpdateItemAssociationName(parentExternalID.ConvertToCheckID(), itemAssociation);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:UpdateItemAssociationName parentExternalID:{parentExternalID} userID:{userID} message:{ex.Message}";
				log.Error(message, ex);
				response = (Response<bool>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Method will return all WorkItemTypes for open StandAlone and Bundle (i.e., Parent) items.
		/// </summary>
		public WorkItemTypesResponse1 GetWorkItemTypes(string userID, string[] agentID, string clientID, string filter, DateTime? fromDate, DateTime? toDate)
		{
			var response = new WorkItemTypesResponse1();
			string agentIds = agentID != null && agentID.Any() ? string.Join(",", agentID) : string.Empty;

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.WorkItemTypes = tTracking.GetWorkItemTypes(userID, agentID, clientID, filter, fromDate, toDate);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetWorkItemTypes userID:{userID} agentIds:{agentIds} clientID:{clientID} filter:{filter} fromDate:{fromDate} toDate:{toDate} message:{ex.Message}";
				log.Error(message, ex);
				response = (WorkItemTypesResponse1)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Method will return count of open StandAlone and Bundle (i.e., Parent) items.
		/// </summary>
		/// <param name="userID"></param>
		/// <param name="agentID"></param>
		/// <param name="clientID"></param>
		/// <returns></returns>
		public WorkItemsCountResponse GetOpenWorkItemCount(string userID, string[] agentID, string clientID)
		{
			var response = new WorkItemsCountResponse();
			string agentIds = agentID != null && agentID.Any() ? string.Join(",", agentID) : string.Empty;

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response = tTracking.GetOpenWorkItemCount(userID, agentID, clientID);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetOpenWorkItemCount userID:{userID} agentIds:{agentIds} clientID:{clientID} message:{ex.Message}";
				log.Error(message, ex);
				response = (WorkItemsCountResponse)FaultUtility.InitFault(response, ex, serviceVersion, "GetOpenWorkItemCount");
			}

			return response;
		}

		/// <summary>
		/// Method will return StandAlone work-item details
		/// </summary>
		public WorkItemResponse3 GetWorkItemDetails(string processID, string userID, string sourceID)
		{
			var response = new WorkItemResponse3();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.WorkItem = tTracking.GetWorkItemDetails(processID.ConvertToCheckID(),
																	sourceID);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetWorkItemDetails userID:{userID} processID:{processID} sourceID:{sourceID} message:{ex.Message}";
				log.Error(message, ex);
				response = (WorkItemResponse3)FaultUtility.InitFault(response, ex, serviceVersion, "GetWorkItemDetails");
			}

			return response;
		}

		/// <summary>
		/// Service operation will return StandAlone item details based on ExternalID.
		/// </summary>
		public WorkItemResponse3 GetBPMItemDetails(string externalID, string userID)
		{
			var response = new WorkItemResponse3();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.WorkItem = tTracking.GetBPMItemDetails(externalID.ConvertToCheckID());
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetBPMItemDetails userID:{userID} externalID:{externalID} message:{ex.Message}";
				log.Error(message, ex);
				response = (WorkItemResponse3)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Service operattion will return details of Component (i.e., Child) item details based on ExternalID
		/// </summary>
		public ChildWorkItemResponse GetChildWorkItemDetails(string processID, string userID, string sourceID)
		{
			var response = new ChildWorkItemResponse();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.ChildWorkItem = tTracking.GetChildWorkItemDetails(processID.ConvertToCheckID(), userID, sourceID);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetChildWorkItemDetails userID:{userID} processID:{processID} sourceID:{sourceID} message:{ex.Message}";
				log.Error(message, ex);
				response = (ChildWorkItemResponse)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Service operation will return entire Bundle (i.e., Parent) and all associated Components (i.e.,Child) items details
		/// based on Parent/Child ExternalID.
		/// </summary>
		public RelatedWorkItemsResponse GetRelatedWorkItemDetails(string externalID, string userID, WorkItemSource source)
		{
			var response = new RelatedWorkItemsResponse();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response = tTracking.GetRelatedWorkItemDetails(externalID.ConvertToCheckID(), source);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetRelatedWorkItemDetails userID:{userID} externalID:{externalID} source:{source} message:{ex.Message}";
				log.Error(message, ex);
				response = (RelatedWorkItemsResponse)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Method will return list of  item acocunt association details from ItemAssociation table.
		/// </summary>
		public WorkItemAssociationsResponse GetItemAssociation(string externalID, string userID)
		{
			var response = new WorkItemAssociationsResponse();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.WorkItemAssociations = tTracking.GetItemAssociations(externalID.ConvertToCheckID(), userID);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetItemAssociation userID:{userID} externalID:{externalID} message:{ex.Message}";
				log.Error(message, ex);
				response = (WorkItemAssociationsResponse)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Service operation will return StandAlone and Bundle items based on search criterias.
		/// </summary>
		public WorkItemsResponse3 GetWorkitemList(string userID, string BDID, string advisorID, string[] agentID, string clientID, string accountID, string type, string status, DateTime? fromDate, DateTime? toDate, string source)
		{
			var response = new WorkItemsResponse3();
			string agentIds = agentID != null && agentID.Any() ? string.Join(",", agentID) : string.Empty;

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.WorkItems = tTracking.GetWorkItemList(userID, BDID, advisorID, agentID, clientID, accountID, type, status, fromDate, toDate, source);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetWorkitemList userID:{userID} BDID:{BDID} advisorID:{advisorID} agentIds:{agentIds} clientID:{clientID} accountID:{accountID} type:{type} status:{status} fromDate:{fromDate} toDate:{toDate} source:{source} message:{ex.Message}";
				log.Error(message, ex);
				response = (WorkItemsResponse3)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Service operation will return StandAlone and Bundle items based on search criterias.
		/// </summary>
		public WorkItemsResponse3 GetWorkitemListWeb(string userID, string[] agentID, string clientID, string accountID, string filter, string[] sortOrder, DateTime? fromDate, DateTime? toDate, int start, int count, bool computeCount)
		{
			var response = new WorkItemsResponse3();

			string agentIds = agentID != null && agentID.Any() ? string.Join(",", agentID) : string.Empty;
			string sortOrders = sortOrder != null && sortOrder.Any() ? string.Join(",", sortOrder) : string.Empty;

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response = tTracking.GetWorkitemListWeb(userID, agentID, clientID, accountID, filter, sortOrder, fromDate, toDate, start, count, computeCount);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetWorkitemListWeb userID:{userID} agentIds:{agentIds} clientID:{clientID} accountID:{accountID} filter:{filter} sortOrders:{sortOrders} fromDate:{fromDate} toDate:{toDate} start:{start} count:{count} computeCount:{computeCount} message:{ex.Message}";
				log.Error(message, ex);
				response = (WorkItemsResponse3)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Service operation will ExternalID and ShortExternalDescription based on Bundle (i.e., Parent) ExternalID.
		/// </summary>
		public WorkItemListResponse GetItemDescription(string userID, string externalID)
		{
			WorkItemListResponse response = new WorkItemListResponse();

			try
			{
				if (string.IsNullOrWhiteSpace(externalID))
				{
					throw new ArgumentNullException(nameof(externalID));
				}

				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.WorkItemList = tTracking.GetExternalDescription(externalID.ConvertToCheckID());
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetItemDescription userID:{userID} externalID:{externalID} message:{ex.Message}";
				log.Error(message, ex);
				response = (WorkItemListResponse)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// 
		/// </summary>
		public WorkItemsResponse3 GetBPMWorkItemList(string[] agentId, string[] clientId, string[] accountId, string[] accountNumber, string filter, string[] status = null, string[] internalStatus = null)
		{
			var response = new WorkItemsResponse3();

			string agentIds = agentId != null && agentId.Any() ? string.Join(",", agentId) : string.Empty;
			string clientIds = clientId != null && clientId.Any() ? string.Join(",", clientId) : string.Empty;
			string accountIds = accountId != null && accountId.Any() ? string.Join(",", accountId) : string.Empty;
			string accountNumbers = accountNumber != null && accountNumber.Any() ? string.Join(",", accountNumber) : string.Empty;
			string statuses = status != null && status.Any() ? string.Join(",", status) : string.Empty;
			string internalStatuses = internalStatus != null && internalStatus.Any() ? string.Join(",", internalStatus) : string.Empty;

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.WorkItems = tTracking.GetBPMWorkItemList(agentId, clientId, accountId, accountNumber, filter, status, internalStatus);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:GetBPMWorkItemList agentIds:{agentIds} agentIds:{agentIds} clientIds:{clientIds} accountIds:{accountIds} accountNumbers:{accountNumbers} statuses:{statuses} internalStatuses:{internalStatuses} filter:{filter} Message:{ex.Message}";
				log.Error(message, ex);
				response = (WorkItemsResponse3)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// UpdateHideNoteFlag will update HideNote flag from 0 to 1 or vice versa in ItemNote table only from BPM.
		/// </summary>
		public Response<bool> UpdateNoteInternalVisible(string externalId, int noteId, bool internalVisible, string userId)
		{
			var response = new Response<bool>();

			try
			{
				if (string.IsNullOrWhiteSpace(externalId))
				{
					throw new ArgumentNullException(nameof(externalId));
				}

				if (string.IsNullOrWhiteSpace(userId))
				{
					throw new ArgumentNullException(nameof(userId));
				}

				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.UpdateNoteInternalVisible(externalId, noteId, internalVisible, userId);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:UpdateHideNoteFlag ExternalID:{externalId} NoteId:{noteId} UserId:{userId} InternalVisible:{internalVisible} Message:{ex.Message}";
				log.Error(message, ex);
				response = (Response<bool>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Service operation will be only called by CloseAgedTrackingCenterItemJob from ActiveBatch.
		/// </summary>
		public Response<ClosedAgedOpenWorkItemResponse> CloseAgedOpenDNumber()
		{
			Response<ClosedAgedOpenWorkItemResponse> response = new Response<ClosedAgedOpenWorkItemResponse>();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.CloseAgedOpenDNumber();
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:CloseAgedOpenDNumber Message:{ex.Message}";
				log.Error(message, ex);
				response = (Response<ClosedAgedOpenWorkItemResponse>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="isEsignatureItem"></param>
		/// <param name="numberofDays"></param>
		/// <param name="status"></param>
		/// <param name="filter"></param>
		/// <returns></returns>
		public WorkItemsResponse3 GetAgedOpenWorkItemList(bool isEsignatureItem, bool submitToBD, int numberofDays, string[] status, string filter)
		{
			var response = new WorkItemsResponse3();

			try
			{
				if (numberofDays == 0)
				{
					throw new ArgumentException("numberofDays - should be a valid number");
				}

				if (string.IsNullOrWhiteSpace(filter))
				{
					throw new ArgumentException("filter criteria missing. There should be at least one filter criteria to get aged open work-items.");
				}

				if (status == null || status.Length == 0)
				{
					throw new ArgumentException("status - There should be at least one valid work-item status.");
				}

				using (var trackingCenterProcessor = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response = trackingCenterProcessor.GetAgedOpenWorkItemList(isEsignatureItem, submitToBD, numberofDays, status, filter);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName: GetAgedOpenWorkItemList isEsignatureItem: {isEsignatureItem} submitToBD: {submitToBD} numberofDays: {numberofDays} status:{string.Join(",", status)} filter:{filter} message:{ex.Message}";
				log.Error(message, ex);
				response = (WorkItemsResponse3)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		/// <summary>
		/// Service operation will be only called by CloseAgedTrackingCenterItemJob from ActiveBatch.
		/// </summary>
		public Response<ClosedAgedOpenWorkItemResponse> CloseAgedOpenNNumber()
		{
			var response = new Response<ClosedAgedOpenWorkItemResponse>();

			try
			{
				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.CloseAgedOpenNNumber();
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:CloseAgedOpenNNumber Message:{ex.Message}";
				log.Error(message, ex);
				response = (Response<ClosedAgedOpenWorkItemResponse>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		public Response<bool> DeleteEnvelope(int envelopeId)
		{
			var response = new Response<bool>();

			try
			{
				if (envelopeId <= 0)
				{
					throw new ArgumentException($"Invalid EnvelopeId:{envelopeId}");
				}

				using (var tTracking = new TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = tTracking.DeleteEnvelope(envelopeId);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:DeleteEnvelope Message:{ex.Message}";
				log.Error(message, ex);
				response = (Response<bool>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		public Response<SearchWorkItemResponse> SearchWorkItem(SearchWorkItemRequest request)
		{
			var response = new Response<SearchWorkItemResponse>();

			try
			{
				if (request == null)
				{
					throw new ArgumentException($"Request cannot be null or blank.");
				}

				if (request.PageNumber <= 0)
				{
					request.PageNumber = 1;
				}

				if (request.PageSize <= 0)
				{
					request.PageSize = 100;
				}

				using (var trackingCenterProcessor = new GSS.TrackingCenter.BusinessProcessor.TrackingCenterBusinessProcessor())
				{
					response.Data = trackingCenterProcessor.SearchWorkItem(request);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:SearchWorkItem Message:{ex.Message} Request:{JsonConvert.SerializeObject(request)}";
				log.Error(message, ex);
				response = (Response<SearchWorkItemResponse>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		public Response<BAWSearchWorkItemResponse> BAWSearchWorkItem(BAWSearchWorkItemRequest request)
		{
			var response = new Response<BAWSearchWorkItemResponse>();

			try
			{
				if (request == null)
				{
					throw new ArgumentException($"Request cannot be null or blank.");
				}

				if ((request.PreviousPageLastItemId.HasValue && request.PreviousPageLastItemId.Value > 0 && string.IsNullOrWhiteSpace(request.PreviousPageLastItemModifiedDateTime))
					|| (!string.IsNullOrWhiteSpace(request.PreviousPageLastItemModifiedDateTime) && (!request.PreviousPageLastItemId.HasValue || request.PreviousPageLastItemId.Value <= 0)))
				{
					throw new ArgumentException($"Invalid request.");
				}

				if (request.PageSize <= 0)
				{
					request.PageSize = 100;
				}

				using (var trackingCenterProcessor = new GSS.TrackingCenterProcessor.TrackingCenterProcessor())
				{
					response.Data = trackingCenterProcessor.BAWSearchWorkItem(request);
				}
			}
			catch (Exception ex)
			{
				string message = $"MethodName:SearchWorkItem Message:{ex.Message} Request:{JsonConvert.SerializeObject(request)}";
				log.Error(message, ex);
				response = (Response<BAWSearchWorkItemResponse>)FaultUtility.InitFault(response, ex, serviceVersion, message);
			}

			return response;
		}

		#endregion
	}
}
