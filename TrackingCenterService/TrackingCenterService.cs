namespace GSS.TrackingCenterService
{
	using System;
	using System.ServiceModel;
	using System.ServiceModel.Activation;
	using System.Reflection;
	using Entities;
	using Interfaces;
	using Utility;
	using Common.Logging;

	/// <summary>
	///  Original version of tracking center. Going to be removed in a future release. Use newest version instead. 
	///  <para>This version is currently being kept for the BPM product.</para>
	/// </summary>

	[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
	[AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
	[ServiceKnownType(typeof(Response))]
	[ServiceKnownType(typeof(Fault))]
	public class GSSTrackingCenterService : ITrackingCenter
	{
		static bool initialized = false;
		static object initialize = new object();

		public GSSTrackingCenterService()
			: base()
		{
			Initialize();
		}

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

		/// <summary>
		///  Manual conversion from WorkItem to WorkItem1. 
		/// </summary>
		private WorkItem1 UpgradeWorkItem(WorkItem oldWorkItem)
		{
			WorkItem1 newWorkItem = null;

			if (oldWorkItem != null)
			{
				newWorkItem = new WorkItem1
				{
					Id = oldWorkItem.Id,
					ExternalId = oldWorkItem.ExternalId,
					InternalStatus = oldWorkItem.InternalStatus,
					OldExternalID = oldWorkItem.OldExternalID,
					Source = oldWorkItem.Source,
					BDID = oldWorkItem.BDID,
					AdvisorID = oldWorkItem.AdvisorID,
					AgentID = oldWorkItem.AgentID,
					ClientID = oldWorkItem.ClientID,
					AccountID = oldWorkItem.AccountID,
					Description = oldWorkItem.Description,
					Custodian = oldWorkItem.Custodian,
					CustodialAccNo = oldWorkItem.CustodialAccNo,
					FundingAccNo = oldWorkItem.FundingAccNo,
					Status = oldWorkItem.Status,
					WorkItemType = oldWorkItem.WorkItemType,
					CreatedBy = oldWorkItem.CreatedBy,
					CreatedDate = oldWorkItem.CreatedDate,
					ModifiedDate = oldWorkItem.ModifiedDate,
					ClosedBy = oldWorkItem.ClosedBy,
					ClosedDate = oldWorkItem.ClosedDate,
					AdvisorVisible = oldWorkItem.AdvisorVisible,
					ResponseAllowed = oldWorkItem.ResponseAllowed,
					ResponseRequired = oldWorkItem.ResponseRequired,
					ObjectName = oldWorkItem.ObjectName,
					Notes = oldWorkItem.Notes,
					DocuSignID = oldWorkItem.DocuSignID,
					WebStatus = oldWorkItem.WebStatus,
					SortOrderByStatus = oldWorkItem.SortOrderByStatus,
					eSigType = oldWorkItem.eSigType,
					UploadAllowed = oldWorkItem.UploadAllowed,
					IsClosable = oldWorkItem.IsClosable,
					KitName = null
				};
			}

			return newWorkItem;
		}

		/// <summary>
		///  Manual conversion from WorkItem1 to WorkItem. Removed inheritance from versioning.
		/// </summary>
		private WorkItem DowngradeWorkItem(WorkItem1 oldWorkItem)
		{
			WorkItem newWorkItem = null;

			if (oldWorkItem != null)
			{
				newWorkItem = new WorkItem
				{
					Id = oldWorkItem.Id,
					ExternalId = oldWorkItem.ExternalId,
					InternalStatus = oldWorkItem.InternalStatus,
					OldExternalID = oldWorkItem.OldExternalID,
					Source = oldWorkItem.Source,
					BDID = oldWorkItem.BDID,
					AdvisorID = oldWorkItem.AdvisorID,
					AgentID = oldWorkItem.AgentID,
					ClientID = oldWorkItem.ClientID,
					AccountID = oldWorkItem.AccountID,
					Description = oldWorkItem.Description,
					Custodian = oldWorkItem.Custodian,
					CustodialAccNo = oldWorkItem.CustodialAccNo,
					FundingAccNo = oldWorkItem.FundingAccNo,
					Status = oldWorkItem.Status,
					WorkItemType = oldWorkItem.WorkItemType,
					CreatedBy = oldWorkItem.CreatedBy,
					CreatedDate = oldWorkItem.CreatedDate,
					ModifiedDate = oldWorkItem.ModifiedDate,
					ClosedBy = oldWorkItem.ClosedBy,
					ClosedDate = oldWorkItem.ClosedDate,
					AdvisorVisible = oldWorkItem.AdvisorVisible,
					ResponseAllowed = oldWorkItem.ResponseAllowed,
					ResponseRequired = oldWorkItem.ResponseRequired,
					ObjectName = oldWorkItem.ObjectName,
					Notes = oldWorkItem.Notes,
					DocuSignID = oldWorkItem.DocuSignID,
					WebStatus = oldWorkItem.WebStatus,
					SortOrderByStatus = oldWorkItem.SortOrderByStatus,
					eSigType = oldWorkItem.eSigType,
					UploadAllowed = oldWorkItem.UploadAllowed,
					IsClosable = oldWorkItem.IsClosable
				};
			}

			return newWorkItem;
		}

		/// <summary>
		///  Manual conversion from WorkItem1 to WorkItem. Removed inheritance from versioning.
		/// </summary>
		private WorkItem[] DowngradeWorkItems(WorkItem1[] oldWorkItems)
		{
			WorkItem[] newWorkItems = null;

			if (oldWorkItems != null)
			{
				newWorkItems = new WorkItem[oldWorkItems.Length];

				for (int i = 0; i < oldWorkItems.Length; i++)
				{
					newWorkItems[i] = DowngradeWorkItem(oldWorkItems[i]);
				}
			}

			return newWorkItems;
		}

		/// <summary>
		///  Manual conversion from WorkItemResponse1 to WorkItemResponse. They are not inherited from one another.
		/// </summary>
		private WorkItemResponse DowngradeWorkItemResponse(WorkItemResponse1 existingResponse)
		{
			WorkItemResponse downgradedResponse = null;

			if (existingResponse != null)
			{
				downgradedResponse = new WorkItemResponse
				{
					WorkItem = DowngradeWorkItem(existingResponse.WorkItem),
					Fault = existingResponse.Fault
				};
			}

			return downgradedResponse;
		}

		/// <summary>
		///  Manual conversion from WorkItemsResponse1 to WorkItemsResponse. They are not inherited from one another.
		/// </summary>
		private WorkItemsResponse DowngradeWorkItemsResponse(WorkItemsResponse1 existingResponse)
		{
			WorkItemsResponse downgradedResponse = null;

			if (existingResponse != null)
			{
				downgradedResponse = new WorkItemsResponse
				{
					WorkItems = DowngradeWorkItems(existingResponse.WorkItems),
					AggregationValues = existingResponse.AggregationValues,
					Fault = existingResponse.Fault
				};
			}

			return downgradedResponse;
		}

		/// <summary>
		///  Manual conversion from WorkItem1 to WorkItem. Removed inheritance from versioning.
		/// </summary>
		private WorkItemType DowngradeWorkItemType(WorkItemType1 oldWorkItemType)
		{
			WorkItemType newWorkItemType = null;

			if (oldWorkItemType != null)
			{
				newWorkItemType = new WorkItemType
				{
					CategoryID = oldWorkItemType.CategoryID,
					AgentID = oldWorkItemType.AgentID,
					ClientID = oldWorkItemType.ClientID,
					ItemType = oldWorkItemType.ItemType,
					Title = oldWorkItemType.Title,
					CreatedDate = oldWorkItemType.CreatedDate,
					Status = oldWorkItemType.Status,
					WebStatus = oldWorkItemType.WebStatus,
					AdvisorVisible = oldWorkItemType.AdvisorVisible,
					Description = oldWorkItemType.Description
				};
			}

			return newWorkItemType;
		}

		/// <summary>
		///  Manual conversion from WorkItemTypesResponse1 to WorkItemTypesResponse. They are not inherited from one another.
		/// </summary>
		private WorkItemTypesResponse DowngradeWorkItemTypesResponse(WorkItemTypesResponse1 existingResponse)
		{
			WorkItemTypesResponse downgradedResponse = null;

			if (existingResponse != null)
			{
				WorkItemType[] newWorkItemTypes = null;

				if (existingResponse.WorkItemTypes != null)
				{
					newWorkItemTypes = new WorkItemType[existingResponse.WorkItemTypes.Length];

					for (int i = 0; i < existingResponse.WorkItemTypes.Length; i++)
					{
						newWorkItemTypes[i] = DowngradeWorkItemType(existingResponse.WorkItemTypes[i]);
					}
				}

				downgradedResponse = new WorkItemTypesResponse
				{
					WorkItemTypes = newWorkItemTypes,
					Fault = existingResponse.Fault
				};
			}

			return downgradedResponse;
		}

		#region ITrackingCenter
		// All functions below are calling the newer versions. No work is being done here.

		public WorkItemNoteResponse AddWorkItemNote(string processID, string userID, string noteText, int displayMode, int noteFormat, bool advisorVisible, string source)
		{
			return new GSSTrackingCenterService1().AddWorkItemNote(processID, userID, noteText, displayMode, noteFormat, advisorVisible, source);
		}

		public WorkItemNoteResponse AddWorkItemNoteWeb(string processID, string userID, string userName, string noteText, int displayMode, int noteFormat)
		{
			return new GSSTrackingCenterService1().AddWorkItemNoteWeb(processID, userID, userName, noteText, displayMode, noteFormat);
		}

		public WorkItemsCountResponse GetOpenWorkItemCount(string userID, string[] agentID, string clientID)
		{
			return new GSSTrackingCenterService1().GetOpenWorkItemCount(userID, agentID, clientID);
		}

		public WorkItemResponse GetWorkItemDetails(string processID, string userID, string sourceID)
		{
			return DowngradeWorkItemResponse(new GSSTrackingCenterService1().GetWorkItemDetails(processID, userID, sourceID));
		}

		public WorkItemNotesResponse GetWorkItemNotes(string processID, string userID, long noteID)
		{
			return new GSSTrackingCenterService1().GetWorkItemNotes(processID, userID, noteID);
		}

		public WorkItemTypesResponse GetWorkItemTypes(string userID, string[] agentID, string clientID, string filter, DateTime? fromDate, DateTime? toDate)
		{
			return DowngradeWorkItemTypesResponse(new GSSTrackingCenterService1().GetWorkItemTypes(userID, agentID, clientID, filter, fromDate, toDate));
		}

		public WorkItemsResponse GetWorkitemList(string userID, string BDID, string advisorID, string[] agentID, string clientID, string accountID, string type, string status, DateTime? fromDate, DateTime? toDate, string source)
		{
			return DowngradeWorkItemsResponse(new GSSTrackingCenterService1().GetWorkitemList(userID, BDID, advisorID, agentID, clientID, accountID, type, status, fromDate, toDate, source));
		}

		public WorkItemsResponse GetWorkitemListWeb(string userID, string[] agentID, string clientID, string accountID, string filter, string[] sortOrder, DateTime? fromDate, DateTime? toDate, int start, int count, bool computeCount)
		{
			return DowngradeWorkItemsResponse(new GSSTrackingCenterService1().GetWorkitemListWeb(userID, agentID, clientID, accountID, filter, sortOrder, fromDate, toDate, start, count, computeCount));
		}

		public EnvelopIDResponse GetEnvelopeID(string userID, string agentID, string clientID, string accountID)
		{
			return new GSSTrackingCenterService1().GetEnvelopeID(userID, agentID, clientID, accountID);
		}

		public WorkItemResponse UpdateWorkItem(string processID, string userID, WorkItem workItem)
		{
			return DowngradeWorkItemResponse(new GSSTrackingCenterService1().UpdateWorkItem(processID, userID, UpgradeWorkItem(workItem)));
		}

		public WorkItemNoteResponse UpdateNoteAdvisorVisible(string processID, string userID, int NoteID, bool AdvisorVisible)
		{
			return new GSSTrackingCenterService1().UpdateNoteAdvisorVisible(processID, userID, NoteID, AdvisorVisible);
		}

		public AlertQueueResponse AddAlertQueueItem(string processID, string subject, string message)
		{
			return new GSSTrackingCenterService1().AddAlertQueueItem(processID, subject, message);
		}

		public WQDocumentUploadResponse DocumentUpload(string userid, string username, string FileName, string ExternalID, string status, byte[] fileContent)
		{
			return new GSSTrackingCenterService1().DocumentUpload(userid, username, FileName, ExternalID, status, fileContent);
		}

		public GetQueuesResponse GetProcessAlerts()
		{
			return new GSSTrackingCenterService1().GetProcessAlerts();
		}

		public WorkItemResponse GetBPMItemDetails(string externalID, string userID)
		{
			return DowngradeWorkItemResponse(new GSSTrackingCenterService1().GetBPMItemDetails(externalID, userID));
		}

		public DocumentStatusResponse GetDocumentStatus(string externalID)
		{
			return new GSSTrackingCenterService1().GetDocumentStatus(externalID);
		}

		public Response AddDocumentAction(string doc_id, string externalID, int? processID, int? applicationID, int actionType, string userID, string actionDescription)
		{
			return new GSSTrackingCenterService1().AddDocumentAction(doc_id, externalID, processID, applicationID, actionType, userID, actionDescription);
		}

		#endregion
	}
}
