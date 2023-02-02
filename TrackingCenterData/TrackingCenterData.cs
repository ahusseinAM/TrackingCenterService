using System.Linq;
using System.Data.Linq;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Configuration;
using GSS.Entities;
using System;
using System.Collections.Generic;
using GSS.Entities.DB;

namespace GSS.TrackingCenterData
{
	internal static class ConnectionSetting
	{
		internal static string TrackingCenter = ConfigurationManager.ConnectionStrings["TRACKING_CENTER"].ToString();
	}

	public class TrackingCenterDataContext : DataContext
	{
		public TrackingCenterDataContext(string conn) :
			base(conn, XmlMappingSource.FromStream(Assembly.GetExecutingAssembly()
				.GetManifestResourceStream("GSS.TrackingCenterData.TrackingCenter.EntityMapping.xml")))
		{
			this.Log = Console.Out;
		}

		public TrackingCenterDataContext() : this(ConnectionSetting.TrackingCenter) { }

		public IQueryable<WorkItemType1> GetWorkItemTypes()
		{
			return this.CreateMethodCallQuery<WorkItemType1>(this, (MethodInfo)MethodBase.GetCurrentMethod());
		}

		public IQueryable<Item> GetWorkitemList()
		{
			return this.CreateMethodCallQuery<Item>(this, (MethodInfo)MethodBase.GetCurrentMethod());
		}

		public IQueryable<Item> GetWorkItemDetails(string externalID)
		{
			return this.CreateMethodCallQuery<Item>(this, (MethodInfo)MethodBase.GetCurrentMethod(), externalID);
		}

		public IQueryable<Item> GetBPMItemDetails(string externalID)
		{
			return this.CreateMethodCallQuery<Item>(this, (MethodInfo)MethodBase.GetCurrentMethod(), externalID);
		}

		public IQueryable<WorkItemNote> GetWorkItemNotes()
		{
			return this.CreateMethodCallQuery<WorkItemNote>(this, (MethodInfo)MethodBase.GetCurrentMethod());
		}

		public IQueryable<ProcessAlert> GetProcessAlerts()
		{
			return this.CreateMethodCallQuery<ProcessAlert>(this, (MethodInfo)MethodBase.GetCurrentMethod());
		}

		[Function(Name = "dbo.getDocumentStatus")]
		public IEnumerable<DocumentStatus> GetDocumentStatus(
			[Parameter(DbType = "NCHAR(10)")] string externalID,
			[Parameter(DbType = "INT")] int? bundleID)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, ((MethodInfo)MethodBase.GetCurrentMethod()), externalID, bundleID);
			return ((IEnumerable<DocumentStatus>)result.ReturnValue);
		}

		[Function(Name = "dbo.getRelatedItems")]
		public IEnumerable<Item> GetRelatedItems(
			[Parameter(DbType = "NCHAR(10)")] string externalID)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, ((MethodInfo)MethodBase.GetCurrentMethod()), externalID);
			return ((IEnumerable<Item>)result.ReturnValue);
		}

		[Function(Name = "dbo.getRelatedItemAssociations")]
		public IEnumerable<WorkItemAssociation> GetRelatedItemAssociations(
			[Parameter(DbType = "NCHAR(10)")] string externalID)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, ((MethodInfo)MethodBase.GetCurrentMethod()), externalID);
			return ((IEnumerable<WorkItemAssociation>)result.ReturnValue);
		}

		[Function(Name = "dbo.getRelatedItemNotes")]
		public IEnumerable<WorkItemNote> GetRelatedItemNotes(
			[Parameter(DbType = "NCHAR(10)")] string externalID)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, ((MethodInfo)MethodBase.GetCurrentMethod()), externalID);
			return ((IEnumerable<WorkItemNote>)result.ReturnValue);
		}

		public IQueryable<WorkItemAssociation> GetItemAssociations()
		{
			return this.CreateMethodCallQuery<WorkItemAssociation>(this, (MethodInfo)MethodBase.GetCurrentMethod());
		}

		public IQueryable<WorkItemAssociation> GetItemAssociationsByExternalId(string externalId)
		{
			return this.CreateMethodCallQuery<WorkItemAssociation>(this, (MethodInfo)MethodBase.GetCurrentMethod(), externalId);
		}

		[Function(Name = "dbo.CloseAgedOpenDNumber")]
		public ISingleResult<ClosedAgedOpenWorkItemResponse> CloseAgedOpenDNumber()
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod());
			return (ISingleResult<ClosedAgedOpenWorkItemResponse>)result.ReturnValue;
		}

		[Function(Name = "dbo.CloseAgedOpenNNumber")]
		public ISingleResult<ClosedAgedOpenWorkItemResponse> CloseAgedOpenNNumber()
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod());
			return (ISingleResult<ClosedAgedOpenWorkItemResponse>)result.ReturnValue;
		}
	}

	public class TrackingCenterUpdateDataContext : DataContext
	{
		public TrackingCenterUpdateDataContext(string conn) :
			base(conn)
		{
			this.Log = Console.Out;
		}

		public TrackingCenterUpdateDataContext() : this(ConnectionSetting.TrackingCenter) { }

		/*-- TODO: Return type is given is Int that need to changed --*/
		[Function(Name = "dbo.AddItemNote")]
		public int AddWorkItemNote(
			[Parameter(DbType = "NChar(10)")] string externalID,
			[Parameter(DbType = "NVarChar(50)")] string userID,
			[Parameter(DbType = "NVarChar(176)")] string userName,
			[Parameter(DbType = "NVarchar(MAX)")] string noteText,
			[Parameter(DbType = "NVarchar(50)")] string source,
			[Parameter(DbType = "TinyInt")] int displayMode,
			[Parameter(DbType = "TinyInt")] int noteFormat,
			[Parameter(DbType = "TinyInt")] int userType,
			[Parameter(DbType = "Bit")] bool advisorVisible)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)(MethodBase.GetCurrentMethod()),
																	externalID,
																	userID,
																	userName,
																	noteText,
																	source,
																	displayMode,
																	noteFormat,
																	userType,
																	advisorVisible);
			return ((int)result.ReturnValue);
		}

		/// <summary>
		/// Create/Update workitem
		/// ExternalId will be returned from Stored procedure
		/// </summary>
		/// <param name="oldExternalID"></param>
		/// <param name="externalID"></param>
		/// <param name="BDID"></param>
		/// <param name="advisorID"></param>
		/// <param name="agentID"></param>
		/// <param name="clientID"></param>
		/// <param name="objName"></param>
		/// <param name="description"></param>
		/// <param name="status"></param>
		/// <param name="internalStatus"></param>
		/// <param name="createdBy"></param>
		/// <param name="advisorVisible"></param>
		/// <param name="responseAllowed"></param>
		/// <param name="responseRequired"></param>
		/// <param name="isClosable"></param>
		/// <param name="sourceID"></param>
		/// <param name="createdDate"></param>
		/// <param name="closedDate"></param>
		/// <param name="bundleID"></param>
		/// <param name="shortExternalDescription"></param>
		/// <param name="rankOrder"></param>
		/// <param name="isParent"></param>
		/// <param name="parentExternalID"></param>
		/// <param name="ssoGuidId"></param>
		/// <param name="submitToBD"></param>
		/// <returns></returns>
		[Function(Name = "dbo.UpdateItem")]
		public int UpdateItem(
			[Parameter(DbType = "NChar(10)")] string oldExternalID,
			[Parameter(DbType = "NChar(10)")] ref string externalID,
			[Parameter(DbType = "NVarChar(6)")] string BDID,
			[Parameter(DbType = "NVarChar(6)")] string advisorID,
			[Parameter(DbType = "NVarChar(6)")] string agentID,
			[Parameter(DbType = "NVarChar(6)")] string clientID,
			[Parameter(DbType = "NVarChar(176)")] string objName,
			[Parameter(DbType = "NVarChar(150)")] string description,
			[Parameter(DbType = "NVarChar(80)")] string status,
			[Parameter(DbType = "NVarChar(80)")] string internalStatus,
			[Parameter(DbType = "NVarChar(255)")] string createdBy,
			[Parameter(DbType = "Bit")] bool advisorVisible,
			[Parameter(DbType = "Bit")] bool responseAllowed,
			[Parameter(DbType = "Bit")] bool responseRequired,
			[Parameter(DbType = "Bit")] bool isClosable,
			[Parameter(DbType = "NVarChar(1)")] string sourceID,
			[Parameter(DbType = "DateTime")] DateTime? createdDate,
			[Parameter(DbType = "DateTime")] DateTime? closedDate,
			[Parameter(DbType = "INT")] int? bundleID,
			[Parameter(DbType = "NVarChar(160)")] string shortExternalDescription,
			[Parameter(DbType = "INT")] int? rankOrder,
			[Parameter(DbType = "Bit")] bool isParent,
			[Parameter(DbType = "Nchar(10)")] string parentExternalID,
			[Parameter(DbType = "Bit")] bool isReopen,
			[Parameter(DbType = "Bit")] bool? isEsignatureRequested,
			[Parameter(DbType = "NVarChar(50)")] string ssoGuidId,
			[Parameter(DbType = "Bit")] bool? submitToBD
			)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(),
																	oldExternalID,
																	externalID,
																	BDID,
																	advisorID,
																	agentID,
																	clientID,
																	objName,
																	description,
																	status,
																	internalStatus,
																	createdBy,
																	advisorVisible,
																	responseAllowed,
																	responseRequired,
																	isClosable,
																	sourceID,
																	createdDate,
																	closedDate,
																	bundleID,
																	shortExternalDescription,
																	rankOrder,
																	isParent,
																	parentExternalID,
																	isReopen,
																	isEsignatureRequested,
																	ssoGuidId,
																	submitToBD);

			externalID = (string)result.GetParameterValue(1);

			return (int)result.ReturnValue;
		}

		[Function(Name = "dbo.AddAlertQueueItem")]
		public int AddAlertQueueItem(
			[Parameter(DbType = "NChar(10)")] string externalID,
			[Parameter(DbType = "NVarchar(80)")] string subject,
			[Parameter(DbType = "NVarChar(200)")] string message,
			[Parameter(Name = "alertTypeEV", DbType = "INT")] int alertType,
			[Parameter(DbType = "NVarChar(200)")] string documentID)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(), externalID, subject, message, alertType, documentID);
			return (int)result.ReturnValue;
		}

		[Function(Name = "dbo.AddEnvelope")]
		public int AddEnvelope(
			[Parameter(DbType = "UNIQUEIDENTIFIER")] Guid? docuSignID)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(), docuSignID);
			return (int)result.ReturnValue;
		}

		[Function(Name = "dbo.UpdateEnvelope")]
		public int UpdateEnvelope(
			[Parameter(DbType = "INT")] int envelopeID,
			[Parameter(DbType = "UNIQUEIDENTIFIER")] Guid docuSignID,
			[Parameter(DbType = "VARCHAR(20)")] string Type,
			[Parameter(DbType = "VARCHAR(20)")] string kitName,
			[Parameter(DbType = "INT")] EnvelopeType? envelopeType,
			[Parameter(DbType = "INT")] EsigStatus? esigStatus)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(),
																	envelopeID,
																	docuSignID,
																	Type,
																	kitName,
																	envelopeType,
																	esigStatus);
			return ((int)result.ReturnValue);
		}

		[Function(Name = "dbo.AddItemUpload")]
		public int AddItemUpload(
			[Parameter(DbType = "NChar(10)")] string externalID,
			[Parameter(DbType = "NVarchar(50)")] string fileName,
			[Parameter(DbType = "NVarChar(50)")] string createdBy)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(), externalID, fileName, createdBy);
			return (int)result.ReturnValue;
		}

		[Function(Name = "dbo.UpdateItemUpload")]
		public int UpdateItemUpload(
			[Parameter(DbType = "INT")] int uploadID,
			[Parameter(DbType = "NVarchar(50)")] string fileName,
			[Parameter(DbType = "NVarChar(50)")] string status
)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(), uploadID, fileName, status);
			return (int)result.ReturnValue;
		}

		[Function(Name = "dbo.UpdateNoteAdvisorVisible")]
		public int UpdateNoteAdvisorVisible(
			[Parameter(DbType = "NChar(10)")] string externalID,
			[Parameter(DbType = "NVarChar(20)")] string userID,
			[Parameter(DbType = "Int")] int noteID,
			[Parameter(DbType = "Bit")] bool advisorVisible)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(), externalID, userID, noteID, advisorVisible);
			return (int)result.ReturnValue;
		}

		[Function(Name = "dbo.UpdateAlertQueue")]
		[return: Parameter(DbType = "Int")]
		public int UpdateAlertQueue(
			[Parameter(DbType = "Int")] int queueId,
			[Parameter(DbType = "Bit")] bool processed,
			[Parameter(DbType = "NVARCHAR(4000)")] string statusDescription)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(), queueId, processed, statusDescription);
			return (int)result.ReturnValue;
		}

		[Function(Name = "dbo.AddDocumentAction")]
		[return: Parameter(DbType = "Int")]
		public int AddDocumentAction(
			[Parameter(DbType = "NVARCHAR(40)")] string doc_id,
			[Parameter(DbType = "NCHAR(10)")] string externalID,
			[Parameter(DbType = "Int")] int? processID,
			[Parameter(DbType = "Int")] int? applicationID,
			[Parameter(DbType = "Int")] int actionType,
			[Parameter(DbType = "NVARCHAR(50)")] string userID,
			[Parameter(DbType = "NVARCHAR(MAX)")] string actionDescription)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(),
																doc_id,
																externalID,
																processID,
																applicationID,
																actionType,
																userID,
																actionDescription);
			return (int)result.ReturnValue;
		}

		[Function(Name = "dbo.updateItemToBundle")]
		public int UpdateItemToBundle(
			[Parameter(DbType = "NChar(10)")] string oldExternalID,
			[Parameter(DbType = "NChar(10)")] string externalID,
			[Parameter(DbType = "NVarChar(6)")] string BDID,
			[Parameter(DbType = "NVarChar(6)")] string advisorID,
			[Parameter(DbType = "NVarChar(6)")] string agentID,
			[Parameter(DbType = "NVarChar(6)")] string clientID,
			[Parameter(DbType = "NVarChar(176)")] string objName,
			[Parameter(DbType = "NVarChar(150)")] string description,
			[Parameter(DbType = "NVarChar(80)")] string status,
			[Parameter(DbType = "NVarChar(80)")] string internalStatus,
			[Parameter(DbType = "NVarChar(255)")] string createdBy,
			[Parameter(DbType = "Bit")] bool advisorVisible,
			[Parameter(DbType = "Bit")] bool responseAllowed,
			[Parameter(DbType = "Bit")] bool responseRequired,
			[Parameter(DbType = "Bit")] bool isClosable,
			[Parameter(DbType = "NVarChar(1)")] string sourceID,
			[Parameter(DbType = "DateTime")] DateTime? createdDate,
			[Parameter(DbType = "DateTime")] DateTime? closedDate,
			[Parameter(DbType = "INT")] int? bundleID,
			[Parameter(DbType = "NVarChar(160)")] string shortExternalDescription,
			[Parameter(DbType = "Nchar(10)")] string parentExternalID,
			[Parameter(DbType = "INT")] int? rankOrder
			)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(),
																		oldExternalID,
																		externalID,
																		BDID,
																		advisorID,
																		agentID,
																		clientID,
																		objName,
																		description,
																		status,
																		internalStatus,
																		createdBy,
																		advisorVisible,
																		responseAllowed,
																		responseRequired,
																		isClosable,
																		sourceID,
																		createdDate,
																		closedDate,
																		bundleID,
																		shortExternalDescription,
																		parentExternalID,
																		rankOrder);
			return ((int)result.ReturnValue);
		}

		[Function(Name = "dbo.updateItemAssociation")]
		public int UpdateItemAssociation(
			[Parameter(DbType = "NCHAR(10)")] string externalID,
			[Parameter(DbType = "INT")] int itemAssociationID,
			[Parameter(DbType = "VARCHAR(10)")] string accountID,
			[Parameter(DbType = "NVARCHAR(6)")] string portfolioID,
			[Parameter(DbType = "NVARCHAR(20)")] string fundingAccountNumber,
			[Parameter(DbType = "NVARCHAR(25)")] string custodialAccountNumber,
			[Parameter(DbType = "NVARCHAR(6)")] string custodian,
			[Parameter(DbType = "NVARCHAR(176)")] string itemName,
			[Parameter(DbType = "INT")] int? itemStatus,
			[Parameter(DbType = "BIT")] bool isActive)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(),
															externalID,
															itemAssociationID,
															accountID,
															portfolioID,
															fundingAccountNumber,
															custodialAccountNumber,
															custodian,
															itemName,
															itemStatus,
															isActive);
			return ((int)result.ReturnValue);
		}

		[Function(Name = "dbo.UpdateItemAssociationName")]
		public int UpdateItemAssociationName(
			[Parameter(DbType = "NCHAR(10)")] string parentExternalID,
			[Parameter(DbType = "NVARCHAR(25)")] string custodialAccountNumber,
			[Parameter(DbType = "NVARCHAR(176)")] string itemName)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(), parentExternalID, custodialAccountNumber, itemName);
			return ((int)result.ReturnValue);
		}

		[Function(Name = "dbo.AddItemToBundle")]
		public int AddItemToBundle(
			[Parameter(DbType = "NCHAR(10)")] string externalID,
			[Parameter(DbType = "NCHAR(10)")] string parentExternalID,
			[Parameter(DbType = "NVARCHAR(160)")] string shortExternalDescription,
			[Parameter(DbType = "INT")] int rankOrder)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(), externalID, parentExternalID, shortExternalDescription, rankOrder);
			return ((int)result.ReturnValue);
		}

		[Function(Name = "dbo.UpdateNoteInternalVisible")]
		public int UpdateHideNoteFlag(
			[Parameter(DbType = "NCHAR(10)")] string externalId,
			[Parameter(DbType = "INT")] int noteId,
			[Parameter(DbType = "BIT")] bool internalVisible,
			[Parameter(DbType = "NVARCHAR(50)")] string userId)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(), externalId, noteId, internalVisible, userId);
			return ((int)result.ReturnValue);
		}

		[Function(Name = "dbo.DeleteEnvelope")]
		public int DeleteEnvelope([Parameter(DbType = "INT")] int envelopeId)
		{
			IExecuteResult result = this.ExecuteMethodCall(this, (MethodInfo)MethodBase.GetCurrentMethod(), envelopeId);
			return (int)result.ReturnValue;
		}
	}
}
