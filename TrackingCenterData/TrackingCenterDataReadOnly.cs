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
	internal static class ReadOnlyConnectionSetting
	{
		internal static string TrackingCenter = ConfigurationManager.ConnectionStrings["TRACKING_CENTER_READONLY"].ToString();
	}

	public class TrackingCenterDataReadOnly : DataContext
	{
		public TrackingCenterDataReadOnly(string conn) :
			base(conn, XmlMappingSource.FromStream(Assembly.GetExecutingAssembly()
				.GetManifestResourceStream("GSS.TrackingCenterData.TrackingCenter.EntityMapping.xml")))
		{
			this.Log = Console.Out;
		}

		public TrackingCenterDataReadOnly() : this(ReadOnlyConnectionSetting.TrackingCenter) { }

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
}
