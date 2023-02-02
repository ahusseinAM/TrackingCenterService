using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using GSS.Entities;
using GSS.TrackingCenter.BusinessProcessor.Utility;
using GSS.TrackingCenter.DataAccess;
using GSS.Utility;

namespace GSS.TrackingCenter.BusinessProcessor
{
	public class TrackingCenterBusinessProcessor : IDisposable
	{
		private static readonly string[] dateFormats = { "yyyy/MM/dd", "yyyy/M/d", "M/d/yyyy", "MM/dd/yyyy" };
		private static readonly CultureInfo usaCultureInfo = CultureInfo.CreateSpecificCulture("en-US");
		private static readonly char SEPARATOR = '|';
		protected const string UNKNOWN = "UNKNOWN";

		public TrackingCenterBusinessProcessor() : base()
		{
		}

		public SearchWorkItemResponse SearchWorkItem(SearchWorkItemRequest request)
		{
			var response = new SearchWorkItemResponse();
			var workItems = new List<WorkItem3>();
			List<WorkItemAssociation> itemAssociations = null;
			Expression<Func<GSS.Entities.DB.SerachWorkItem, bool>> predicate = PredicateBuilder.True<GSS.Entities.DB.SerachWorkItem>();
			int totalItemsCount = 0;

			predicate = predicate.And(item => item.IsActive == true);

			if (request.ItemTypes != null && request.ItemTypes.Count > 0)
			{
				string[] itemTypes = request.ItemTypes.Select(item => item.ToString()).ToArray();
				predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.SerachWorkItem>(itemTypes, WorkItemFilters.ItemType.ToString()));
			}

			if (!string.IsNullOrWhiteSpace(request.ExternalID))
			{
				predicate = predicate.And(item => item.ExternalId == request.ExternalID);
			}

			if (!string.IsNullOrWhiteSpace(request.AgentID))
			{
				if (request.AgentID.Contains(SEPARATOR))
				{
					predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.SerachWorkItem>(request.AgentID.Split(SEPARATOR), WorkItemFilters.AgentId.ToString()));
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
					predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.SerachWorkItem>(request.ClientID.Split(SEPARATOR), WorkItemFilters.ClientId.ToString()));
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
					predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.SerachWorkItem>(request.AccountID.Split(SEPARATOR), WorkItemFilters.AccountId.ToString()));
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
					predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.SerachWorkItem>(request.CustodialAccountNumber.Split(SEPARATOR), WorkItemFilters.CustodialAccountNumber.ToString()));
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
				predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.SerachWorkItem>(request.WorkItemDescriptions.ToArray(), WorkItemFilters.Description.ToString()));
			}

			if (request.IsParentItemsOnly.HasValue)
			{
				predicate = predicate.And(item => item.IsParent == request.IsParentItemsOnly);
			}

			if (request.IsOpenItemsOnly.HasValue && request.IsOpenItemsOnly.Value)
			{
				predicate = predicate.And(item => item.CloseDate == null);
			}

			if (request.IsClosedItemsOnly.HasValue && request.IsClosedItemsOnly.Value)
			{
				predicate = predicate.And(item => item.CloseDate != null);
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
					predicate = predicate.And(item => item.CloseDate >= datesSearch.Item1 && item.CloseDate <= datesSearch.Item2);
				}
			}

			if (request.Statuses != null && request.Statuses.Count > 0)
			{
				predicate = predicate.And(ExpressionBuilder.LINQExpression<GSS.Entities.DB.SerachWorkItem>(request.Statuses.ToArray(), WorkItemFilters.Status.ToString()));
			}

			using (var context = new TrackingCenterReadOnlyDataContext())
			{
				totalItemsCount = context.GetWorkItemsCount(predicate);

				if (totalItemsCount > 0)
				{
					var tempItems = context.GetSearchWorkItemList(predicate, request.PageNumber, request.PageSize);

					if (tempItems != null && tempItems.Any())
					{
						foreach (var iqItem in tempItems)
						{
							if (iqItem != null && iqItem.ItemID > 0)
							{
								var workItem = new WorkItem3();
								var item = new GSS.Entities.DB.SerachWorkItem();
								itemAssociations = new List<WorkItemAssociation>();

								if (!workItems.Any(x => x.Id == iqItem.ItemID))
								{
									item = iqItem;

									if (item != null && item.ItemID > 0)
									{
										workItem.CopyFrom(item);
										workItem.Id = item.ItemID;
										workItem.CreatedBy = HttpUtility.HtmlEncode(workItem.CreatedBy);

										workItem.ItemAssociations = tempItems.Where(it => it.ItemID == item.ItemID).Select(ia => new WorkItemAssociation
										{
											AccountID = ia.AccountID,
											CustodialAccountNumber = ia.CustodialAccountNumber,
											ItemName = ia.ItemName,
											Custodian = ia.Custodian,
											IsActive = ia.IsActive ?? true
										}).ToArray();

										if (request.Source == WorkItemSource.BPM)
										{
											workItem.CreatedDate = workItem.CreatedDate.Value.ToUniversalTime();
											workItem.ModifiedDate = workItem.ModifiedDate.HasValue ? item.ModifiedDate.ToUniversalTime() : item.ModifiedDate;
											workItem.ClosedDate = workItem.ClosedDate.HasValue ? item.CloseDate.Value.ToUniversalTime() : item.CloseDate;
										}

										workItems.Add(workItem);
									}
								}
							}
						}
					}
				}
			}

			response.WorkItems = workItems.Count() > 0 ? workItems.OrderByDescending(item => item.ModifiedDate).ToList() : new List<WorkItem3>();
			response.TotalCount = totalItemsCount;
			response.CurrentPage = request.PageNumber;
			response.TotalPages = (int)Math.Ceiling(totalItemsCount / (double)request.PageSize);
			response.PageSize = request.PageSize;
			response.HasPrevious = response.CurrentPage > 1;
			response.HasNext = response.CurrentPage < response.TotalPages;
			return response;
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

		void IDisposable.Dispose()
		{
		}
	}
}
