namespace GSS.TrackingCenterService
{
	using System;
	using System.ServiceModel;
	using System.ServiceModel.Activation;
	using Entities;
	using Interfaces;

	/// <summary>
	///  Version 1 of Tracking Center Service. 
	///  <para>Changes:</para>
	///  <para>Added KitName to Work Item. Using WorkItem1, WorkItemResponse1, and WorkItemsResponse1.</para>
	/// </summary>
	[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
	[AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
	[ServiceKnownType(typeof(Response))]
	[ServiceKnownType(typeof(Fault))]
	public class GSSTrackingCenterService1 : ITrackingCenter1
	{
		static bool initialized = false;
		static object initialize = new object();

		public GSSTrackingCenterService1()
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

		#region ITrackingCenter1
		// All functions below are calling the newer versions. No work is being done here.

		public WorkItemNoteResponse AddWorkItemNote(string processID, string userID, string noteText, int displayMode, int noteFormat, bool advisorVisible, string source)
		{
			return new GSSTrackingCenterService2().AddWorkItemNote(processID, userID, noteText, displayMode, noteFormat, advisorVisible, source);
		}

		public WorkItemNoteResponse AddWorkItemNoteWeb(string processID, string userID, string userName, string noteText, int displayMode, int noteFormat)
		{
			return new GSSTrackingCenterService2().AddWorkItemNoteWeb(processID, userID, userName, noteText, displayMode, noteFormat);
		}

		public WorkItemsCountResponse GetOpenWorkItemCount(string userID, string[] agentID, string clientID)
		{
			return new GSSTrackingCenterService2().GetOpenWorkItemCount(userID, agentID, clientID);
		}

		public WorkItemResponse1 GetWorkItemDetails(string processID, string userID, string sourceID)
		{
			return DowngradeWorkItemResponse(new GSSTrackingCenterService2().GetWorkItemDetails(processID, userID, sourceID));
		}

		public WorkItemNotesResponse GetWorkItemNotes(string processID, string userID, long noteID)
		{
			return new GSSTrackingCenterService2().GetWorkItemNotes(processID, userID, noteID);
		}

		public WorkItemTypesResponse1 GetWorkItemTypes(string userID, string[] agentID, string clientID, string filter, DateTime? fromDate, DateTime? toDate)
		{
			return new GSSTrackingCenterService2().GetWorkItemTypes(userID, agentID, clientID, filter, fromDate, toDate);
		}

		public WorkItemsResponse1 GetWorkitemList(string userID, string BDID, string advisorID, string[] agentID, string clientID, string accountID, string type, string status, DateTime? fromDate, DateTime? toDate, string source)
		{
			return DowngradeWorkItemsResponse(new GSSTrackingCenterService2().GetWorkitemList(userID, BDID, advisorID, agentID, clientID, accountID, type, status, fromDate, toDate, source));
		}

		public WorkItemsResponse1 GetWorkitemListWeb(string userID, string[] agentID, string clientID, string accountID, string filter, string[] sortOrder, DateTime? fromDate, DateTime? toDate, int start, int count, bool computeCount)
		{
			return DowngradeWorkItemsResponse(new GSSTrackingCenterService2().GetWorkitemListWeb(userID, agentID, clientID, accountID, filter, sortOrder, fromDate, toDate, start, count, computeCount));
		}

		public EnvelopIDResponse GetEnvelopeID(string userID, string agentID, string clientID, string accountID)
		{
			return new GSSTrackingCenterService2().GetEnvelopeID(userID, agentID, clientID, accountID);
		}

		public WorkItemResponse1 UpdateWorkItem(string processID, string userID, WorkItem1 workItem)
		{
			return DowngradeWorkItemResponse(new GSSTrackingCenterService2().UpdateWorkItem(processID, userID, UpgradeWorkItem(workItem)));
		}

		public WorkItemNoteResponse UpdateNoteAdvisorVisible(string processID, string userID, int NoteID, bool AdvisorVisible)
		{
			return new GSSTrackingCenterService2().UpdateNoteAdvisorVisible(processID, userID, NoteID, AdvisorVisible);
		}

		public AlertQueueResponse AddAlertQueueItem(string processID, string subject, string message)
		{
			return new GSSTrackingCenterService2().AddAlertQueueItem(processID, subject, message);
		}

		public WQDocumentUploadResponse DocumentUpload(string userid, string username, string FileName, string ExternalID, string status, byte[] fileContent)
		{
			return new GSSTrackingCenterService2().DocumentUpload(userid, username, FileName, ExternalID, status, fileContent);
		}

		public GetQueuesResponse GetProcessAlerts()
		{
			return new GSSTrackingCenterService2().GetProcessAlerts();
		}

		public WorkItemResponse1 GetBPMItemDetails(string externalID, string userID)
		{
			return DowngradeWorkItemResponse(new GSSTrackingCenterService2().GetBPMItemDetails(externalID, userID));
		}

		public DocumentStatusResponse GetDocumentStatus(string externalID)
		{
			return new GSSTrackingCenterService2().GetDocumentStatus(externalID, null);
		}

		public Response AddDocumentAction(string doc_id, string externalID, int? processID, int? applicationID, int actionType, string userID, string actionDescription)
		{
			return new GSSTrackingCenterService2().AddDocumentAction(doc_id, externalID, processID, applicationID, actionType, userID, actionDescription);
		}

		#endregion

		/// <summary>
		///  Manual conversion from WorkItem to WorkItem1. 
		/// </summary>
		private WorkItem2 UpgradeWorkItem(WorkItem1 oldWorkItem)
		{
			WorkItem2 newWorkItem = null;

			if (oldWorkItem != null)
			{
				newWorkItem = new WorkItem2
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
					KitName = oldWorkItem.KitName
				};
			}

			return newWorkItem;
		}

		/// <summary>
		///  Manual conversion from WorkItem1 to WorkItem. Removed inheritance from versioning.
		/// </summary>
		private WorkItem1 DowngradeWorkItem(WorkItem2 oldWorkItem)
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
					KitName = oldWorkItem.KitName
				};
			}

			return newWorkItem;
		}

		/// <summary>
		///  Manual conversion from WorkItem1 to WorkItem. Removed inheritance from versioning.
		/// </summary>
		private WorkItem1[] DowngradeWorkItems(WorkItem2[] oldWorkItems)
		{
			WorkItem1[] newWorkItems = null;

			if (oldWorkItems != null)
			{
				newWorkItems = new WorkItem1[oldWorkItems.Length];

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
		private WorkItemResponse1 DowngradeWorkItemResponse(WorkItemResponse2 existingResponse)
		{
			WorkItemResponse1 downgradedResponse = null;

			if (existingResponse != null)
			{
				downgradedResponse = new WorkItemResponse1
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
		private WorkItemsResponse1 DowngradeWorkItemsResponse(WorkItemsResponse2 existingResponse)
		{
			WorkItemsResponse1 downgradedResponse = null;

			if (existingResponse != null)
			{
				downgradedResponse = new WorkItemsResponse1
				{
					WorkItems = DowngradeWorkItems(existingResponse.WorkItems),
					AggregationValues = existingResponse.AggregationValues,
					Fault = existingResponse.Fault
				};
			}

			return downgradedResponse;
		}

	}
}
