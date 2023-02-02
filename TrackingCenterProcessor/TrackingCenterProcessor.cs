namespace GSS.TrackingCenterProcessor
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using System.Web;
	using Common.Logging;
	using Entities;
	using Entities.DB;
	using Ewm.DocumentStore.Client;
	using GSS.TrackingCenterProcessor.Resources;
	using GSS.Utility;
	using TrackingCenterData;
	using Utility;
	using DocStore = Ewm.DocumentStore.Common;

	public class TrackingCenterProcessor : IDisposable
	{
		private const string DSCONNECTION_SERVICE = "PROXY.DSCONNECTService";
		private const int INVALID_ITEM_UPLOAD_ID = 0;
		private static readonly ILog log = LogManager.GetCurrentClassLogger();
		protected const string PDF = ".PDF";
		protected const string FORM_VERSION = "FORM_VERSION";
		protected const string UNKNOWN = "UNKNOWN";
		private const string NITEM_WORKITEM_NOTE = "Once the New Account Paperwork is signed and fully approved, submit the documents via Tracking Center upload or mail.";
		private IEnumerable<string> FileExtensions = null;
		private static readonly string[] dateFormats = { "yyyy/MM/dd", "yyyy/M/d", "M/d/yyyy", "MM/dd/yyyy" };
		private static readonly CultureInfo usaCultureInfo = CultureInfo.CreateSpecificCulture("en-US");
		private static readonly char SEPARATOR = '|';
		private const string DATETIMEFORMAT = "MM/dd/yyyy HH:mm:ss fff";

		public TrackingCenterProcessor() : base()
		{
			FileExtensions = Util.FileExtensions;
		}

		public WorkItemType1[] GetWorkItemTypes(string userID, string[] agentID, string clientID, string filter, DateTime? fromDate, DateTime? toDate)
		{
			if ((agentID == null || agentID.Count() == 0) && string.IsNullOrWhiteSpace(clientID))
			{
				log.Warn($"TrackingCenterProcessor.GetWorkItemTypes - Criteria missing. There should be at least one criteria needs to be provided to get item count. userID:{userID}");
				return new WorkItemType1[0];
			}

			string agentIds = agentID != null && agentID.Any() ? string.Join(",", agentID) : string.Empty;
			Expression<Func<WorkItemType1, bool>> predicate = PredicateBuilder.True<WorkItemType1>();
			Expression<Func<WorkItemType1, bool>> typeFilter = PredicateBuilder.False<WorkItemType1>();

			if (agentID != null && agentID.Length > 0)
			{
				predicate = predicate.And(ExpressionBuilder.LINQExpression<WorkItemType1>(agentID, WorkItemFilters.AgentId.ToString()));
			}

			if (!string.IsNullOrWhiteSpace(clientID))
			{
				predicate = predicate.And(wit => wit.ClientID == clientID);
			}

			if (fromDate != null)
			{
				predicate = predicate.And(wit => wit.CreatedDate.Date >= fromDate);
			}

			if (toDate != null)
			{
				predicate = predicate.And(wit => wit.CreatedDate.Date <= toDate);
			}

			predicate = predicate.And(wit => wit.AdvisorVisible == true);

			using (var tcdc = new TrackingCenterDataContext())
			{
				if (!string.IsNullOrWhiteSpace(filter))
				{
					//Split filter types.
					var andFilters = filter.Split(new string[] { "&&" }, StringSplitOptions.None).ToList();
					bool isTypeFilter = false;
					var reg = new Regex("\".*?\"");

					foreach (string f in andFilters)
					{
						string tempForNewFilter = string.Empty;
						Expression<Func<Item, bool>> filters = null;
						var orFilters = f.Split(new string[] { "||" }, StringSplitOptions.None).ToList();

						foreach (string i in orFilters)
						{
							if (i.Contains("WebStatus"))
							{
								string theFilter = i.Replace("(", "").Replace(")", "");
								var matches = reg.Matches(theFilter);

								foreach (object item in matches)
								{
									string filterstring = item.ToString().Replace("\"", "");

									if (filterstring == "closed<7")
									{
										typeFilter = typeFilter.Or(wit => wit.CloseDate >= DateTime.Now.AddDays(-7));
										isTypeFilter = true;
									}
									else if (filterstring == "open-Advisor")
									{
										typeFilter = typeFilter.Or(wit => wit.CloseDate == null && wit.ResponseRequired == true);
										isTypeFilter = true;
									}
									else if (filterstring == "open")
									{
										typeFilter = typeFilter.Or(wit => wit.CloseDate == null);
										isTypeFilter = true;
									}
									else if (filterstring == "closed")
									{
										typeFilter = typeFilter.Or(wit => wit.CloseDate != null);
										isTypeFilter = true;
									}
								}
							}
							else if (i.ToLower().Contains("itemtype"))
							{
								string theFilter = i.Replace("(", "").Replace(")", "");
								var matches = reg.Matches(theFilter);

								foreach (object item in matches)
								{
									string filterstring = item.ToString().Replace("\"", "");
									typeFilter = typeFilter.And(wit => wit.ItemType == filterstring);
									isTypeFilter = true;
								}
							}
							else if (i.ToLower().Contains("clientid") && string.IsNullOrWhiteSpace(clientID))
							{
								string theFilter = i.Replace("(", "").Replace(")", "");
								var matches = reg.Matches(theFilter);

								foreach (object item in matches)
								{
									string filterstring = item.ToString().Replace("\"", "");
									typeFilter = typeFilter.And(wit => wit.ClientID == filterstring);
									isTypeFilter = true;
								}
							}
						}
					}

					if (isTypeFilter)
					{
						predicate = predicate.And(typeFilter);
					}
				}

				var workItemTypes = tcdc.GetWorkItemTypes().Where(predicate);
				workItemTypes = this.ApplyFilters<WorkItemType1>(null, new string[] { })(workItemTypes);

				if (null != workItemTypes)
				{
					WorkItemType1[] workItemsTypes = workItemTypes.ToArray();

					foreach (WorkItemType1 workItemType in workItemsTypes)
					{
						this.PopulateWebStatus(workItemType);
					}

					return workItemsTypes;
				}
				else
				{
					log.Warn($"TrackingCenterProcessor.GetWorkItemTypes: Record not found for provided criteria. userID:{userID} agentIds:{agentIds} clientID:{clientID} filter:{filter} fromDate:{fromDate} toDate:{toDate}");

					return new WorkItemType1[0];
				}
			}
		}

		public WorkItem3[] GetWorkItemList(string userID, string BDID, string advisorID, string[] agentID, string clientID, string accountID, string type, string status, DateTime? fromDate, DateTime? toDate, string source)
		{
			if (string.IsNullOrWhiteSpace(BDID)
					&& string.IsNullOrWhiteSpace(advisorID)
					&& (agentID == null || agentID.Count() == 0)
					&& string.IsNullOrWhiteSpace(clientID)
					&& string.IsNullOrWhiteSpace(accountID))
			{
				throw new ArgumentException("TrackingCenterProcessor.GetWorkitemList - Criteria missing. There should be at least one criteria for GetWorkitemList.");
			}

			IList<WorkItem3> workItems = new List<WorkItem3>();
			Expression<Func<Item, bool>> predicate = PredicateBuilder.True<Item>();
			string agentIds = string.Empty;

			if (!string.IsNullOrWhiteSpace(BDID))
			{
				predicate = predicate.And(wil => wil.BDID == BDID);
			}

			if (!string.IsNullOrWhiteSpace(advisorID))
			{
				predicate = predicate.And(wil => wil.AdvisorID == advisorID);
			}

			if (agentID != null && agentID.Length > 0)
			{
				agentIds = agentID != null && agentID.Any() ? string.Join(",", agentID) : string.Empty;
				predicate = predicate.And(ExpressionBuilder.LINQExpression<Item>(agentID, WorkItemFilters.AgentId.ToString()));
			}

			if (!string.IsNullOrWhiteSpace(clientID))
			{
				predicate = predicate.And(wil => wil.ClientID == clientID);
			}

			if (!string.IsNullOrWhiteSpace(accountID))
			{
				predicate = predicate.And(wil => wil.AccountID == accountID);
			}

			if (!string.IsNullOrWhiteSpace(type))
			{
				predicate = predicate.And(wil => wil.Description == type);
			}

			if (!string.IsNullOrWhiteSpace(status))
			{
				predicate = predicate.And(wil => wil.Status == status);
			}

			if (fromDate != null)
			{
				predicate = predicate.And(wil => wil.CreatedDate >= fromDate);
			}

			if (toDate != null)
			{
				predicate = predicate.And(wil => wil.CreatedDate <= toDate);
			}

			if (WorkItemSource.PI.ToString().Equals(source, StringComparison.OrdinalIgnoreCase))
			{
				predicate = predicate.And(wil => wil.IsParent == true);
				predicate = predicate.And(wil => wil.ClosedDate == null);
			}

			if (WorkItemSource.BPM.ToString().Equals(source, StringComparison.OrdinalIgnoreCase))
			{
				predicate = predicate.And(wil => wil.ExternalId.StartsWith("B"));
			}

			predicate = predicate.And(ia => ia.ItemAssociationActive == true);

			using (var tcdc = new TrackingCenterDataReadOnly())
			{
				var iqWILs = tcdc.GetWorkitemList()
									.Where(predicate)
									.Select(i => new Item
									{
										AgentID = i.AgentID,
										ClientID = i.ClientID,
										ClosedDate = i.ClosedDate,
										CreatedBy = i.CreatedBy,
										CreatedDate = i.CreatedDate,
										Description = i.Description,
										ExternalId = i.ExternalId,
										Id = i.Id,
										InternalStatus = i.InternalStatus,
										IsParent = i.IsParent,
										ObjectName = i.ObjectName,
										Status = i.Status,
										WorkItemType = i.WorkItemType,
										AccountID = i.AccountID,
										CustodialAccountNumber = i.CustodialAccountNumber,
										ItemName = i.ItemName,
										Custodian = i.Custodian,
										ItemAssociationActive = i.ItemAssociationActive
									});

				if (iqWILs != null)
				{
					/* New implementation based on new search requirement for ENAX project.
					 * Search result should always return StandAlone and Bundle (i.e., Parent) items.
					 * If search is based on Component (i.e., Child) item value then function should always get associated Parent (i.e.) Bundle item details.
					 */
					IList<WorkItemAssociation> itemAssociations = null;
					IList<Item> tempItems = iqWILs.ToList();
					tempItems.OrderBy(x => x.Id);

					if (tempItems != null && tempItems.Any())
					{
						foreach (var iqItem in tempItems)
						{
							if (iqItem != null && iqItem.Id > 0)
							{
								var workItem = new WorkItem3();
								var item = new Item();
								itemAssociations = new List<WorkItemAssociation>();

								if (!workItems.Any(x => x.Id == iqItem.Id))
								{
									item = iqItem;

									if (item != null && item.Id > 0)
									{
										workItem.CopyFrom(item);
										workItem.CreatedBy = HttpUtility.HtmlEncode(workItem.CreatedBy);

										workItem.ItemAssociations = tempItems.Where(it => it.Id == item.Id).Select(ia => new WorkItemAssociation
										{
											AccountID = ia.AccountID,
											CustodialAccountNumber = ia.CustodialAccountNumber,
											ItemName = ia.ItemName,
											Custodian = ia.Custodian,
											IsActive = ia.ItemAssociationActive ?? true
										}).ToArray();

										if (WorkItemSource.BPM.ToString().Equals(source, StringComparison.OrdinalIgnoreCase))
										{
											workItem.ExternalId = Verhoeff.ParseBPMID(workItem.ExternalId);
											workItem.CreatedDate = workItem.CreatedDate.Value.ToUniversalTime();
											workItem.ModifiedDate = workItem.ModifiedDate.HasValue ? item.ModifiedDate.Value.ToUniversalTime() : item.ModifiedDate;
											workItem.ClosedDate = workItem.ClosedDate.HasValue ? item.ClosedDate.Value.ToUniversalTime() : item.ClosedDate;
										}

										workItems.Add(workItem);
									}
								}
							}
						}
					}
					else
					{
						log.Warn($"MethodName:GetWorkitemList userID:{userID} BDID:{BDID} advisorID:{advisorID} agentIds:{agentIds} clientID:{clientID} accountID:{accountID} type:{type} status:{status} fromDate:{fromDate} toDate:{toDate} source:{source}");
						return Array.Empty<WorkItem3>();
					}

					tempItems = null;
				}
				else
				{
					throw new ArgumentException(string.Format("Data request cannot be constructed."));
				}
			}

			return workItems.ToArray();
		}

		public WorkItemsResponse3 GetWorkitemListWeb(string userID, string[] agentID, string clientID, string accountID, string filter, string[] sortOrder, DateTime? fromDate, DateTime? toDate, int start, int count, bool computeCount)
		{
			if ((agentID == null || agentID.Count() == 0)
				&& string.IsNullOrWhiteSpace(clientID)
				&& string.IsNullOrWhiteSpace(accountID))
			{
				throw new ArgumentException("TrackingCenterProcessor.GetWorkitemListWeb - Criteria missing. There should be at least one criteria for GetWorkitemListWeb.");
			}

			var response = new WorkItemsResponse3();
			string externalId = string.Empty;
			IQueryable<Item> iqWILs = null;
			var workItem3 = new ConcurrentBag<WorkItem3>();
			var itemList = new ConcurrentBag<Item>();
			Expression<Func<Item, bool>> predicate = PredicateBuilder.True<Item>();
			IList<Item> tempItemList = null;

			if (agentID != null && agentID.Length > 0)
			{
				predicate = predicate.And(ExpressionBuilder.LINQExpression<Item>(agentID, WorkItemFilters.AgentId.ToString()));
			}

			if (!string.IsNullOrWhiteSpace(clientID))
			{
				predicate = predicate.And(wil => wil.ClientID == clientID);
			}

			if (!string.IsNullOrWhiteSpace(accountID))
			{
				predicate = predicate.And(wil => wil.AccountID == accountID);
			}

			if (fromDate != null)
			{
				predicate = predicate.And(wil => wil.CreatedDate.Value.Date >= fromDate);
			}

			if (toDate != null)
			{
				predicate = predicate.And(wil => wil.CreatedDate.Value.Date <= toDate);
			}

			predicate = predicate.And(wil => wil.AdvisorVisible == true);

			//See the search string is an externalid or item number
			if (!string.IsNullOrEmpty(filter) && filter.Contains("ExternalID"))
			{
				string exfilter = filter.Split('|').FirstOrDefault(s => s.Contains("ExternalID"));

				if (exfilter != null)
				{
					externalId = Regex.Replace(exfilter.Substring(exfilter.IndexOf("ExternalID")), @"\s|ExternalID|==|\(|\)|""", string.Empty);
				}

				if (!string.IsNullOrWhiteSpace(externalId))
				{
					int ItemId;
					int.TryParse(externalId, out ItemId);

					bool validExternalID = Regex.IsMatch(string.IsNullOrWhiteSpace(externalId) ? string.Empty : externalId, ConfigurationManager.AppSettings.Get("ExternalIdRegex"));

					if (!validExternalID)
					{
						externalId = string.Empty;
					}
				}
			}

			using (var tcdc = new TrackingCenterDataReadOnly())
			{
				//Used to hold filters that map to column.
				string newFilter = string.Empty;

				//Run historical search if search string is a valid StandAlone/Parent/Child ExternalId.
				if (Verhoeff.ValidateID(externalId))
				{
					var item = tcdc.GetWorkItemDetails(externalId)
									.Where(predicate)
									.Select(i => new Item
									{
										AdvisorID = i.AdvisorID,
										AdvisorVisible = i.AdvisorVisible,
										AgentID = i.AgentID,
										BDID = i.BDID,
										BundleID = i.BundleID,
										ClientID = i.ClientID,
										ClosedDate = i.ClosedDate,
										CreatedBy = i.CreatedBy,
										CreatedDate = i.CreatedDate,
										Description = i.Description,
										DocuSignID = i.DocuSignID,
										EnvelopeType = i.EnvelopeType,
										EsigStatus = i.EsigStatus,
										eSigType = i.eSigType,
										ExternalId = i.ExternalId,
										Id = i.Id,
										InternalStatus = i.InternalStatus,
										IsEsignatureRequested = i.IsEsignatureRequested,
										IsParent = i.IsParent,
										KitName = i.KitName,
										ModifiedDate = i.ModifiedDate,
										ObjectName = i.ObjectName,
										OldExternalID = i.OldExternalID,
										ParentExternalID = i.ParentExternalID,
										ParentItemID = i.ParentItemID,
										RankOrder = i.RankOrder,
										ResponseAllowed = i.ResponseAllowed,
										ResponseRequired = i.ResponseRequired,
										ShortExternalDescription = i.ShortExternalDescription,
										SsoGuidId = i.SsoGuidId,
										Status = i.Status,
										SubmitToBD = i.SubmitToBD,
										UploadAllowed = i.UploadAllowed,
										WorkItemType = i.WorkItemType
									}).FirstOrDefault();

					if (item != null)
					{
						if (item.ParentItemID.HasValue && item.ParentItemID.Value > 0)
						{
							var items = tcdc.GetRelatedItems(externalId).ToList(); //Get ParentItem based on ChildItem ExternalId.

							if (items != null && items.Any())
							{
								item = items.Where(i => i.IsParent).FirstOrDefault();
							}
						}

						var workItem = new WorkItem3();
						workItem = workItem.CopyFrom(item);
						workItem.ItemAssociations = tcdc.GetItemAssociationsByExternalId(item.ExternalId).ToArray();
						workItem3.Add(workItem); //Return StandAloneItem (D#,N#,B#)  or ParentItem (B#) for further processing.
					}
				}
				else
				{
					//Filter may contain WebStatus and/or ExternalID, so we need to parse the filter and rebuild it without either.
					//!!! NOTE:Taking the long approach to ensure matching '(' ')'. 
					//Don't want to just remove WebStatus then have to try to fix matching parenthesis.
					//Can have multiple instances of WebStatus. !!! 
					if (!string.IsNullOrEmpty(filter))
					{
						var reg = new Regex("\".*?\"");

						//Split filter types.
						var andFilters = filter.Split(new string[] { "&&" }, StringSplitOptions.None).ToList();

						foreach (string f in andFilters)
						{
							string tempForNewFilter = string.Empty;
							Expression<Func<Item, bool>> filters = null;
							var orFilters = f.Split(new string[] { "||" }, StringSplitOptions.None).ToList();

							foreach (string i in orFilters)
							{
								if (i.Contains("WebStatus"))
								{
									string theFilter = i.Replace("(", "").Replace(")", "");
									var matches = reg.Matches(theFilter);

									foreach (object item in matches)
									{
										string filterstring = item.ToString().Replace("\"", "");

										if (filterstring == "closed<7")
										{
											filters = this.UseOrInitOrExpression(filters, (wit => wit.ClosedDate >= DateTime.Now.AddDays(-7)));
										}
										else if (filterstring == "open-Advisor")
										{
											filters = this.UseOrInitOrExpression(filters, (wit => wit.ClosedDate == null && wit.ResponseRequired == true));
										}
										else if (filterstring == "open")
										{
											filters = this.UseOrInitOrExpression(filters, (wit => wit.ClosedDate == null));
										}
										else if (filterstring == "closed")
										{
											filters = this.UseOrInitOrExpression(filters, (wit => wit.ClosedDate != null));
										}
									}
								}
								else if (i.ToLower().Contains("clientid"))
								{
									string theFilter = i.Replace("(", "").Replace(")", "");
									var matches = reg.Matches(theFilter);

									foreach (object item in matches)
									{
										string filterstring = item.ToString().Replace("\"", "");
										filters = this.UseOrInitOrExpression(filters, (wil => wil.ClientID == filterstring));
									}
								}
								else if (i.ToLower().Contains("isparent"))
								{
									string theFilter = i.Replace("(", "").Replace(")", "");
									var matches = reg.Matches(theFilter);

									foreach (object item in matches)
									{
										string filterstring = item.ToString().Replace("\"", "");
										filters = this.UseOrInitOrExpression(filters, (wil => wil.IsParent == bool.Parse(filterstring)));
									}
								}
								else if (i.ToLower().Contains("externalid") && Verhoeff.ValidateID(externalId))
								{
									//If ExternalID had been valid, this part of the code would not have run; so skip it.
									continue;
								}
								else
								{
									//Check with double quotes or single quotes in the like statement.
									Match likeMatch = Regex.Match(i, "like\\((.*?)[,'%](.*?)%'\\)|like\\((.*?)[,\"%](.*?)%\"\\)");
									string theFilter = string.Empty;

									if (likeMatch.Length > 0)
									{
										theFilter = likeMatch.Captures[0].ToString();
									}
									else
									{
										theFilter = i.Trim(new char[] { '(', ')', ' ' });
									}

									if (string.IsNullOrEmpty(tempForNewFilter))
									{
										tempForNewFilter = string.Concat("(", theFilter, ")");
									}
									else
									{
										if (!string.IsNullOrEmpty(theFilter))
										{
											tempForNewFilter = string.Concat(tempForNewFilter, "||", string.Concat("(", theFilter, ")"));
										}
									}
								}
							}

							if (filters != null)
							{
								predicate = predicate.And(filters);
							}

							if (string.IsNullOrEmpty(newFilter))
							{
								newFilter = tempForNewFilter;
							}
							else
							{
								if (!string.IsNullOrEmpty(tempForNewFilter))
								{
									newFilter = $"({newFilter})&&({tempForNewFilter})";
								}
							}
						}
					}

					predicate = predicate.And(wil => wil.ItemAssociationActive == true);

					iqWILs = tcdc.GetWorkitemList().Where(predicate)
													.Select(i => new Item
													{
														AdvisorID = i.AdvisorID,
														AdvisorVisible = i.AdvisorVisible,
														AgentID = i.AgentID,
														BDID = i.BDID,
														BundleID = i.BundleID,
														ClientID = i.ClientID,
														ClosedDate = i.ClosedDate,
														CreatedDate = i.CreatedDate,
														Description = i.Description,
														DocuSignID = i.DocuSignID,
														EnvelopeType = i.EnvelopeType,
														EsigStatus = i.EsigStatus,
														eSigType = i.eSigType,
														ExternalId = i.ExternalId,
														Id = i.Id,
														InternalStatus = i.InternalStatus,
														IsEsignatureRequested = i.IsEsignatureRequested,
														IsParent = i.IsParent,
														KitName = i.KitName,
														ModifiedDate = i.ModifiedDate,
														ObjectName = i.ObjectName,
														ParentExternalID = i.ParentExternalID,
														ParentItemID = i.ParentItemID,
														RankOrder = i.RankOrder,
														ResponseAllowed = i.ResponseAllowed,
														ResponseRequired = i.ResponseRequired,
														ShortExternalDescription = i.ShortExternalDescription,
														SsoGuidId = i.SsoGuidId,
														Status = i.Status,
														SubmitToBD = i.SubmitToBD,
														UploadAllowed = i.UploadAllowed,
														WorkItemType = i.WorkItemType,
														AccountID = i.AccountID,
														CustodialAccountNumber = i.CustodialAccountNumber,
														Custodian = i.Custodian,
														FundingAccountNumber = i.FundingAccountNumber,
														ItemAssociationActive = i.ItemAssociationActive,
														ItemAssociationID = i.ItemAssociationID,
														ItemName = i.ItemName
													});

					if (!string.IsNullOrEmpty(newFilter))
					{
						iqWILs = this.ApplyFilters<Item>(string.Concat("(", newFilter, ")"), null)(iqWILs);
					}

					var tempItems = iqWILs.ToList();

					if (tempItems != null && tempItems.Count > 0)
					{
						foreach (var item in tempItems)
						{
							/* 1 or more Component (i.e.,Child) items can be associated to same AccountID/AccountNumber and belongs to same     Parent, then we should not add Parent item twice.
							* In next sequential run if we received different Component item with same ParentItemID, then it should skip below implementation.
							*/

							bool isSkip = itemList.Any(x => x.Id == item.ParentItemID);

							if (!isSkip)
							{
								if (item.ParentItemID > 0)
								{
									if (tempItems.Any(x => x.Id == item.ParentItemID))
									{
										itemList.Add(tempItems.Where(x => x.Id == item.ParentItemID).FirstOrDefault());
									}
								}
								else
								{
									if (!itemList.Any(x => x.Id == item.Id))
									{
										itemList.Add(item);
									}
								}

								iqWILs = itemList.Count > 0 ? itemList.AsQueryable() : iqWILs;
							}
						}
					}
				}

				if (iqWILs != null)
				{
					tempItemList = iqWILs.ToList();
				}
			}

			if (tempItemList != null && tempItemList.Any())
			{
				foreach (var item in tempItemList)
				{
					var workItem = new WorkItem3
					{
						CreatedBy = HttpUtility.HtmlEncode(item.CreatedBy),
						ItemAssociations = Array.Empty<WorkItemAssociation>()
					};

					workItem.ItemAssociations = tempItemList.Where(it => it.Id == item.Id).Select(ia => new WorkItemAssociation
					{
						AccountID = ia.AccountID,
						CustodialAccountNumber = ia.CustodialAccountNumber,
						ItemName = ia.ItemName,
						Custodian = ia.Custodian,
						IsActive = ia.ItemAssociationActive ?? true
					}).ToArray();

					workItem3.Add(workItem.CopyFrom(item));
				}
			}

			int CountAll = 0;
			var workItems = this.ApplyFilters<WorkItem3>(null, sortOrder)(workItem3.AsQueryable());

			if (computeCount)
			{
				CountAll = workItems.Count();
			}

			//If no column ordering selected, use default ordering by status. 
			if (sortOrder == null || sortOrder.Count() > 1)
			{
				workItems = workItems.OrderBy(i => i.ClosedDate == null && i.ResponseRequired ? 1 : ((i.ClosedDate == null && i.ResponseRequired == false) ? 2 : (i.ClosedDate > DateTime.Now.AddDays(-7)) ? 3 : 4));
			}

			//Paging
			if (count > 0)
			{
				if (start > CountAll)
				{
					//Set to last possible page or go to 0.
					start = CountAll - count;

					if (start < 0)
					{
						start = 0;
					}
				}

				workItems = workItems.Skip(start);
				workItems = workItems.Take(count);
			}

			if (computeCount)
			{
				var aggValues = new Dictionary<string, string>
					{
						{ "Count_All", CountAll.ToString() }
					};
				response.AggregationValues = aggValues;
			}

			foreach (var item in workItems)
			{
				this.PopulateWebAndSortOrderByStatuses(item);
				this.PopulateUploadAllowed(item);
			}

			response.WorkItems = workItems.ToArray();
			return response;
		}

		public WorkItem3 GetWorkItemDetails(string processID, string sourceID)
		{
			Item item = null;
			var response = new WorkItem3();
			Expression<Func<WorkItemNote, bool>> predicate = PredicateBuilder.True<WorkItemNote>();

			if (WorkItemSource.TrackingCenter.ToString().Equals(sourceID, StringComparison.OrdinalIgnoreCase))
			{
				predicate = predicate.And(note => note.AdvisorVisible == true);
			}

			using (var tcdc = new TrackingCenterDataContext())
			{
				item = tcdc.GetWorkItemDetails(processID)
							.Select(i => new Item
							{
								AdvisorID = i.AdvisorID,
								AdvisorVisible = i.AdvisorVisible,
								AgentID = i.AgentID,
								BDID = i.BDID,
								BundleID = i.BundleID,
								ClientID = i.ClientID,
								ClosedDate = i.ClosedDate,
								CreatedBy = i.CreatedBy,
								CreatedDate = i.CreatedDate,
								Description = i.Description,
								DocuSignID = i.DocuSignID,
								EnvelopeType = i.EnvelopeType,
								EsigStatus = i.EsigStatus,
								eSigType = i.eSigType,
								ExternalId = i.ExternalId,
								Id = i.Id,
								InternalStatus = i.InternalStatus,
								IsEsignatureRequested = i.IsEsignatureRequested,
								IsParent = i.IsParent,
								KitName = i.KitName,
								ModifiedDate = i.ModifiedDate,
								ObjectName = i.ObjectName,
								OldExternalID = i.OldExternalID,
								ParentExternalID = i.ParentExternalID,
								ParentItemID = i.ParentItemID,
								RankOrder = i.RankOrder,
								ResponseAllowed = i.ResponseAllowed,
								ResponseRequired = i.ResponseRequired,
								ShortExternalDescription = i.ShortExternalDescription,
								SsoGuidId = i.SsoGuidId,
								Status = i.Status,
								SubmitToBD = i.SubmitToBD,
								UploadAllowed = i.UploadAllowed,
								WorkItemType = i.WorkItemType
							}).FirstOrDefault();

				if (item != null && item.Id > 0)
				{
					predicate = predicate.And(note => note.ItemID == item.Id);
					response.Notes = tcdc.GetWorkItemNotes().Where(predicate).OrderByDescending(wn => wn.CreatedDate).ToArray();
					response.ItemAssociations = tcdc.GetItemAssociations().Where(x => x.ItemID == item.Id).ToArray();
				}
			}

			if (item != null && item.Id > 0)
			{
				response.CopyFrom(item);

				if (response != null && response.Id > 0)
				{
					if (response.Notes != null && response.Notes.Length > 0)
					{
						response.CreatedBy = HttpUtility.HtmlEncode(response.CreatedBy);

						if (WorkItemSource.BPM.ToString().Equals(sourceID, StringComparison.OrdinalIgnoreCase))
						{
							response.CreatedDate = response.CreatedDate.Value.ToUniversalTime();
							response.ModifiedDate = response.ModifiedDate.HasValue ? response.ModifiedDate.Value.ToUniversalTime() : response.ModifiedDate;
							response.ClosedDate = response.ClosedDate.HasValue ? response.ClosedDate.Value.ToUniversalTime() : response.ClosedDate;

							Parallel.ForEach(response.Notes,
								note =>
								{
									note.NoteText = !string.IsNullOrWhiteSpace(note.NoteText) ? Convert.ToBase64String(Encoding.UTF8.GetBytes(HttpUtility.HtmlEncode(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(note.NoteText))))) : string.Empty;
									note.CreatedDate = note.CreatedDate.ToUniversalTime();
									note.LastInternalVisibleModifiedDate = note.LastInternalVisibleModifiedDate.HasValue ? note.LastInternalVisibleModifiedDate.Value.ToUniversalTime() : (DateTime?)null;
								}
								);
						}
						else
						{
							Parallel.ForEach(response.Notes,
								note =>
								{
									note.NoteText = !string.IsNullOrWhiteSpace(note.NoteText) ? HttpUtility.HtmlEncode(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(note.NoteText))) : string.Empty;
								});
						}
					}
				}

				this.PopulateWebAndSortOrderByStatuses(response);
				this.PopulateUploadAllowed(response);
			}
			else
			{
				throw new ArgumentException($"GetWorkItemDetails: Data request cannot be constructed. processID:{processID}");
			}

			return response;
		}

		public WorkItemsCountResponse GetOpenWorkItemCount(string userID, string[] agentID, string clientID)
		{
			if ((agentID == null || !agentID.Any()) && string.IsNullOrWhiteSpace(clientID))
			{
				log.Warn($"TrackingCenterProcessor.GetOpenWorkItemCount - Criteria missing. There should be at least one criteria needs to be provided to get item count. userID:{userID}");

				return new WorkItemsCountResponse
				{
					AdvisorResponseCount = 0,
					WorkItemsCount = 0
				};
			}

			var response = new WorkItemsCountResponse();
			Expression<Func<Item, bool>> predicate = PredicateBuilder.True<Item>();

			if (agentID != null && agentID.Length > 0)
			{
				predicate = predicate.And(ExpressionBuilder.LINQExpression<Item>(agentID, WorkItemFilters.AgentId.ToString()));
			}

			if (!string.IsNullOrEmpty(clientID))
			{
				predicate = predicate.And(owic => owic.ClientID == clientID);
			}

			predicate = predicate.And(st => st.ClosedDate == null);
			predicate = predicate.And(wil => wil.AdvisorVisible == true);
			predicate = predicate.And(wil => wil.ParentItemID == null);

			using (var tcdc = new TrackingCenterDataContext())
			{
				int itemCount = 0;
				int advisorResponseCount = 0;

				var items = tcdc.GetWorkitemList().Where(predicate);

				var dbResult = from i in items
							   group i by 1 into p
							   select new
							   {
								   itemCount = p.Count(),
								   advisorResponseCount = p.Sum(x => Convert.ToInt32(x.ResponseRequired))
							   };

				var result = dbResult.SingleOrDefault();

				if (result != null)
				{
					response.AdvisorResponseCount = result.advisorResponseCount;
					response.WorkItemsCount = result.itemCount;
				}
				else
				{
					response.AdvisorResponseCount = advisorResponseCount;
					response.WorkItemsCount = itemCount;
				}
			}

			return response;
		}

		public WorkItemNote[] GetWorkItemNotes(string processID, string userID, long noteID, string source)
		{
			if (string.IsNullOrWhiteSpace(processID))
			{
				throw new ArgumentNullException(nameof(processID));
			}

			var tcdc = new TrackingCenterDataContext();

			using (tcdc)
			{
				var notes = tcdc.GetWorkItemNotes().Where(win => win.WorkItemExternalID == processID && win.Id > noteID).ToArray();

				if (notes != null && notes.Any())
				{
					if (WorkItemSource.BPM.ToString().Equals(source, StringComparison.OrdinalIgnoreCase))
					{
						Parallel.ForEach(notes,
						note =>
						{
							note.NoteText = !string.IsNullOrWhiteSpace(note.NoteText) ? Convert.ToBase64String(Encoding.UTF8.GetBytes(HttpUtility.HtmlEncode(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(note.NoteText))))) : string.Empty;
							note.CreatedDate = note.CreatedDate.ToUniversalTime();
							note.LastInternalVisibleModifiedDate = note.LastInternalVisibleModifiedDate.HasValue ? note.LastInternalVisibleModifiedDate.Value.ToUniversalTime() : (DateTime?)null;
						});
					}
					else
					{
						Parallel.ForEach(notes,
						note =>
						{
							note.NoteText = !string.IsNullOrWhiteSpace(note.NoteText) ? HttpUtility.HtmlEncode(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(note.NoteText))) : string.Empty;
							note.CreatedDate = note.CreatedDate.ToUniversalTime();
							note.LastInternalVisibleModifiedDate = note.LastInternalVisibleModifiedDate.HasValue ? note.LastInternalVisibleModifiedDate.Value.ToUniversalTime() : (DateTime?)null;
						});
					}

					return notes.OrderByDescending(wn => wn.CreatedDate).ToArray();
				}
				else
				{
					return Array.Empty<WorkItemNote>();
				}
			}
		}

		/// <summary>
		/// Add new note to StandAlone, Parent or Child items in TrackingCenter. Method will NoteId for added note.
		/// </summary>
		/// <returns>NoteID</returns>
		public int AddWorkItemNote(string processID, string userID, string userName, int userType, string noteText, int displayMode, int noteFormat, bool advisorVisible, string source)
		{
			int response = 0;

			if (WorkItemSource.BPM.ToString().Equals(source, StringComparison.OrdinalIgnoreCase)
				&& (processID.StartsWith("B", StringComparison.OrdinalIgnoreCase) || processID.StartsWith("D", StringComparison.OrdinalIgnoreCase) || processID.StartsWith("N", StringComparison.OrdinalIgnoreCase)))
			{
				byte[] noteContent = Convert.FromBase64String(noteText);
				noteText = Encoding.UTF8.GetString(noteContent);
				noteText = HttpUtility.HtmlDecode(noteText);
			}
			else
			{
				noteText = HttpUtility.HtmlDecode(noteText);
			}

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				response = tcindc.AddWorkItemNote(processID,
												userID,
												userName,
												noteText,
												source,
												displayMode,
												noteFormat,
												userType,
												advisorVisible);

				if (response > 0)
				{
					string subject = "New external user note alert";
					int resultQ = 0;
					int alertMessageLength = 0;
					var alertMessageText = new StringBuilder();

					try
					{
						if (!WorkItemSource.BPM.ToString().Equals(source, StringComparison.OrdinalIgnoreCase)
							&& processID.StartsWith("B", StringComparison.OrdinalIgnoreCase))
						{

							System.Globalization.TextElementEnumerator enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(noteText);

							while (enumerator.MoveNext())
							{
								if (alertMessageLength <= 151)
								{
									alertMessageText.Append(enumerator.Current);
									alertMessageLength += enumerator.GetTextElement().Length;
								}
								else
								{
									break;
								}
							}

							resultQ = tcindc.AddAlertQueueItem(processID, subject, alertMessageText.ToString(), (int)AlertType.Note, null);

							if (resultQ < 0)
							{
								throw new ApplicationException(string.Format("AddWorkItemNote: Add Queue failed. processID:{0}", processID));
							}
						}
					}
					catch (Exception ex)
					{
						log.Error($"MethodName: AddWorkItemNote - Error occurred while adding alert on workitem:{processID} userId:{userID} userName:{userName} decodedNoteText:{noteText} alertMessageLength :{alertMessageLength} alertMessageText:{alertMessageText.ToString()}");
						throw;
					}
				}
				else
				{
					throw new ApplicationException(string.Format("AddWorkItemNote: Add work-item note failed. processID:{0} noteText:{1} userID:{2} userName:{3}", processID, noteText, userID, userName));
				}
			}

			return response;
		}

		public WorkItem3 UpdateWorkItem(long requestID, string processID, string userID, WorkItem3 workItem)
		{
			if (workItem == null)
			{
				throw new ArgumentNullException(nameof(workItem));
			}

			if (string.IsNullOrWhiteSpace(workItem.Source))
			{
				throw new ArgumentNullException(nameof(workItem.Source));
			}

			workItem.ExternalId = Verhoeff.GenerateID(workItem.ExternalId);
			workItem.OldExternalID = Verhoeff.GenerateID(workItem.OldExternalID);
			string source = workItem.Source;
			workItem.Source = GetSourceCode((WorkItemSource)Enum.Parse(typeof(WorkItemSource), workItem.Source));
			workItem.CreatedBy = string.IsNullOrWhiteSpace(workItem.CreatedBy) ? userID : workItem.CreatedBy;

			if (WorkItemSource.BPM.ToString().Equals(workItem.Source, StringComparison.OrdinalIgnoreCase))
			{
				workItem.CreatedDate = workItem.CreatedDate.HasValue ? workItem.CreatedDate.Value.ToLocalTime() : workItem.CreatedDate;
				workItem.ClosedDate = workItem.ClosedDate.HasValue ? workItem.ClosedDate.Value.ToLocalTime() : workItem.ClosedDate;
			}

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				workItem = this.UpdateWorkItem<WorkItem3>(item: workItem, rankOrder: null, parentExternalID: string.Empty);

				if (workItem.Id < 1)
				{
					throw new ApplicationException($"TrackingCenterProcessor.UpdateWorkItem: UpdateItem database operation fail. processID:{processID} ItemRequest:{workItem}");
				}
				else
				{
					//Add system generated note for N# Items
					if (source == WorkItemSource.NewAccount.ToString())
					{
						this.AddWorkItemNote(workItem.ExternalId,
											workItem.CreatedBy,
											null,
											(int)NoteUserType.InternalUser,
											NITEM_WORKITEM_NOTE,
											1,
											1,
											true,
											WorkItemSource.NewAccount.ToString());
					}


					// Add/Update ItemAccountAssociations for externalID on UpdateWorkItem details
					if (workItem.ItemAssociations?.Length > 0)
					{
						foreach (var itemAssociation in workItem.ItemAssociations)
						{
							itemAssociation.ItemID = workItem.Id;
							itemAssociation.ID = tcindc.UpdateItemAssociation(workItem.ExternalId,
														itemAssociation.ID,
														itemAssociation.AccountID,
														itemAssociation.PortfolioID,
														itemAssociation.FundingAccountNumber,
														itemAssociation.CustodialAccountNumber,
														itemAssociation.Custodian,
														itemAssociation.ItemName,
														itemAssociation.ItemStatus.HasValue ? (int)(itemAssociation.ItemStatus.Value) : (int?)null,
														itemAssociation.IsActive);
						}
					}

					if (workItem.DocuSignID != null && workItem.ExternalId.StartsWith("D", StringComparison.OrdinalIgnoreCase))
					{
						int eId = 0;
						int.TryParse(Verhoeff.ParseBPMID(workItem.ExternalId), out eId);
						tcindc.UpdateEnvelope(eId,
											workItem.DocuSignID.Value,
											workItem.eSigType,
											workItem.KitName,
											workItem.EnvelopeType,
											workItem.EsigStatus);
					}
				}
			}

			this.PopulateWebAndSortOrderByStatuses(workItem);
			this.PopulateUploadAllowed(workItem);

			return workItem;
		}

		public string AddEnvelope(Guid? docusignID)
		{
			string newExternalId = string.Empty;

			using (var trackingcenterdata = new TrackingCenterUpdateDataContext())
			{
				int envelopeId = trackingcenterdata.AddEnvelope(docusignID);
				newExternalId = Verhoeff.GenerateID(string.Format("D{0}", envelopeId));

				if (!Verhoeff.ValidateID(newExternalId))
				{
					throw new ApplicationException("AddEnvelope: EnvelopeID is invalid");
				}
			}

			return newExternalId;
		}

		public AlertQueueResponse AddAlertQueueItem(string processID, string subject, string message, AlertType alertType, string documentID)
		{
			var response = new AlertQueueResponse();
			int resultQ = 0;

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				resultQ = tcindc.AddAlertQueueItem(processID, subject, message, (int)alertType, documentID);
			}

			response.Success = resultQ > 0;
			return response;
		}

		public WQDocumentUploadResponse DocumentUpload(string userId, string userName, string fileName, string externalId, string status, byte[] fileContent)
		{
			int uploadId = INVALID_ITEM_UPLOAD_ID;
			string documentType = string.Empty;
			byte[] pdfBytes = new byte[] { };
			var response = new WQDocumentUploadResponse();
			string trackingCenterNote = string.Empty;
			var documentInfo = new DocStore.DocumentInfo
			{
				FormData = new Dictionary<string, string>()
			};

			try
			{
				Expression<Func<Item, bool>> predicate = PredicateBuilder.True<Item>();
				predicate = predicate.And(i => i.ExternalId == externalId);
				Item item = this.GetWorkItemInfo(predicate);

				if (item == null || string.IsNullOrWhiteSpace(item.ExternalId))
				{
					throw new ApplicationException(string.Format("DocumentUpload: Item not found. ExternalId:{0}.", externalId));
				}

				var documentAssociations = new ConcurrentBag<DocStore.DocumentAssociation>();
				string randomNumber = DateTime.Now.ToString("yyyyMMddHHmmss") + (new Random().Next(0, 100)).ToString();
				string envelopeId = item.DocuSignID.HasValue ? item.DocuSignID.Value.ToString() : string.Empty;
				userId = !string.IsNullOrWhiteSpace(userId) ? userId : (!string.IsNullOrWhiteSpace(item.AgentID) ? item.AgentID : item.CreatedBy);

				if (!string.IsNullOrWhiteSpace(fileName))
				{
					bool isPdf = Path.GetExtension(fileName).Equals(PDF, StringComparison.OrdinalIgnoreCase);

					if (!isPdf)
					{
						string fileExtension = Path.GetExtension(fileName).ToUpper();

						if (this.FileExtensions != null)
						{
							bool isValidExtension = this.FileExtensions.Any(x => x.Equals(fileExtension, StringComparison.OrdinalIgnoreCase));

							if (isValidExtension)
							{
								//Conversion of Image document into PDF
								var imageStream = new MemoryStream();
								imageStream.Write(fileContent, 0, fileContent.Length);
								pdfBytes = PDFHelper.ConvertImageStreamToPDFByte(imageStream);

								if (pdfBytes != null && pdfBytes.Length > 0)
								{
									documentInfo.FileName = Path.ChangeExtension(fileName, PDF);
								}
								else
								{
									pdfBytes = fileContent; //Assigning Original File Name and Content
									documentInfo.FileName = fileName;
								}
							}
							else
							{
								throw new ApplicationException(string.Format("DocumentUpload: Invalid file extension. Allowed file extension:{0},{1}", string.Join(",", this.FileExtensions.ToArray()), PDF));
							}
						}
					}
					else
					{
						documentInfo.FileName = fileName;
						pdfBytes = fileContent; //Assigning Original File Content
					}
				}

				//Make a entry in ItemUpload table
				uploadId = this.AddItemUpload(item.ExternalId, fileName, userId);

				if (uploadId <= INVALID_ITEM_UPLOAD_ID)
				{
					throw new ApplicationException($"DocumentUpload: Could not add ItemUpload record for WorkItem:{externalId}, FileName:{fileName}, userId:{userId}");
				}

				if (externalId.StartsWith("D", StringComparison.OrdinalIgnoreCase))
				{
					Dictionary<string, string> ocrData = this.GetOCRData(envelopeId);
					var ocrDataValue = new ConcurrentDictionary<string, string>();

					if (log.IsInfoEnabled)
					{
						log.Info($"TrackingCenterProcessor.DocumentUpload - ExternalId:{externalId} FileName:{fileName} FormData:{string.Join(",", ocrData.Select(kv => kv.Key + "=" + kv.Value).ToArray())}");
					}

					if (ocrData != null && ocrData.Count > 0)
					{
						var result = Parallel.ForEach(ocrData, od =>
									{
										if (!string.IsNullOrWhiteSpace(od.Key) && !ocrDataValue.ContainsKey(od.Key))
										{
											ocrDataValue.TryAdd(od.Key, FilenetHelper.ReplaceNonPrintableCharsWithSpace(!string.IsNullOrWhiteSpace(od.Value) ? od.Value.Trim() : od.Value));
										}
									});

						if (result.IsCompleted)
						{
							documentInfo.FormData = ocrDataValue.ToDictionary(pair => pair.Key, pair => pair.Value);
						}

						ocrData.TryGetValue(FORM_VERSION, out documentType);
					}
				}

				//Map DocumentInfo object attributes
				documentInfo.DocType = !string.IsNullOrWhiteSpace(documentType) ? documentType.ToUpper() : item.Description;
				documentInfo.ContentType = !string.IsNullOrEmpty(fileName) ? MimeTypes.GetMimeTypeByFileType(Path.GetExtension(fileName).ToUpper()) : string.Empty;
				documentInfo.DateAdded = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.Now, "Pacific Standard Time");
				documentInfo.EnvelopeID = string.Format("BAT{0}", item.Id);
				documentInfo.VendorNumber = string.Format("UP{0}", randomNumber);
				documentInfo.ClientVisible = false;
				documentInfo.AdvisorVisible = true;
				documentInfo.CustodianCode = item.Custodian;
				documentInfo.DNumber = externalId.StartsWith("B") ? string.Empty : externalId;

				trackingCenterNote = string.Format(TrackingCenterNotes.UploadDocument, fileName, DateTime.Now);

				if (externalId.StartsWith("N", StringComparison.OrdinalIgnoreCase))
				{
					if (item.Status.Equals(TrackingCenterStatus.AwaitingBrokerDealerApproval, StringComparison.OrdinalIgnoreCase))
					{
						documentInfo.Source = "New Account Upload";
						documentInfo.Status = DocStore.DocumentInfoStatus.NewAccountPendingBD;
						trackingCenterNote = string.Format(TrackingCenterNotes.AdvisorUpload, fileName, DateTime.Now);
					}
					else
					{
						documentInfo.Source = "New Account Upload";
						documentInfo.Status = DocStore.DocumentInfoStatus.NewAccountNew;
					}
				}
				else if (externalId.StartsWith("A", StringComparison.OrdinalIgnoreCase))
				{
					documentInfo.Source = "Online Submission";
					documentInfo.Status = DocStore.DocumentInfoStatus.Processed;
				}
				else
				{
					documentInfo.Source = "Tracking Center Upload";
					documentInfo.Status = DocStore.DocumentInfoStatus.New;
				}

				documentInfo.DocumentAssociations = new List<DocStore.DocumentAssociation>()
				{
					new DocStore.DocumentAssociation()
					{
						BDID = item.BDID,
						AgentID = item.AgentID,
						ClientID = item.ClientID,
						AccountID = item.AccountID,
						AccountNumber = item.CustodialAccountNumber,
						Status = DocStore.DocumentRecordStatus.Active
					}
				};

				if (item.ExternalId.StartsWith("D", StringComparison.OrdinalIgnoreCase))
				{
					documentInfo.DocumentWorkItems = Enumerable.Empty<DocStore.DocumentWorkItem>();
				}
				else
				{
					documentInfo.DocumentWorkItems = new List<DocStore.DocumentWorkItem>
					{
						new DocStore.DocumentWorkItem
						{
							Status = DocStore.DocumentRecordStatus.Active,
							WorkItemID = item.ExternalId
						}
					};
				}

				var storeService = StoreService.Create();
				DocStore.DocumentInfo documentResponse = storeService.AddDocument(documentInfo, pdfBytes);

				if (documentResponse != null && !string.IsNullOrEmpty(documentResponse.RawID))
				{
					// Successful upload to the document store. Mark "Pending" ItemUpload record as "Success"
					this.UpdateItemUpload(uploadId, string.Format($"{Guid.Parse(documentResponse.RawID):D}{Path.GetExtension(documentInfo.FileName)}"), "Success");

					using (var tcindc = new TrackingCenterUpdateDataContext())
					{
						// Add note to workitem in TrackingCenter database
						if (!externalId.StartsWith("A", StringComparison.OrdinalIgnoreCase))
						{
							tcindc.AddWorkItemNote(externalId,
											userId,
											userName,
											trackingCenterNote,
											WorkItemSource.TrackingCenter.ToString(),
											1,
											1,
											(int)NoteUserType.ExternalUser,
											true);
						}
						// Add Alert on workitem on each image upload from TrackingCenter
						if (externalId.StartsWith("B", StringComparison.OrdinalIgnoreCase))
						{
							tcindc.AddAlertQueueItem(externalId,
													string.Format("New Image Upload - {0}", externalId),
													string.Format("{0} successfully uploaded from TrackingCenter on {1} for workitem {2} and will be available momentarily.",
																fileName, DateTime.Now,
																externalId),
													(int)AlertType.Document,
													documentResponse.RawID);
						}

						// Add DocumentAction for each document.
						tcindc.AddDocumentAction(documentResponse.RawID,
								externalId,
								null,
								null,
								externalId.StartsWith("N", StringComparison.OrdinalIgnoreCase) ?
														(int)Entities.Enums.DocumentActionType.NewAccountUpload :
														(int)Entities.Enums.DocumentActionType.TrackingCenterUpload,
								userId,
								null);
					}

					if (externalId.StartsWith("N", StringComparison.OrdinalIgnoreCase))
					{
						if (!item.Status.Equals(TrackingCenterStatus.AwaitingBrokerDealerApproval, StringComparison.OrdinalIgnoreCase))
						{
							var workItem = new WorkItem3();
							workItem.CopyFrom(item);
							workItem.Status = TrackingCenterStatus.ImageReceived;
							workItem.InternalStatus = TrackingCenterStatus.ImageReceived;
							workItem.ResponseRequired = false;
							workItem.Source = WorkItemSource.Upload.ToString();

							this.UpdateWorkItem<WorkItem3>(item: workItem, rankOrder: null, parentExternalID: string.Empty);
						}
					}

					response.Success = true;
				}
				else
				{
					throw new ApplicationException(string.Format("DocumentUpload operation failed for WorkItemId:{0}", externalId));
				}
			}
			catch (Exception ex)
			{
				// Check if "Pending" record was written to ItemUpload table
				if (uploadId > INVALID_ITEM_UPLOAD_ID)
				{
					// Mark "Pending" ItemUpload record as "Failed"
					this.UpdateItemUpload(uploadId, fileName, "Failed");
				}

				log.Error(string.Format("DocumentUpload operation failed for WorkItemId: {0}", externalId), ex);
				throw ex;
			}

			response.ExternalID = externalId;
			return response;
		}

		public ProcessAlert[] GetProcessAlerts()
		{
			int alertThreshold = 50;
			int.TryParse(ConfigurationManager.AppSettings.Get("AlertThresholdValue"), out alertThreshold);

			using (var tcdc = new TrackingCenterDataReadOnly())
			{
				var alerts = tcdc.GetProcessAlerts().Take(50).ToArray();

				if (alerts != null && alerts.Any())
				{
					Parallel.ForEach(alerts,
						alert =>
						{
							alert.Subject = !string.IsNullOrWhiteSpace(alert.Subject) ? Convert.ToBase64String(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(HttpUtility.HtmlDecode(alert.Subject))))) : string.Empty;

							alert.Message = !string.IsNullOrWhiteSpace(alert.Message) ? Convert.ToBase64String(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(HttpUtility.HtmlDecode(alert.Message))))) : string.Empty;
						});

					return alerts;
				}
				else
				{
					return Array.Empty<ProcessAlert>();
				}
			}
		}

		public UpdateQueuesResponse UpdateAlertsAsProcessedInBatch(List<UpdateProcessAlertStatus> alertRequests)
		{
			if (alertRequests == null || alertRequests.Count == 0)
			{
				return null;
			}

			var response = new UpdateQueuesResponse();

			try
			{
				using (var tcdc = new TrackingCenterUpdateDataContext())
				{
					foreach (var alert in alertRequests)
					{
						if (alert.QueueId > 0)
						{
							tcdc.UpdateAlertQueue(alert.QueueId, alert.Processed, alert.StatusDescription);
						}
					}
				}

				response.Success = true;
			}
			catch (Exception ex)
			{
				response.Fault = new Fault
				{
					Message = ex.Message
				};

				log.Error("UpdateAlertsAsProcessedInBatch", ex);
			}

			return response;
		}

		public WorkItemNote UpdateNoteAdvisorVisible(string processID, string userID, int noteID, bool advisorVisible)
		{
			int result = 0;

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				result = tcindc.UpdateNoteAdvisorVisible(processID, userID, noteID, advisorVisible);
			}

			if (result > 0)
			{
				return new WorkItemNote
				{
					Id = result,
					CreatedBy = userID,
					AdvisorVisible = advisorVisible
				};
			}
			else
			{
				throw new ApplicationException($"TrackingCenterProcessor.UpdateNoteAdvisorVisible: Update workitem advisor visible flag failed. processID:{processID}");
			}
		}

		public WorkItem3 GetBPMItemDetails(string externalID)
		{
			Item item = null;
			var response = new WorkItem3();

			using (var tcdc = new TrackingCenterDataContext())
			{
				item = tcdc.GetBPMItemDetails(externalID)
							.Select(i => new Item
							{
								AdvisorVisible = i.AdvisorVisible,
								AgentID = i.AgentID,
								BundleID = i.BundleID,
								ClientID = i.ClientID,
								ClosedDate = i.ClosedDate,
								CreatedBy = i.CreatedBy,
								CreatedDate = i.CreatedDate,
								Description = i.Description,
								ExternalId = i.ExternalId,
								Id = i.Id,
								InternalStatus = i.InternalStatus,
								IsEsignatureRequested = i.IsEsignatureRequested,
								IsParent = i.IsParent,
								ModifiedDate = i.ModifiedDate,
								ObjectName = i.ObjectName,
								OldExternalID = i.OldExternalID,
								ParentExternalID = i.ParentExternalID,
								ParentItemID = i.ParentItemID,
								RankOrder = i.RankOrder,
								ResponseAllowed = i.ResponseAllowed,
								ResponseRequired = i.ResponseRequired,
								ShortExternalDescription = i.ShortExternalDescription,
								SsoGuidId = i.SsoGuidId,
								Status = i.Status,
								SubmitToBD = i.SubmitToBD,
								UploadAllowed = i.UploadAllowed,
								WorkItemType = i.WorkItemType
							}).FirstOrDefault();

				if (item != null && item.Id > 0)
				{
					response.ItemAssociations = tcdc.GetItemAssociations().Where(x => x.ItemID == item.Id).ToArray();
				}
			}

			if (item != null)
			{
				response.CopyFrom(item);

				if (response != null)
				{
					response.CreatedBy = HttpUtility.HtmlEncode(response.CreatedBy);
					response.CreatedDate = response.CreatedDate.Value.ToUniversalTime();
					response.ModifiedDate = response.ModifiedDate.HasValue ? response.ModifiedDate.Value.ToUniversalTime() : response.ModifiedDate;
					response.ClosedDate = response.ClosedDate.HasValue ? response.ClosedDate.Value.ToUniversalTime() : response.ClosedDate;
				}
			}
			else
			{
				throw new ArgumentException(string.Format("GetBPMItemDetails: Item not found. processID:{0}", externalID));
			}

			return response;
		}

		public DocumentStatus GetDocumentStatus(string externalID, int? bundleID)
		{
			DocumentStatus response = null;

			using (var tcdc = new TrackingCenterDataContext())
			{
				response = tcdc.GetDocumentStatus(externalID, bundleID).FirstOrDefault();
			}

			return response;
		}

		public int AddDocumentAction(string doc_id, string externalID, int? processID, int? applicationID, int actionType, string userID, string applicationDescription)
		{
			int uploadID = 0;

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				uploadID = tcindc.AddDocumentAction(doc_id, Verhoeff.GenerateID(externalID), processID, applicationID, actionType, userID, applicationDescription);
			}

			if (uploadID <= 0)
			{
				throw new ApplicationException($"AddDocumentAction: Fail to add document action. processID:{processID} externalID:{externalID} docID:{doc_id}");
			}

			return uploadID;
		}

		/// <summary>
		/// Add/Update new ENAX Parent Item information in TrackingCenter database. Operation will return ParentExternalID.
		/// </summary>
		/// <returns>ParentExternalID</returns>
		public string UpdateParentWorkItem(long requestId, string processID, string userID, ParentWorkItem parentWorkItem)
		{
			if (parentWorkItem == null)
			{
				throw new ArgumentNullException(nameof(parentWorkItem));
			}

			if (string.IsNullOrWhiteSpace(parentWorkItem.ExternalId))
			{
				throw new ArgumentNullException(nameof(parentWorkItem.ExternalId));
			}

			if (string.IsNullOrWhiteSpace(parentWorkItem.Source))
			{
				throw new ArgumentNullException(nameof(parentWorkItem.Source));
			}

			parentWorkItem.ExternalId = Verhoeff.GenerateID(parentWorkItem.ExternalId);
			parentWorkItem.OldExternalID = Verhoeff.GenerateID(parentWorkItem.OldExternalID);
			parentWorkItem.Source = GetSourceCode((WorkItemSource)Enum.Parse(typeof(WorkItemSource), parentWorkItem.Source));
			parentWorkItem.IsParent = true;
			parentWorkItem.CreatedBy = string.IsNullOrWhiteSpace(parentWorkItem.CreatedBy) ? userID : parentWorkItem.CreatedBy;

			if (WorkItemSource.BPM.ToString().Equals(parentWorkItem.Source, StringComparison.OrdinalIgnoreCase))
			{
				parentWorkItem.CreatedDate = parentWorkItem.CreatedDate.HasValue ? parentWorkItem.CreatedDate.Value.ToLocalTime() : parentWorkItem.CreatedDate;
				parentWorkItem.ClosedDate = parentWorkItem.ClosedDate.HasValue ? parentWorkItem.ClosedDate.Value.ToLocalTime() : parentWorkItem.ClosedDate;
			}

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				parentWorkItem = this.UpdateWorkItem<ParentWorkItem>(item: parentWorkItem, rankOrder: null) as ParentWorkItem;

				if (parentWorkItem.Id < 1)
				{
					throw new ApplicationException($"UpdateParentWorkItem: UpdateItem database operation fail. processID:{processID} ItemRequest:{parentWorkItem}");
				}
				else
				{
					if (parentWorkItem.ItemAssociations != null && parentWorkItem.ItemAssociations.Any())
					{
						foreach (var itemAssociation in parentWorkItem.ItemAssociations)
						{
							itemAssociation.ItemID = parentWorkItem.Id;
							itemAssociation.ID = tcindc.UpdateItemAssociation(parentWorkItem.ExternalId,
																				itemAssociation.ID,
																				itemAssociation.AccountID,
																				itemAssociation.PortfolioID,
																				itemAssociation.FundingAccountNumber,
																				itemAssociation.CustodialAccountNumber,
																				itemAssociation.Custodian,
																				itemAssociation.ItemName,
																				itemAssociation.ItemStatus.HasValue ? (int)(itemAssociation.ItemStatus.Value) : (int?)null,
																				itemAssociation.IsActive);
						}
					}

					if (parentWorkItem.ChildWorkItems != null && parentWorkItem.ChildWorkItems.Any())
					{
						foreach (var child in parentWorkItem.ChildWorkItems)
						{
							this.UpdateChildWorkItem(child.ExternalId, userID, child);
						}
					}

				}
			}

			return parentWorkItem.ExternalId;
		}

		/// <summary>
		/// Add/Update new ENAX Child Item information in TrackingCenter database. Operation will return ChildExternalID.
		/// </summary>
		/// <returns>ChildExternalID</returns>
		public string UpdateChildWorkItem(string processID, string userID, ChildWorkItem childWorkItem)
		{
			if (childWorkItem == null)
			{
				throw new ArgumentNullException(nameof(childWorkItem));
			}

			if (string.IsNullOrWhiteSpace(childWorkItem.ExternalId))
			{
				throw new ArgumentNullException(nameof(childWorkItem.ExternalId));
			}

			if (string.IsNullOrWhiteSpace(childWorkItem.Source))
			{
				throw new ArgumentNullException(nameof(childWorkItem.Source));
			}

			if (string.IsNullOrWhiteSpace(childWorkItem.ParentExternalID))
			{
				throw new ArgumentNullException(nameof(childWorkItem.ParentExternalID));
			}

			childWorkItem.ExternalId = Verhoeff.GenerateID(childWorkItem.ExternalId);
			childWorkItem.OldExternalID = Verhoeff.GenerateID(childWorkItem.OldExternalID);
			childWorkItem.Source = GetSourceCode((WorkItemSource)Enum.Parse(typeof(WorkItemSource), childWorkItem.Source));
			childWorkItem.CreatedBy = string.IsNullOrWhiteSpace(childWorkItem.CreatedBy) ? userID : childWorkItem.CreatedBy;

			if (WorkItemSource.BPM.ToString().Equals(childWorkItem.Source, StringComparison.OrdinalIgnoreCase))
			{
				childWorkItem.CreatedDate = childWorkItem.CreatedDate.HasValue ? childWorkItem.CreatedDate.Value.ToLocalTime() : childWorkItem.CreatedDate;
				childWorkItem.ClosedDate = childWorkItem.ClosedDate.HasValue ? childWorkItem.ClosedDate.Value.ToLocalTime() : childWorkItem.ClosedDate;
			}

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				childWorkItem = this.UpdateWorkItem<ChildWorkItem>(item: childWorkItem, rankOrder: childWorkItem.RankOrder, parentExternalID: childWorkItem.ParentExternalID) as ChildWorkItem;

				if (childWorkItem.Id < 1)
				{
					throw new ApplicationException(string.Format("UpdateChildWorkItem: UpdateItem database operation fail. processID:{0}", processID));
				}
				else
				{
					if (childWorkItem.ItemAssociations != null && childWorkItem.ItemAssociations.Any())
					{
						foreach (var itemAssociation in childWorkItem.ItemAssociations)
						{
							itemAssociation.ItemID = childWorkItem.Id;
							itemAssociation.ID = tcindc.UpdateItemAssociation(childWorkItem.ExternalId,
																				itemAssociation.ID,
																				itemAssociation.AccountID,
																				itemAssociation.PortfolioID,
																				itemAssociation.FundingAccountNumber,
																				itemAssociation.CustodialAccountNumber,
																				itemAssociation.Custodian,
																				itemAssociation.ItemName,
																				itemAssociation.ItemStatus.HasValue ? (int)itemAssociation.ItemStatus.Value : (int?)null,
																				itemAssociation.IsActive);
						}
					}
				}
			}

			return childWorkItem.ExternalId;
		}

		public int UpdateItemToBundle(string processID, string userID, ChildWorkItem workItem)
		{
			if (workItem == null)
			{
				throw new ArgumentNullException(nameof(workItem));
			}

			if (string.IsNullOrWhiteSpace(workItem.ExternalId))
			{
				throw new ArgumentNullException(nameof(workItem.ExternalId));
			}

			if (string.IsNullOrWhiteSpace(workItem.Source))
			{
				throw new ArgumentNullException(nameof(workItem.Source));
			}

			int result = 0;
			string ExternalID = Verhoeff.GenerateID(workItem.ExternalId);
			string oldExternalID = Verhoeff.GenerateID(workItem.OldExternalID);
			string source = GetSourceCode((WorkItemSource)Enum.Parse(typeof(WorkItemSource), workItem.Source));

			workItem.CreatedBy = ((string.IsNullOrWhiteSpace(workItem.CreatedBy)) ? userID : workItem.CreatedBy);

			if (WorkItemSource.BPM.ToString().Equals(workItem.Source, StringComparison.OrdinalIgnoreCase))
			{
				workItem.CreatedDate = ((workItem.CreatedDate.HasValue) ? workItem.CreatedDate.Value.ToLocalTime() : workItem.CreatedDate);
				workItem.ClosedDate = ((workItem.ClosedDate.HasValue) ? workItem.ClosedDate.Value.ToLocalTime() : workItem.ClosedDate);
			}

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				result = tcindc.UpdateItemToBundle(oldExternalID,
													ExternalID,
													workItem.BDID,
													workItem.AdvisorID,
													workItem.AgentID,
													workItem.ClientID,
													workItem.ObjectName,
													workItem.Description,
													workItem.Status,
													workItem.InternalStatus,
													workItem.CreatedBy,
													workItem.AdvisorVisible,
													workItem.ResponseAllowed,
													workItem.ResponseRequired,
													workItem.IsClosable,
													source,
													workItem.CreatedDate,
													workItem.ClosedDate,
													workItem.BundleID,
													workItem.ShortExternalDescription,
													workItem.ParentExternalID,
													workItem.RankOrder);

				if (result < 1)
				{
					throw new ApplicationException($"UpdateItemToBundle: UpdateItem database operation fail. processID:{processID} ItemRequest:{workItem}");
				}
				else
				{
					if (workItem.ItemAssociations != null && workItem.ItemAssociations.Any())
					{
						foreach (var itemAssociation in workItem.ItemAssociations)
						{
							tcindc.UpdateItemAssociation(ExternalID,
														itemAssociation.ID,
														itemAssociation.AccountID,
														itemAssociation.PortfolioID,
														itemAssociation.FundingAccountNumber,
														itemAssociation.CustodialAccountNumber,
														itemAssociation.Custodian,
														itemAssociation.ItemName,
														itemAssociation.ItemStatus.HasValue ? (int)(itemAssociation.ItemStatus.Value) : (int?)null,
														itemAssociation.IsActive);
						}
					}
				}
			}

			return result;
		}

		public int UpdateItemAssociation(string externalID, int itemAssociationID, string accountID, string portfolioID, string fundingAccNo,
			string custodialAccNo, string custodian, string itemName, ItemStatus? itemStatus, bool isActive)
		{
			if (string.IsNullOrWhiteSpace(externalID))
			{
				throw new ArgumentNullException(nameof(externalID));
			}

			if (itemAssociationID < 0)
			{
				throw new ArgumentNullException(nameof(itemAssociationID));
			}

			int response = 0;

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				response = tcindc.UpdateItemAssociation(externalID,
														itemAssociationID,
														accountID,
														portfolioID,
														fundingAccNo,
														custodialAccNo,
														custodian,
														itemName,
														(int?)itemStatus,
														isActive);
			}

			return response;
		}

		public bool UpdateItemAssociationName(string parentExternalID, IDictionary<string, string> itemAssociation)
		{
			if (string.IsNullOrWhiteSpace(parentExternalID))
			{
				throw new ArgumentNullException(nameof(parentExternalID));
			}

			if (itemAssociation == null && itemAssociation.Count() <= 0)
			{
				throw new ArgumentNullException(nameof(itemAssociation));
			}

			bool response = true;
			int result = 0;

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{

				foreach (var accountnumber in itemAssociation)
				{
					result = tcindc.UpdateItemAssociationName(parentExternalID, accountnumber.Key, accountnumber.Value);

					if (result < 0)
					{
						response = false;
						throw new ApplicationException(string.Format("UpdateItemAssociationName database operation fail. parentExternalID:{0} accountnumber:{1}", parentExternalID, accountnumber.Key));
					}
				}

			}

			return response;
		}

		public WorkItemAssociation[] GetItemAssociations(string externalID, string userID)
		{
			if (string.IsNullOrWhiteSpace(externalID))
			{
				throw new ArgumentNullException(nameof(externalID));
			}

			var response = new WorkItemAssociation[0];

			using (var tcdc = new TrackingCenterDataContext())
			{
				response = tcdc.GetItemAssociationsByExternalId(externalID).ToArray();
			}

			return response;
		}

		public ChildWorkItem GetChildWorkItemDetails(string processID, string userID, string sourceID)
		{
			if (string.IsNullOrWhiteSpace(processID))
			{
				throw new ArgumentNullException(nameof(processID));
			}

			var response = new ChildWorkItem();
			Item item = null;
			List<Item> items = null;
			Expression<Func<Item, bool>> predicate = PredicateBuilder.True<Item>();
			predicate = predicate.And(i => i.ExternalId == processID);
			predicate = predicate.And(i => i.ItemAssociationActive == true);

			//New - Data access method.
			using (var tcdc = new TrackingCenterDataContext())
			{
				items = tcdc.GetWorkitemList()
								.Where(predicate)
								.Select(i => new Item
								{
									AdvisorVisible = i.AdvisorVisible,
									AgentID = i.AgentID,
									BundleID = i.BundleID,
									ClientID = i.ClientID,
									ClosedDate = i.ClosedDate,
									CreatedBy = i.CreatedBy,
									CreatedDate = i.CreatedDate,
									Description = i.Description,
									ExternalId = i.ExternalId,
									Id = i.Id,
									InternalStatus = i.InternalStatus,
									IsParent = i.IsParent,
									ModifiedDate = i.ModifiedDate,
									ObjectName = i.ObjectName,
									ParentExternalID = i.ParentExternalID,
									ParentItemID = i.ParentItemID,
									RankOrder = i.RankOrder,
									ShortExternalDescription = i.ShortExternalDescription,
									Status = i.Status,
									AccountID = i.AccountID,
									CustodialAccountNumber = i.CustodialAccountNumber,
									Custodian = i.Custodian,
									FundingAccountNumber = i.FundingAccountNumber,
									ItemAssociationActive = i.ItemAssociationActive,
									ItemAssociationID = i.ItemAssociationID,
									ItemName = i.ItemName
								}).ToList();
			}

			if (items != null && items.Any())
			{
				item = items.FirstOrDefault();
				response.CopyFrom(item);
				response.Notes = Array.Empty<WorkItemNote>();
				response.ItemAssociations = items.Where(it => it.Id == item.Id).Select(ia => new WorkItemAssociation
				{
					AccountID = ia.AccountID,
					CustodialAccountNumber = ia.CustodialAccountNumber,
					ItemName = ia.ItemName,
					Custodian = ia.Custodian,
					IsActive = ia.ItemAssociationActive ?? true
				}).ToArray();
			}
			else
			{
				log.Error($"TrackingCenterProcessor.GetChildWorkItemDetails: Data request cannot be constructed. processID:{processID}");
				response = null;
			}

			return response;
		}

		public RelatedWorkItemsResponse GetRelatedWorkItemDetails(string externalID, WorkItemSource source)
		{
			if (string.IsNullOrWhiteSpace(externalID))
			{
				throw new ArgumentNullException(nameof(externalID));
			}

			var response = new RelatedWorkItemsResponse();
			IList<ChildWorkItem> childWorkItems = new List<ChildWorkItem>();
			IEnumerable<Item> items = null;
			IEnumerable<WorkItemNote> notes = null;
			IEnumerable<WorkItemAssociation> itemAssociations = null;
			Expression<Func<WorkItemNote, bool>> parentNote = PredicateBuilder.True<WorkItemNote>();
			bool isAdvisorVisible = false;

			if (WorkItemSource.TrackingCenter.ToString().Equals(source.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				parentNote = parentNote.And(wil => wil.AdvisorVisible == true);
				isAdvisorVisible = true;
			}

			using (var tcdc = new TrackingCenterDataContext())
			{
				items = tcdc.GetRelatedItems(externalID).Where(x => isAdvisorVisible ? x.AdvisorVisible == true : true).ToList();

				if (items == null || items.Count() <= 0)
				{
					return response;
				}
				else
				{
					itemAssociations = tcdc.GetRelatedItemAssociations(externalID).ToList();

					if (source != WorkItemSource.BPM)
					{
						notes = tcdc.GetRelatedItemNotes(externalID).OrderByDescending(wn => wn.CreatedDate).ToList();
					}
				}
			}

			if (items != null)
			{
				response.ParentWorkItem = new ParentWorkItem
				{
					Source = source.ToString(),
					Notes = new WorkItemNote[0]
				};

				response.ParentWorkItem.CopyFrom(items.Where(x => x.ParentItemID == null).FirstOrDefault());

				if (response.ParentWorkItem.Id > 0)
				{
					foreach (var item in items.Where(x => x.ParentItemID > 0))
					{
						var childWI = new ChildWorkItem
						{
							Source = source.ToString(),
							Notes = new WorkItemNote[0]
						};

						this.PopulateWebAndSortOrderByStatuses(item);
						this.PopulateUploadAllowed(item);
						childWorkItems.Add(childWI.CopyFrom(item));
					}

					// Bundle (i.e.,Parent) and Component (i.e.,Child) items notes
					if (notes != null)
					{
						parentNote = parentNote.And(wil => wil.ItemID == response.ParentWorkItem.Id);
						response.ParentWorkItem.Notes = notes.AsQueryable().Where(parentNote).ToArray();

						if (response.ParentWorkItem.Notes != null && response.ParentWorkItem.Notes.Any())
						{
							foreach (var note in response.ParentWorkItem.Notes)
							{
								note.NoteText = !string.IsNullOrWhiteSpace(note.NoteText) ? HttpUtility.HtmlEncode(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(note.NoteText))) : string.Empty;
							}
						}

						if (childWorkItems != null && childWorkItems.Count > 0)
						{
							Parallel.ForEach(childWorkItems,
								childItem =>
								{
									childItem.Notes = notes.Where(wil => wil.ItemID == childItem.Id && (isAdvisorVisible ? wil.AdvisorVisible == true : true)).ToArray();
								});

							foreach (var childItem in childWorkItems)
							{
								if (childItem.Notes != null && childItem.Notes.Any())
								{
									foreach (var note in childItem.Notes)
									{
										note.WorkItemExternalID = childItem.ExternalId;
										note.NoteText = !string.IsNullOrWhiteSpace(note.NoteText) ? HttpUtility.HtmlEncode(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(note.NoteText))) : string.Empty;
									}
								}
							}
						}
					}

					// Bundle (i.e.,Parent) and Component (i.e.,Child) items associations
					if (itemAssociations != null)
					{
						response.ParentWorkItem.ItemAssociations = itemAssociations.Where(ia => ia.ItemID == response.ParentWorkItem.Id).ToArray();

						if (childWorkItems != null && childWorkItems.Count > 0)
						{
							Parallel.ForEach(childWorkItems,
								childItem =>
								{
									childItem.ItemAssociations = itemAssociations.Where(ia => ia.ItemID == childItem.Id).ToArray();
								});
						}
					}

					response.ParentWorkItem.ChildWorkItems = childWorkItems.ToArray();
				}
			}

			this.PopulateWebAndSortOrderByStatuses(response.ParentWorkItem);
			this.PopulateUploadAllowed(response.ParentWorkItem);

			return response;
		}

		public WorkItemList[] GetExternalDescription(string externalID)
		{
			var response = new WorkItemList[0];

			using (var tcdc = new TrackingCenterDataContext())
			{
				IEnumerable<Item> item = tcdc.GetRelatedItems(externalID).Where(x => x.ParentItemID != null);

				if (item != null)
				{
					var workItem = new ConcurrentBag<WorkItemList>();

					Parallel.ForEach(item, it =>
					{
						var workItemList = new WorkItemList();
						workItem.Add(workItemList.CopyFrom(it));
					});

					response = workItem.Count > 0 ? workItem.ToArray() : response;
				}
			}

			return response;
		}

		public bool AddItemToBundle(string externalID, string parentExternalID, string shortExternalDescription, int rankOrder)
		{
			if (string.IsNullOrWhiteSpace(externalID))
			{
				throw new ArgumentNullException(nameof(externalID));
			}

			if (string.IsNullOrWhiteSpace(parentExternalID))
			{
				throw new ArgumentNullException(nameof(parentExternalID));
			}

			int result = 0;
			bool isAdopt = true;
			externalID = Verhoeff.GenerateID(externalID);

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				result = tcindc.AddItemToBundle(externalID, parentExternalID, shortExternalDescription, rankOrder);

				if (result < 1)
				{
					isAdopt = false;
					throw new ApplicationException(string.Format("AddItemToBundle: UpdateItem database operation fail. externalID:{0}", externalID));
				}
			}

			return isAdopt;
		}

		public WorkItem3[] GetBPMWorkItemList(string[] agentId,
											string[] clientId,
											string[] accountId,
											string[] accountNumber,
											string filter,
											string[] status = null,
											string[] internalStatus = null)
		{
			if ((agentId == null || agentId.Count() == 0)
				&& (clientId == null || clientId.Count() == 0)
				&& (accountId == null || accountId.Count() == 0)
				&& (accountNumber == null || accountNumber.Count() == 0)
				&& string.IsNullOrWhiteSpace(filter))
			{
				throw new ArgumentException("Criteria missing. There should be at least one criteria for GetBPMWorkItemList.");
			}

			string externalId = string.Empty;
			IQueryable<Item> iqWILs = null;
			WorkItem3[] response = ArrayExt.Empty<WorkItem3>();
			string newFilter = string.Empty;
			Expression<Func<Item, bool>> predicate = PredicateBuilder.True<Item>();
			IList<Item> itemList = new List<Item>();
			var workItem3 = new ConcurrentBag<WorkItem3>();

			if (agentId?.Count() > 0)
			{
				predicate = predicate.And(ExpressionBuilder.LINQExpression<Item>(agentId, WorkItemFilters.AgentId.ToString()));
			}

			if (clientId?.Count() > 0)
			{
				predicate = predicate.And(ExpressionBuilder.LINQExpression<Item>(clientId, WorkItemFilters.ClientId.ToString()));
			}

			if (accountId?.Count() > 0)
			{
				predicate = predicate.And(ExpressionBuilder.LINQExpression<Item>(accountId, WorkItemFilters.AccountId.ToString()));
			}

			if (accountNumber?.Count() > 0)
			{
				predicate = predicate.And(ExpressionBuilder.LINQExpression<Item>(accountNumber, WorkItemFilters.CustodialAccountNumber.ToString()));
			}

			if (status?.Count() > 0)
			{
				predicate = predicate.And(ExpressionBuilder.LINQExpression<Item>(status, WorkItemFilters.Status.ToString()));
			}

			if (internalStatus?.Count() > 0)
			{
				predicate = predicate.And(ExpressionBuilder.LINQExpression<Item>(internalStatus, WorkItemFilters.InternalStatus.ToString()));
			}

			predicate = predicate.And(ia => ia.ItemAssociationActive == true);

			if (!string.IsNullOrWhiteSpace(filter))
			{
				var regex = new Regex("\".*?\"");

				//Split filter types.
				IEnumerable<string> andFilters = filter.Split(new string[] { "&&" }, StringSplitOptions.None).ToList();

				foreach (string f in andFilters)
				{
					string tempForNewFilter = string.Empty;
					Expression<Func<Item, bool>> filters = null;
					IEnumerable<string> orFilters = f.Split(new string[] { "||" }, StringSplitOptions.None).ToList();

					foreach (string i in orFilters)
					{
						if (i.Contains("Status"))
						{
							string theFilter = i.Replace("(", "").Replace(")", "");
							var matches = regex.Matches(theFilter);

							foreach (object item in matches)
							{
								string filterstring = item.ToString().Replace("\"", "");

								if (filterstring == "closed<7")
								{
									filters = this.UseOrInitOrExpression(filters, (wit => wit.ClosedDate >= DateTime.Now.AddDays(-7)));
								}
								else if (filterstring == "closed<90")
								{
									filters = this.UseOrInitOrExpression(filters, (wit => wit.ClosedDate >= DateTime.Now.AddDays(-90)));
								}
								else if (filterstring == "open-Advisor")
								{
									filters = this.UseOrInitOrExpression(filters, (wit => wit.ClosedDate == null && wit.ResponseRequired == true));
								}
								else if (filterstring == "open")
								{
									filters = this.UseOrInitOrExpression(filters, (wit => wit.ClosedDate == null));
								}
								else if (filterstring == "closed")
								{
									filters = this.UseOrInitOrExpression(filters, (wit => wit.ClosedDate != null));
								}
							}
						}
						else if (i.ToLower().Contains("isparent"))
						{
							string theFilter = i.Replace("(", "").Replace(")", "");
							var matches = regex.Matches(theFilter);

							foreach (object item in matches)
							{
								string filterstring = item.ToString().Replace("\"", "");
								filters = this.UseOrInitOrExpression(filters, (wil => wil.IsParent == bool.Parse(filterstring)));
							}
						}
						else if (i.ToLower().Contains("isadvisorvisible"))
						{
							string theFilter = i.Replace("(", "").Replace(")", "");
							var matches = regex.Matches(theFilter);

							foreach (object item in matches)
							{
								string filterstring = item.ToString().Replace("\"", "");
								filters = this.UseOrInitOrExpression(filters, (wil => wil.AdvisorVisible == bool.Parse(filterstring)));
							}
						}
						else if (i.ToLower().Contains("isunadopted"))
						{
							string theFilter = i.Replace("(", "").Replace(")", "");
							var matches = regex.Matches(theFilter);

							foreach (object item in matches)
							{
								string filterstring = item.ToString().Replace("\"", "");
								bool adopted = bool.Parse(filterstring);

								if (!adopted)
								{
									filters = this.UseOrInitOrExpression(filters, (wil => wil.ParentItemID == null));
								}
							}
						}
						else
						{
							//Check with double quotes or single quotes in the like statement.
							Match likeMatch = Regex.Match(i, "like\\((.*?)[,'%](.*?)%'\\)|like\\((.*?)[,\"%](.*?)%\"\\)");
							string theFilter = string.Empty;

							if (likeMatch.Length > 0)
							{
								theFilter = likeMatch.Captures[0].ToString();
							}
							else
							{
								theFilter = i.Trim(new char[] { '(', ')', ' ' });
							}

							if (string.IsNullOrEmpty(tempForNewFilter))
							{
								tempForNewFilter = string.Concat("(", theFilter, ")");
							}
							else
							{
								if (!string.IsNullOrEmpty(theFilter))
								{
									tempForNewFilter = string.Concat(tempForNewFilter, "||", string.Concat("(", theFilter, ")"));
								}
							}
						}
					}

					if (filters != null)
					{
						predicate = predicate.And(filters);
					}

					if (string.IsNullOrEmpty(newFilter))
					{
						newFilter = tempForNewFilter;
					}
					else
					{
						if (!string.IsNullOrEmpty(tempForNewFilter))
						{
							newFilter = string.Concat(newFilter, "&&", tempForNewFilter);
						}
					}
				}
			}

			using (var tcdc = new TrackingCenterDataContext())
			{
				iqWILs = tcdc.GetWorkitemList()
							.Where(predicate)
							.Select(i => new Item
							{
								AdvisorID = i.AdvisorID,
								AdvisorVisible = i.AdvisorVisible,
								AgentID = i.AgentID,
								BDID = i.BDID,
								BundleID = i.BundleID,
								ClientID = i.ClientID,
								ClosedDate = i.ClosedDate,
								CreatedDate = i.CreatedDate,
								Description = i.Description,
								ExternalId = i.ExternalId,
								Id = i.Id,
								InternalStatus = i.InternalStatus,
								IsEsignatureRequested = i.IsEsignatureRequested,
								IsParent = i.IsParent,
								ModifiedDate = i.ModifiedDate,
								ObjectName = i.ObjectName,
								ParentExternalID = i.ParentExternalID,
								ParentItemID = i.ParentItemID,
								RankOrder = i.RankOrder,
								ResponseAllowed = i.ResponseAllowed,
								ResponseRequired = i.ResponseRequired,
								ShortExternalDescription = i.ShortExternalDescription,
								SsoGuidId = i.SsoGuidId,
								Status = i.Status,
								SubmitToBD = i.SubmitToBD,
								UploadAllowed = i.UploadAllowed,
								WorkItemType = i.WorkItemType,
								AccountID = i.AccountID,
								CustodialAccountNumber = i.CustodialAccountNumber,
								Custodian = i.Custodian,
								FundingAccountNumber = i.FundingAccountNumber,
								ItemAssociationActive = i.ItemAssociationActive,
								ItemAssociationID = i.ItemAssociationID,
								ItemName = i.ItemName
							});

				if (!string.IsNullOrEmpty(newFilter))
				{
					iqWILs = this.ApplyFilters<Item>(string.Concat("(", newFilter, ")"), null)(iqWILs);
				}

				itemList = iqWILs.ToList();
			}

			foreach (var item in itemList)
			{
				var workItem = new WorkItem3
				{
					ItemAssociations = itemList.Where(it => it.Id == item.Id).Select(ia => new WorkItemAssociation
					{
						AccountID = ia.AccountID,
						CustodialAccountNumber = ia.CustodialAccountNumber,
						ItemName = ia.ItemName,
						Custodian = ia.Custodian,
						IsActive = ia.ItemAssociationActive ?? true
					}).ToArray()
				};

				workItem3.Add(workItem.CopyFrom(item));
			}

			response = workItem3.ToArray();
			return response;
		}

		public bool UpdateNoteInternalVisible(string externalId, int noteId, bool internalVisible, string userId)
		{
			int result = 0;

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				result = tcindc.UpdateHideNoteFlag(externalId, noteId, internalVisible, userId);
			}

			return result > 0;
		}

		public ClosedAgedOpenWorkItemResponse CloseAgedOpenDNumber()
		{
			ClosedAgedOpenWorkItemResponse response = new ClosedAgedOpenWorkItemResponse();

			using (var tcdc = new TrackingCenterDataContext())
			{
				response = tcdc.CloseAgedOpenDNumber().FirstOrDefault();
			}

			return response;
		}

		public WorkItemsResponse3 GetAgedOpenWorkItemList(bool isEsignatureItem, bool submitToBD, int numberofDays, string[] status, string filter)
		{
			var response = new WorkItemsResponse3();
			Expression<Func<Item, bool>> predicate = PredicateBuilder.True<Item>();
			string newFilter = string.Empty;
			IQueryable<Item> iqWILs = null;
			IList<Item> itemList = new List<Item>();
			IList<WorkItem3> workItems = new List<WorkItem3>();

			if (status != null && status.Length > 0)
			{
				predicate = predicate.And(ExpressionBuilder.LINQExpression<Item>(status, WorkItemFilters.Status.ToString()));
			}

			predicate = predicate.And(wil => wil.IsEsignatureRequested == isEsignatureItem);

			predicate = predicate.And(wil => wil.SubmitToBD == submitToBD);

			predicate = predicate.And(wil => wil.ParentItemID == null);

			if (!string.IsNullOrWhiteSpace(filter))
			{
				var regex = new Regex("\".*?\"");

				//Split filter types.
				IEnumerable<string> andFilters = filter.Split(new string[] { "&&" }, StringSplitOptions.None).ToList();

				foreach (string f in andFilters)
				{
					string tempForNewFilter = string.Empty;
					Expression<Func<Item, bool>> filters = null;
					IEnumerable<string> orFilters = f.Split(new string[] { "||" }, StringSplitOptions.None).ToList();

					foreach (string i in orFilters)
					{
						if (i.Contains("Status"))
						{
							string theFilter = i.Replace("(", "").Replace(")", "");
							var matches = regex.Matches(theFilter);

							foreach (object item in matches)
							{
								string filterValue = item.ToString().Replace("\"", "");

								if (filterValue == "ageditems")
								{
									filters = this.UseOrInitOrExpression(filters, wit => wit.ModifiedDate <= DateTime.Now.AddDays(numberofDays));
								}

								if (filterValue == "open")
								{
									filters = this.UseOrInitOrExpression(filters, wit => wit.ClosedDate == null);
								}
							}
						}
						else
						{
							//Check with double quotes or single quotes in the like statement.
							Match likeMatch = Regex.Match(i, "like\\((.*?)[,'%](.*?)%'\\)|like\\((.*?)[,\"%](.*?)%\"\\)");
							string theFilter = string.Empty;

							theFilter = likeMatch.Length > 0 ? likeMatch.Captures[0].ToString() : i.Trim(new char[] { '(', ')', ' ' });

							if (string.IsNullOrEmpty(tempForNewFilter))
							{
								tempForNewFilter = string.Concat("(", theFilter, ")");
							}
							else
							{
								if (!string.IsNullOrEmpty(theFilter))
								{
									tempForNewFilter = string.Concat(tempForNewFilter, "||", string.Concat("(", theFilter, ")"));
								}
							}
						}
					}

					if (filters != null)
					{
						predicate = predicate.And(filters);
					}

					if (string.IsNullOrEmpty(newFilter))
					{
						newFilter = tempForNewFilter;
					}
					else
					{
						if (!string.IsNullOrEmpty(tempForNewFilter))
						{
							newFilter = string.Concat(newFilter, "&&", tempForNewFilter);
						}
					}
				}
			}

			using (var tcdc = new TrackingCenterDataContext())
			{
				iqWILs = tcdc.GetWorkitemList()
							.Where(predicate)
							.Select(i => new Item
							{
								AdvisorID = i.AdvisorID,
								AdvisorVisible = i.AdvisorVisible,
								AgentID = i.AgentID,
								BDID = i.BDID,
								BundleID = i.BundleID,
								ClientID = i.ClientID,
								ClosedDate = i.ClosedDate,
								CreatedDate = i.CreatedDate,
								Description = i.Description,
								ExternalId = i.ExternalId,
								Id = i.Id,
								InternalStatus = i.InternalStatus,
								IsEsignatureRequested = i.IsEsignatureRequested,
								IsParent = i.IsParent,
								ModifiedDate = i.ModifiedDate,
								ObjectName = i.ObjectName,
								ParentExternalID = i.ParentExternalID,
								ParentItemID = i.ParentItemID,
								RankOrder = i.RankOrder,
								ResponseAllowed = i.ResponseAllowed,
								ResponseRequired = i.ResponseRequired,
								ShortExternalDescription = i.ShortExternalDescription,
								SsoGuidId = i.SsoGuidId,
								Status = i.Status,
								SubmitToBD = i.SubmitToBD,
								UploadAllowed = i.UploadAllowed,
								WorkItemType = i.WorkItemType,
								AccountID = i.AccountID,
								CustodialAccountNumber = i.CustodialAccountNumber,
								Custodian = i.Custodian,
								FundingAccountNumber = i.FundingAccountNumber,
								ItemAssociationActive = i.ItemAssociationActive,
								ItemAssociationID = i.ItemAssociationID,
								ItemName = i.ItemName
							});

				if (!string.IsNullOrEmpty(newFilter))
				{
					iqWILs = this.ApplyFilters<Item>(string.Concat("(", newFilter, ")"), null)(iqWILs);
				}

				itemList = iqWILs.ToList();
			}

			if (itemList != null && itemList.Any())
			{
				foreach (var item in itemList)
				{
					var workItem = new WorkItem3();
					workItems.Add(workItem.CopyFrom(item));
				}
			}

			response.WorkItems = workItems != null && workItems.Any() ? workItems.ToArray() : Array.Empty<WorkItem3>();
			return response;
		}

		public ClosedAgedOpenWorkItemResponse CloseAgedOpenNNumber()
		{
			ClosedAgedOpenWorkItemResponse response = new ClosedAgedOpenWorkItemResponse();

			using (var tcdc = new TrackingCenterDataContext())
			{
				response = tcdc.CloseAgedOpenNNumber().FirstOrDefault();
			}

			return response;
		}

		public bool DeleteEnvelope(int envelopeId)
		{
			if (envelopeId <= 0)
			{
				throw new ArgumentException($"Invalid EnvelopeId:{envelopeId}");
			}

			bool returnValue;
			int result = 0;

			using (var tcdc = new TrackingCenterUpdateDataContext())
			{
				result = tcdc.DeleteEnvelope(envelopeId);
			}

			returnValue = result > 0;
			return returnValue;
		}

		public BAWSearchWorkItemResponse BAWSearchWorkItem(BAWSearchWorkItemRequest request)
		{
			var response = new BAWSearchWorkItemResponse();
			var workItems = new List<WorkItem3>();
			List<WorkItemAssociation> itemAssociations = null;
			Expression<Func<GSS.Entities.DB.Item, bool>> predicate = PredicateBuilder.True<GSS.Entities.DB.Item>();
			int totalItemsCount = 0;

			predicate = predicate.And(item => item.ItemAssociationActive == true);

			if (request.ItemTypes != null && request.ItemTypes.Count > 0)
			{
				string[] itemTypes = request.ItemTypes.Select(item => item.ToString()).ToArray();
				predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.Item>(itemTypes, WorkItemFilters.ItemType.ToString()));
			}

			if (!string.IsNullOrWhiteSpace(request.ExternalID))
			{
				predicate = predicate.And(item => item.ExternalId == request.ExternalID);
			}

			if (!string.IsNullOrWhiteSpace(request.AgentID))
			{
				if (request.AgentID.Contains(SEPARATOR))
				{
					predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.Item>(request.AgentID.Split(SEPARATOR), WorkItemFilters.AgentId.ToString()));
				}
				else
				{
					predicate = predicate.And(item => item.AgentID == request.AgentID);
				}
			}

			if (!string.IsNullOrWhiteSpace(request.ClientID))
			{
				if (request.ClientID.Contains(SEPARATOR))
				{
					predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.Item>(request.ClientID.Split(SEPARATOR), WorkItemFilters.ClientId.ToString()));
				}
				else
				{
					predicate = predicate.And(item => item.ClientID == request.ClientID);
				}
			}

			if (!string.IsNullOrWhiteSpace(request.AccountID))
			{
				if (request.AccountID.Contains(SEPARATOR))
				{
					predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.Item>(request.AccountID.Split(SEPARATOR), WorkItemFilters.AccountId.ToString()));
				}
				else
				{
					predicate = predicate.And(item => item.AccountID == request.AccountID);
				}
			}

			if (!string.IsNullOrWhiteSpace(request.CustodialAccountNumber))
			{
				if (request.CustodialAccountNumber.Contains(SEPARATOR))
				{
					predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.Item>(request.CustodialAccountNumber.Split(SEPARATOR), WorkItemFilters.CustodialAccountNumber.ToString()));
				}
				else
				{
					predicate = predicate.And(item => item.CustodialAccountNumber == request.CustodialAccountNumber);
				}
			}

			if (!string.IsNullOrWhiteSpace(request.Custodian))
			{
				if (request.Custodian.Equals(UNKNOWN, StringComparison.OrdinalIgnoreCase))
				{
					predicate = predicate.And(item => item.Custodian == null);
				}
				else
				{
					predicate = predicate.And(item => item.Custodian == request.Custodian);
				}
			}

			if (request.WorkItemDescriptions != null && request.WorkItemDescriptions.Count > 0)
			{
				predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.Item>(request.WorkItemDescriptions.ToArray(), WorkItemFilters.Description.ToString()));
			}

			if (request.IsParentItemsOnly.HasValue)
			{
				predicate = predicate.And(item => item.IsParent == request.IsParentItemsOnly);
			}

			if (request.IsOpenItemsOnly.HasValue && request.IsOpenItemsOnly.Value)
			{
				predicate = predicate.And(item => item.ClosedDate == null);
			}

			if (request.IsClosedItemsOnly.HasValue && request.IsClosedItemsOnly.Value)
			{
				predicate = predicate.And(item => item.ClosedDate != null);
			}

			if (!string.IsNullOrWhiteSpace(request.CreatedDate))
			{
				Tuple<DateTime, DateTime> datesSearch = this.GetBetweenValues(request.CreatedDate);

				if (datesSearch != null)
				{
					predicate = predicate.And(item => item.CreatedDate >= datesSearch.Item1 && item.CreatedDate <= datesSearch.Item2);
				}
			}

			if (!string.IsNullOrWhiteSpace(request.ModifiedDate))
			{
				Tuple<DateTime, DateTime> datesSearch = this.GetBetweenValues(request.ModifiedDate);

				if (datesSearch != null)
				{
					predicate = predicate.And(item => item.ModifiedDate >= datesSearch.Item1 && item.ModifiedDate <= datesSearch.Item2);
				}
			}

			if (!string.IsNullOrWhiteSpace(request.ClosedDate))
			{
				Tuple<DateTime, DateTime> datesSearch = this.GetBetweenValues(request.ClosedDate);

				if (datesSearch != null)
				{
					predicate = predicate.And(item => item.ClosedDate >= datesSearch.Item1 && item.ClosedDate <= datesSearch.Item2);
				}
			}

			DateTime previousPageLastItemModifiedDateTime;

			if (!request.PreviousPageLastItemId.HasValue || request.PreviousPageLastItemId.Value <= 0)
			{
				request.PreviousPageLastItemId = int.MaxValue;
				previousPageLastItemModifiedDateTime = DateTime.Now;
			}
			else
			{
				if (!DateTime.TryParseExact(request.PreviousPageLastItemModifiedDateTime, DATETIMEFORMAT,
								null, DateTimeStyles.None, out previousPageLastItemModifiedDateTime))
				{
					throw new ApplicationException($"Unable to convert {request.PreviousPageLastItemModifiedDateTime} to a date and time specified {DATETIMEFORMAT}.");
				}
			}

			predicate = predicate.And(item => item.ModifiedDate < previousPageLastItemModifiedDateTime
										|| (item.ModifiedDate == previousPageLastItemModifiedDateTime && item.Id < request.PreviousPageLastItemId.Value));

			using (var context = new TrackingCenterDataReadOnly())
			{
				 var tempItems = context.GetWorkitemList()
										.Where(predicate)
										.Select(i => new Item
										{
											AgentID = i.AgentID,
											ClientID = i.ClientID,
											ClosedDate = i.ClosedDate,
											CreatedBy = i.CreatedBy,
											CreatedDate = i.CreatedDate,
											Description = i.Description,
											ExternalId = i.ExternalId,
											Id = i.Id,
											InternalStatus = i.InternalStatus,
											IsParent = i.IsParent,
											ObjectName = i.ObjectName,
											Status = i.Status,
											WorkItemType = i.WorkItemType,
											AccountID = i.AccountID,
											CustodialAccountNumber = i.CustodialAccountNumber,
											ItemName = i.ItemName,
											Custodian = i.Custodian,
											ItemAssociationActive = i.ItemAssociationActive,
											ModifiedDate = i.ModifiedDate,
											ParentExternalID = i.ParentExternalID,
											ShortExternalDescription = i.ShortExternalDescription,
											AdvisorVisible = i.AdvisorVisible,
											ResponseRequired = i.ResponseRequired,
											BundleID = i.BundleID
										})
										.OrderByDescending(item => item.ModifiedDate)
										.ThenByDescending(item => item.Id)
										.Take(request.PageSize)
										.ToList();

				if (tempItems != null && tempItems.Any())
				{
					foreach (var iqItem in tempItems)
					{
						if (iqItem != null && iqItem.Id > 0)
						{
							var workItem = new WorkItem3();
							var item = new GSS.Entities.DB.Item();
							itemAssociations = new List<WorkItemAssociation>();

							if (!workItems.Any(x => x.Id == iqItem.Id))
							{
								item = iqItem;

								if (item != null && item.Id > 0)
								{
									workItem.CopyFrom(item);
									workItem.Id = item.Id;
									workItem.CreatedBy = HttpUtility.HtmlEncode(workItem.CreatedBy);

									workItem.ItemAssociations = tempItems.Where(it => it.Id == item.Id).Select(ia => new WorkItemAssociation
									{
										AccountID = ia.AccountID,
										CustodialAccountNumber = ia.CustodialAccountNumber,
										ItemName = ia.ItemName,
										Custodian = ia.Custodian,
										IsActive = true,
									}).ToArray();

									workItem.CreatedDate = workItem.CreatedDate.Value.ToUniversalTime();
									workItem.ModifiedDate = item.ModifiedDate.Value.ToUniversalTime();
									workItem.ClosedDate = workItem.ClosedDate.HasValue ? item.ClosedDate.Value.ToUniversalTime() : item.ClosedDate;
									workItems.Add(workItem);
								}
							}
						}
					}
				}
			}

			response.WorkItems = workItems.Count() > 0 ? workItems.OrderByDescending(item => item.ModifiedDate).ThenByDescending(item => item.Id).ToList() : new List<WorkItem3>();

			if (response.WorkItems.Count == 0)
			{
				response.CurrentPageLastItemId = int.MaxValue;
				response.CurrentPageLastItemModifiedDateTime = DateTime.Now.ToString("DATETIMEFORMAT");
			}
			else
			{
				response.CurrentPageLastItemId = response.WorkItems.LastOrDefault().Id;
				response.CurrentPageLastItemModifiedDateTime = this.ConvertDateTimeToPST(response.WorkItems.LastOrDefault().ModifiedDate.Value).ToString(DATETIMEFORMAT);
			}

			return response;
		}

		#region "Private Methods"
		private WorkItem3 UpdateWorkItem<T>(WorkItem3 item, int? rankOrder, string parentExternalID = "", [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
		{
			string fullClassName = $"{MethodBase.GetCurrentMethod().ReflectedType.FullName}.{MethodBase.GetCurrentMethod().Name}";
			int itemId = 0;
			string externalId = item.ExternalId;

			try
			{
				using (var tcindc = new TrackingCenterUpdateDataContext())
				{
					itemId = tcindc.UpdateItem(item.OldExternalID,
												ref externalId,
												item.BDID,
												item.AdvisorID,
												item.AgentID,
												item.ClientID,
												item.ObjectName,
												item.Description,
												item.Status,
												item.InternalStatus,
												HttpUtility.HtmlDecode(item.CreatedBy),
												item.AdvisorVisible,
												item.ResponseAllowed,
												item.ResponseRequired,
												item.IsClosable,
												item.Source,
												item.CreatedDate,
												item.ClosedDate,
												item.BundleID,
												item.ShortExternalDescription,
												rankOrder,
												item.IsParent,
												parentExternalID,
												item.IsReopen,
												item.IsEsignatureRequested ?? null,
												item.SsoGuidId,
												item.SubmitToBD ?? null);
				}
			}
			catch (Exception ex)
			{
				log.Error($"{fullClassName} - CallerMemberName:{callerName} ItemRequest:{item} ParentExternalID:{parentExternalID}", ex);
				externalId = string.Empty;
			}

			item.Id = itemId;
			item.ExternalId = externalId;

			return item;
		}

		private Item GetWorkItemInfo(Expression<Func<Item, bool>> predicate)
		{
			using (var tcdc = new TrackingCenterDataContext())
			{
				return tcdc.GetWorkitemList()
							.Where(predicate)
							.Select(i => new Item
							{
								AdvisorID = i.AdvisorID,
								AdvisorVisible = i.AdvisorVisible,
								AgentID = i.AgentID,
								BDID = i.BDID,
								BundleID = i.BundleID,
								ClientID = i.ClientID,
								ClosedDate = i.ClosedDate,
								CreatedDate = i.CreatedDate,
								Description = i.Description,
								DocuSignID = i.DocuSignID,
								EnvelopeType = i.EnvelopeType,
								EsigStatus = i.EsigStatus,
								eSigType = i.eSigType,
								ExternalId = i.ExternalId,
								Id = i.Id,
								InternalStatus = i.InternalStatus,
								IsEsignatureRequested = i.IsEsignatureRequested,
								IsParent = i.IsParent,
								KitName = i.KitName,
								ModifiedDate = i.ModifiedDate,
								ObjectName = i.ObjectName,
								ParentExternalID = i.ParentExternalID,
								ParentItemID = i.ParentItemID,
								RankOrder = i.RankOrder,
								ResponseAllowed = i.ResponseAllowed,
								ResponseRequired = i.ResponseRequired,
								ShortExternalDescription = i.ShortExternalDescription,
								SsoGuidId = i.SsoGuidId,
								Status = i.Status,
								SubmitToBD = i.SubmitToBD,
								UploadAllowed = i.UploadAllowed,
								WorkItemType = i.WorkItemType,
								AccountID = i.AccountID,
								CustodialAccountNumber = i.CustodialAccountNumber,
								Custodian = i.Custodian,
								FundingAccountNumber = i.FundingAccountNumber,
								ItemAssociationActive = i.ItemAssociationActive,
								ItemAssociationID = i.ItemAssociationID,
								ItemName = i.ItemName
							}).FirstOrDefault();
			}
		}

		private static string GetSourceCode(Enum value)
		{
			FieldInfo fi = value.GetType().GetField(value.ToString());
			var attributes = (SourceCode[])fi.GetCustomAttributes(typeof(SourceCode), false);
			return attributes != null && attributes.Length > 0 ? attributes[0].Code : value.ToString();
		}

		private Func<IQueryable<T>, IQueryable<T>> ApplyFilters<T>(string filter, string[] sortOrders)
		{
			try
			{
				var entity = CriteriaBuilder.Query<T>(filter, sortOrders);
				return entity;
			}
			catch (Exception ex)
			{
				log.Error("TrackingCenterProcessor.ApplyFilters", ex);
			}

			return null;
		}

		/// <summary>
		/// Adds initial information in the database about files being uploaded. These are listed with a "pending" status.
		/// </summary>
		private int AddItemUpload(string processID, string fileName, string createdBy)
		{
			int uploadID = 0;

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				uploadID = tcindc.AddItemUpload(processID, fileName, createdBy);
			}

			return uploadID;
		}

		/// <summary>
		/// Updates information stored in the database about files being uploaded.
		/// </summary>
		private int UpdateItemUpload(int uploadID, string fileName, string status)
		{
			int rtnstatus = 0;

			using (var tcindc = new TrackingCenterUpdateDataContext())
			{
				rtnstatus = tcindc.UpdateItemUpload(uploadID, fileName, status);
			}

			if (rtnstatus <= 0)
			{
				log.Error($"TrackingCenterProcessor.UpdateItemUpload - Item upload status update failed. uploadID{uploadID} fileName:{fileName} uploadStatus:{status}");
			}

			return rtnstatus;
		}

		/// <summary>
		/// Calls DSConnect Service's GetOCRData
		/// </summary>
		private Dictionary<string, string> GetOCRData(string envelopID)
		{
			string fullClassName = string.Concat(MethodInfo.GetCurrentMethod().ReflectedType.FullName, ".", MethodBase.GetCurrentMethod().Name);
			Dictionary<string, string> data = null;

			try
			{
				var DSServiceProxy = new DSConnectServiceProxy.DSConnectServiceProxy(DSCONNECTION_SERVICE);
				data = DSServiceProxy.GetOCRData(new string[] { envelopID });
			}
			catch (Exception ex)
			{
				log.Error($"{fullClassName} - Error occurred while fetching OCR form data from DocuSign. EnvelopeId:{envelopID}", ex);
			}

			return data;
		}

		/// <summary>
		/// Populating WebStatus and SortOrderByStatus fields for backwards compatibility. To be removed with the fields in a future version.
		/// </summary>
		/// <param name="oneWorkItem"></param>
		private void PopulateWebAndSortOrderByStatuses(WorkItem3 oneWorkItem)
		{
			oneWorkItem.WebStatus = ((oneWorkItem.ClosedDate == null || oneWorkItem.ClosedDate.Value > DateTime.Now.AddDays(-7)) ? "open+closed<7" : "closed>7");
			oneWorkItem.SortOrderByStatus = ((oneWorkItem.ClosedDate == null && oneWorkItem.ResponseRequired)
												? 1 : (oneWorkItem.ClosedDate == null
													? 2 : (oneWorkItem.ClosedDate.Value > DateTime.Now.AddDays(-7) ? 4 : 5)));
		}

		/// <summary>
		/// Populating UploadAllowed field for backwards compatibility and for non D items. Do not remove
		/// </summary>
		/// <param name="oneWorkItem"></param>
		private void PopulateUploadAllowed(WorkItem3 oneWorkItem)
		{
			oneWorkItem.UploadAllowed = (oneWorkItem.ClosedDate == null &&
											((oneWorkItem.eSigType == "PrintOnly" || oneWorkItem.ResponseAllowed && !oneWorkItem.IsParent)
											|| oneWorkItem.ExternalId.StartsWith("N")));
		}

		/// <summary>
		/// Populating WebStatus field for backwards compatibility. To be removed with the field in a future version.
		/// </summary>
		/// <param name="oneWorkItemType"></param>
		private void PopulateWebStatus(WorkItemType1 oneWorkItemType)
		{
			oneWorkItemType.WebStatus = (oneWorkItemType.CloseDate == null || oneWorkItemType.CloseDate.Value > DateTime.Now.AddDays(-7)) ? "open+closed<7" : "closed>7";
		}

		/// <summary>
		/// Used to check if expression is initialized before trying to OR the two expressions. 
		/// If expression 1 is null, then the method will just return expression 2. 
		/// If expression 1 is not null, or the two expressions and return the resulting expression.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="expr1"></param>
		/// <param name="expr2"></param>
		/// <returns></returns>
		private Expression<Func<T, bool>> UseOrInitOrExpression<T>(Expression<Func<T, bool>> expr1, Expression<Func<T, bool>> expr2)
		{
			if (expr1 == null)
			{
				return expr2;
			}

			return expr1.Or(expr2);
		}

		/// <summary>
		/// Populating WebStatus and SortOrderByStatus fields for backwards compatibility. To be removed with the fields in a future version.
		/// </summary>
		/// <param name="item"></param>
		private void PopulateWebAndSortOrderByStatuses(Item item)
		{
			item.WebStatus = ((item.ClosedDate == null || item.ClosedDate.Value > DateTime.Now.AddDays(-7)) ? "open+closed<7" : "closed>7");
			item.SortOrderByStatus = ((item.ClosedDate == null && item.ResponseRequired)
												? 1 : (item.ClosedDate == null
													? 2 : (item.ClosedDate.Value > DateTime.Now.AddDays(-7) ? 4 : 5)));
		}

		/// <summary>
		/// Populating UploadAllowed field for backwards compatibility and for non D items. Do not remove
		/// </summary>
		/// <param name="item"></param>
		private void PopulateUploadAllowed(Item item)
		{
			item.UploadAllowed = (item.ClosedDate == null &&
									(item.eSigType == "PrintOnly" || item.ResponseAllowed && !item.IsParent));
		}

		public Tuple<DateTime, DateTime> GetBetweenValues(string dateValues)
		{
			if (string.IsNullOrWhiteSpace(dateValues))
			{
				return null;
			}

			string[] values = dateValues.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

			if (values.Length != 2)
			{
				return null;
			}

			DateTime? @from = ToDate(values[0]);
			DateTime? to = ToDate(values[1]);

			if (!@from.HasValue || !to.HasValue)
			{
				return null;
			}

			return new Tuple<DateTime, DateTime>(@from.Value.Date, to.Value.Date.AddDays(1)); // Ensure the "to" value is inclusive.
		}

		private DateTime? ToDate(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return null;
			}

			DateTime date;

			if (DateTime.TryParseExact(value, dateFormats, usaCultureInfo, DateTimeStyles.None, out date))
			{
				return date;
			}

			if (DateTime.TryParse(value, out date))
			{
				return date;
			}

			return null;
		}

		private DateTime ConvertDateTimeToPST(DateTime dateTime)
		{
			return TimeZoneInfo.ConvertTimeFromUtc(dateTime, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
		}

		#endregion

		void IDisposable.Dispose()
		{
		}
	}
}
