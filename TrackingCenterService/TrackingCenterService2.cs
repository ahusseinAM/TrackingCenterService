namespace GSS.TrackingCenterService
{
	using System;
	using System.ServiceModel;
	using System.ServiceModel.Activation;
	using System.Linq;
	using Entities;
	using Interfaces;
	using Utility;
	using TrackingCenterProcessor.Utility;

	/// <summary>
	///  Version 2 of Tracking Center Service. 
	///  <para>Changes:</para>
	///  <para>Added EsigStatus, EnvelopeType, and BundleID to Work Item. Using WorkItem2, WorkItemResponse2, and WorkItemsResponse2.</para>
	/// </summary>

	[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
	[AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
	[ServiceKnownType(typeof(Response))]
	[ServiceKnownType(typeof(Fault))]
	public class GSSTrackingCenterService2 : ITrackingCenter2
	{
		static bool initialized = false;
		static object initialize = new object();

		public GSSTrackingCenterService2()
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

		#region ITrackingCenter2

		public WorkItemNoteResponse AddWorkItemNote(string processID, string userID, string noteText, int displayMode, int noteFormat, bool advisorVisible, string source)
		{
			WorkItemNoteResponse response = null;
			Response<int> noteResponse = new Response<int>();
			processID = processID.ConvertToCheckID();

			noteResponse = new GSSTrackingCenterService3().AddWorkItemNote(processID,
																	userID,
																	noteText,
																	displayMode,
																	noteFormat,
																	advisorVisible,
																	source);

			if (noteResponse != null && noteResponse.Data > 0)
			{
				response = new WorkItemNoteResponse();
				response.WorkItemNote = new WorkItemNote
				{
					Id = noteResponse.Data,
					WorkItemExternalID = processID,
					NoteText = noteText,
					CreatedBy = userID
				};
			}
			else
			{
				if (noteResponse.Fault != null)
				{
					response.Fault = new Fault();
					response.Fault = noteResponse.Fault;
				}
			}

			return response;
		}

		public WorkItemNoteResponse AddWorkItemNoteWeb(string processID, string userID, string userName, string noteText, int displayMode, int noteFormat)
		{
			WorkItemNoteResponse response = null;
			Response<int> noteResponse = new Response<int>();
			processID = processID.ConvertToCheckID();

			noteResponse = new GSSTrackingCenterService3().AddWorkItemNoteWeb(processID,
																	userID,
																	userName,
																	noteText,
																	displayMode,
																	noteFormat);

			if (noteResponse != null && noteResponse.Data > 0)
			{
				response = new WorkItemNoteResponse();
				response.WorkItemNote = new WorkItemNote
				{
					Id = noteResponse.Data,
					WorkItemExternalID = processID,
					NoteText = noteText,
					CreatedBy = userID
				};
			}
			else
			{
				if (noteResponse.Fault != null)
				{
					response.Fault = new Fault();
					response.Fault = noteResponse.Fault;
				}
			}

			return response;
		}

		public WorkItemsCountResponse GetOpenWorkItemCount(string userID, string[] agentID, string clientID)
		{
			return new GSSTrackingCenterService3().GetOpenWorkItemCount(userID,
																		agentID,
																		clientID);
		}

		public WorkItemResponse2 GetWorkItemDetails(string processID, string userID, string sourceID)
		{
			return DowngradeWorkItemResponse(new GSSTrackingCenterService3().GetWorkItemDetails(processID,
																		userID,
																		sourceID));
		}

		public WorkItemNotesResponse GetWorkItemNotes(string processID, string userID, long noteID)
		{
			return new GSSTrackingCenterService3().GetWorkItemNotes(processID,
																	userID,
																	noteID);
		}

		public WorkItemTypesResponse1 GetWorkItemTypes(string userID, string[] agentID, string clientID, string filter, DateTime? fromDate, DateTime? toDate)
		{
			return new GSSTrackingCenterService3().GetWorkItemTypes(userID,
																	agentID,
																	clientID,
																	filter,
																	fromDate,
																	toDate);
		}

		public WorkItemsResponse2 GetWorkitemList(string userID, string BDID, string advisorID, string[] agentID, string clientID, string accountID, string type, string status, DateTime? fromDate, DateTime? toDate, string source)
		{
			return DowngradeWorkItemsResponse(new GSSTrackingCenterService3().GetWorkitemList(userID,
																	BDID,
																	advisorID,
																	agentID,
																	clientID,
																	accountID,
																	type,
																	status,
																	fromDate,
																	toDate,
																	source));
		}

		public WorkItemsResponse2 GetWorkitemListWeb(string userID, string[] agentID, string clientID, string accountID, string filter, string[] sortOrder, DateTime? fromDate, DateTime? toDate, int start, int count, bool computeCount)
		{
			return DowngradeWorkItemsResponse(new GSSTrackingCenterService3().GetWorkitemListWeb(userID,
																								agentID,
																								clientID,
																								accountID,
																								filter,
																								sortOrder,
																								fromDate,
																								toDate,
																								start,
																								count,
																								computeCount));
		}

		public EnvelopIDResponse GetEnvelopeID(string userID, string agentID, string clientID, string accountID)
		{
			return new GSSTrackingCenterService3().GetEnvelopeID(userID,
																agentID,
																clientID,
																accountID);
		}

		public WorkItemResponse2 UpdateWorkItem(string processID, string userID, WorkItem2 workItem)
		{
			WorkItemResponse2 response = new WorkItemResponse2();

			if (workItem != null && !string.IsNullOrWhiteSpace(workItem.Source) && workItem.Source.Equals(WorkItemSource.Internal.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				new GSSTrackingCenterService3().UpdateWorkItem(processID, userID, UpgradeWorkItem(workItem));
				response = GetWorkItemDetails(workItem.ExternalId, userID, WorkItemSource.Internal.ToString());
			}
			else
			{
				return DowngradeWorkItemResponse(new GSSTrackingCenterService3().UpdateWorkItem(processID,
																							userID,
																							UpgradeWorkItem(workItem)));
			}

			return response;
		}

		public WorkItemNoteResponse UpdateNoteAdvisorVisible(string processID, string userID, int NoteID, bool AdvisorVisible)
		{
			return new GSSTrackingCenterService3().UpdateNoteAdvisorVisible(processID,
																			userID,
																			NoteID,
																			AdvisorVisible);
		}

		public AlertQueueResponse AddAlertQueueItem(string processID, string subject, string message)
		{
			return new GSSTrackingCenterService3().AddAlertQueueItem(processID,
																	subject,
																	message,
																	AlertType.Note);
		}

		public WQDocumentUploadResponse DocumentUpload(string userid, string username, string FileName, string ExternalID, string status, byte[] fileContent)
		{
			return new GSSTrackingCenterService3().DocumentUpload(userid,
																username,
																FileName,
																ExternalID,
																status,
																fileContent);
		}

		public GetQueuesResponse GetProcessAlerts()
		{
			return new GSSTrackingCenterService3().GetProcessAlerts();
		}

		public WorkItemResponse2 GetBPMItemDetails(string externalID, string userID)
		{
			return DowngradeWorkItemResponse(new GSSTrackingCenterService3().GetBPMItemDetails(externalID,
																								userID));
		}

		public DocumentStatusResponse GetDocumentStatus(string externalID, int? bundleID)
		{
			return new GSSTrackingCenterService3().GetDocumentStatus(externalID,
																	bundleID);
		}

		public Response AddDocumentAction(string doc_id, string externalID, int? processID, int? applicationID, int actionType, string userID, string actionDescription)
		{
			return new GSSTrackingCenterService3().AddDocumentAction(doc_id,
																	externalID,
																	processID,
																	applicationID,
																	actionType,
																	userID,
																	actionDescription);
		}

		#endregion

		#region Private Methods

		/// <summary>
		///  Manual conversion from WorkItem2 to WorkItem3. 
		/// </summary>
		private WorkItem3 UpgradeWorkItem(WorkItem2 workItem2)
		{
			/* Note: Below implemention will execute only for StandAlone items which have one ItemAssociation record and running 
			 * on Old BPM Snapshot and Access Web Service which items not migarted to new version.
			 * */
			WorkItem3 workItem3 = new WorkItem3();
			int itemAssociationID = 0;

			if (!string.IsNullOrEmpty(workItem2.Source)
				&& !workItem2.Source.Equals(WorkItemSource.Internal.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				WorkItemAssociationsResponse response = new GSSTrackingCenterService3().GetItemAssociation(workItem2.ExternalId.ConvertToCheckID(), workItem2.CreatedBy);

				if (response != null && response.WorkItemAssociations != null && response.WorkItemAssociations.Length > 0)
				{
					itemAssociationID = response.WorkItemAssociations.FirstOrDefault().ID;
				}

				WorkItemAssociation[] itemAssociation = new WorkItemAssociation[1]
				{
					new WorkItemAssociation
					{
						ID = itemAssociationID,
						AccountID = workItem2.AccountID,
						Custodian = workItem2.Custodian,
						CustodialAccountNumber = workItem2.CustodialAccNo,
						FundingAccountNumber = workItem2.FundingAccNo,
						IsActive = true,
					}
				};

				workItem3.ItemAssociations = itemAssociation;
			}

			if (workItem2 != null)
			{
				workItem3.CopyFrom<WorkItem3, WorkItem2>(workItem2);
				workItem3.ShortExternalDescription = workItem2.Source.Equals(WorkItemSource.Internal.ToString(), StringComparison.OrdinalIgnoreCase) ? string.Empty : workItem2.Description;
			}

			return workItem3;
		}

		/// <summary>
		///  Manual conversion from WorkItemResponse3 to WorkItemResponse2. They are not inherited from one another.
		/// </summary>
		private WorkItemResponse2 DowngradeWorkItemResponse(WorkItemResponse3 workItemReposnse)
		{
			WorkItemResponse2 downgradedResponse = null;

			if (workItemReposnse != null)
			{
				downgradedResponse = new WorkItemResponse2
				{
					WorkItem = DowngradeWorkItem(workItemReposnse.WorkItem),
					Fault = workItemReposnse.Fault
				};
			}

			return downgradedResponse;
		}

		/// <summary>
		///  Manual conversion from WorkItemsResponse3 to WorkItemsResponse2. They are not inherited from one another.
		/// </summary>
		private WorkItemsResponse2 DowngradeWorkItemsResponse(WorkItemsResponse3 workItemResponse)
		{
			WorkItemsResponse2 downgradedResponse = null;

			if (workItemResponse != null)
			{
				downgradedResponse = new WorkItemsResponse2
				{
					WorkItems = DowngradeWorkItems(workItemResponse.WorkItems),
					AggregationValues = workItemResponse.AggregationValues,
					Fault = workItemResponse.Fault
				};
			}

			return downgradedResponse;
		}

		/// <summary>
		///  Manual conversion from WorkItem2 to WorkItem2. Removed inheritance from versioning.
		/// </summary>
		private WorkItem2[] DowngradeWorkItems(WorkItem3[] workItems)
		{
			WorkItem2[] newWorkItems = null;

			if (workItems != null && workItems.Length > 0)
			{
				newWorkItems = new WorkItem2[workItems.Length];

				for (int i = 0; i < workItems.Length; i++)
				{
					newWorkItems[i] = DowngradeWorkItem(workItems[i]);
				}
			}

			return newWorkItems;
		}

		/// <summary>
		///  Manual conversion from WorkItem3 to WorkItem2. Removed inheritance from versioning.
		/// </summary>
		private WorkItem2 DowngradeWorkItem(WorkItem3 workItem3)
		{
			WorkItem2 workItem2 = null;

			if (workItem3 != null)
			{
				workItem2 = new WorkItem2();
				workItem2.CopyFrom<WorkItem2, WorkItem3>(workItem3);

				//Note: StandAlone Item will going to have only one ItemAssociation record.
				if (workItem3.ItemAssociations != null && workItem3.ItemAssociations.Length > 0)
				{
					WorkItemAssociation itemAssociation = workItem3.ItemAssociations.FirstOrDefault();
					workItem2.AccountID = itemAssociation.AccountID;
					workItem2.Custodian = itemAssociation.Custodian;
					workItem2.CustodialAccNo = itemAssociation.CustodialAccountNumber;
					workItem2.FundingAccNo = itemAssociation.FundingAccountNumber;
				}
			}

			return workItem2;
		}

		#endregion
	}
}
