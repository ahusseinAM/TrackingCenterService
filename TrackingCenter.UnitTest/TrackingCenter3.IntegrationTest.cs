using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GSS.TrackingCenterService;
using GSS.Entities;
using System.Drawing;
using GSS.Utility;
using System.IO;
using System.Text;

namespace GSS.TrackingCenter.IntegrationTest
{
	[TestClass]
	public class TrackingCenter3
	{
		public const string USERID = "Version 3.0";

		[TestMethod]
		public void PdfDocumentUpload()
		{
			string userId = string.Empty;
			string userName = string.Empty;
			string fileName = "SampleDocument.pdf";
			string externalId = "N000011481";
			string status = "New";
			byte[] fileContent = File.ReadAllBytes(@"C:\TestDocsUpload\W0TT38_AccountEstDocument.pdf");
			string content = Convert.ToBase64String(fileContent);
			WQDocumentUploadResponse response = new WQDocumentUploadResponse();
			response = TrackingCenterClient.Clientv3.DocumentUpload(userId, userName, fileName, externalId, status, fileContent);

			if (response != null && !response.Success)
			{
				Assert.IsFalse(response.Success, "Document Upload operation has been failed due to error.");
			}
		}

		[TestMethod]
		public void JpgDocumentUpload()
		{
			string userId = string.Empty;
			string userName = string.Empty;
			string fileName = "SampleImage.jpg";
			string externalId = "D007688648";
			string status = "New";
			byte[] fileContent = File.ReadAllBytes(@"C:\Users\Public\Pictures\Sample Pictures\Chrysanthemum.jpg");
			string content = Convert.ToBase64String(fileContent);
			WQDocumentUploadResponse response = new WQDocumentUploadResponse();
			response = TrackingCenterClient.Clientv3.DocumentUpload(userId, userName, fileName, externalId, status, fileContent);

			if (response != null && !response.Success)
			{
				Assert.IsFalse(response.Success, "Document Upload operation has been failed due to error.");
			}
		}

		[TestMethod]
		public void CreateStandAloneItem()
		{
			//First check Max(ItemID) in Item table of TC database, then MAX(ItemID) + 1 and set the value in processID variables.
			string processID = "B4271879";

			WorkItem3 workItem3 = new WorkItem3()
			{
				AdvisorVisible = true,
				AgentID = "AG1234",
				ClientID = "CA1212",
				CreatedBy = USERID,
				CreatedDate = DateTime.Now,
				Description = "Withdrawal Request",
				eSigType = "Print Only",
				ExternalId = processID,
				Id = 3291065,
				InternalStatus = "Request Received",
				IsClosable = false,
				ItemAssociations = new WorkItemAssociation[1]
				{
					new WorkItemAssociation
					{
						ID = 0,
						ItemID = 0,
						ExternalID = processID,
						AccountID = "A1234",
						CustodialAccountNumber = "N8U6677767",
						FundingAccountNumber = "N8U6689898",
						Custodian = "PRS",
						ItemName = "Test Account Name",
						CreatedDate = DateTime.Now,
						IsActive = true,
					}
				},
				KitName = "Test KitName",
				ModifiedDate = DateTime.Now,
				ObjectName = "Test Client Name",
				ResponseAllowed = true,
				ResponseRequired = true,
				Source = WorkItemSource.TrackingCenter.ToString(),
				Status = "Request Received",
				UploadAllowed = true,
				WorkItemType = "Withdrawal Request"
			};

			var response = TrackingCenterClient.Clientv3.UpdateWorkItem(processID, USERID, workItem3);
			Assert.IsNotNull(response.WorkItem);
			Assert.IsNotNull(response.WorkItem.Id);
			Assert.IsNotNull(response.WorkItem.ExternalId);
		}

		[TestMethod]
		public void CreateParentItem()
		{
			string processID = "B4271894";
			string childProcessID1 = "B4271895";
			string childProcessID2 = "B4271896";

			ParentWorkItem parentWorkItem = new ParentWorkItem()
			{
				AdvisorVisible = true,
				AgentID = "AG5638",
				BundleID = 2493441,
				ClientID = "CA78X7",
				CreatedBy = USERID,
				CreatedDate = DateTime.Now,
				Description = "New Account Bundle",
				eSigType = "Print Only",
				ExternalId = processID,
				Id = 0,
				InternalStatus = " In Process",
				ItemAssociations = new WorkItemAssociation[1]
				{
					new WorkItemAssociation
					{
						ExternalID = processID,
						Custodian = "GNW",
						ItemName = "Sample Trust",
						ItemStatus = ItemStatus.InProcess,
						CreatedDate = DateTime.Now,
						IsActive = true,
					}
				},
				KitName = "Test KitName",
				ModifiedDate = DateTime.Now,
				ObjectName = "Graham, Nancy O",
				ResponseAllowed = true,
				ResponseRequired = true,
				Source = "BPM",
				Status = "In Process",
				UploadAllowed = true,
				WorkItemType = "New Account Bundle",
				IsParent = true,
				ChildWorkItems = new ChildWorkItem[2]
				{
					new ChildWorkItem
					{
						AdvisorVisible = true,
						AgentID = "AG5638",
						ClientID = "CA78X7",
						CreatedBy = USERID,
						CreatedDate = DateTime.Now,
						Description = "New Account Application",
						eSigType = "Print Only",
						ExternalId = childProcessID1,
						Id = 0,
						InternalStatus = "Request Received",
						IsClosable = false,
						KitName = "Test KitName",
						ModifiedDate = DateTime.Now,
						ObjectName = "Sawant2, Ketan2",
						ResponseAllowed = true,
						ResponseRequired = true,
						ShortExternalDescription = "New Account Application 3",
						SortOrderByStatus = null,
						Source = "BPM",
						Status = "Request Received",
						UploadAllowed = true,
						WorkItemType = "New Account Application",
						RankOrder = 1,
						ParentExternalID = Verhoeff.GenerateID(processID),
						ItemAssociations = new WorkItemAssociation[1]
						{
							new WorkItemAssociation
							{
								ExternalID = Verhoeff.GenerateID(childProcessID1),
								AccountID = "ATEST1",
								CustodialAccountNumber = "ACNUMBER1",
								Custodian = "GNW",
								ItemName = "Sample Trust",
								ItemStatus = ItemStatus.InProcess,
								CreatedDate = DateTime.Now,
								IsActive = true,
							}
						},
					},
					new ChildWorkItem
					{
						AdvisorVisible = true,
						AgentID = "AG5638",
						ClientID = "CA78X7",
						CreatedBy = USERID,
						CreatedDate = DateTime.Now,
						Description = "New Account Application",
						eSigType = "Print Only",
						ExternalId = childProcessID2,
						Id = 0,
						InternalStatus = "Request Received",
						IsClosable = false,
						KitName = "Test KitName",
						ModifiedDate = DateTime.Now,
						ObjectName = "Andrea L Shields SEP IRA",
						ResponseAllowed = true,
						ResponseRequired = true,
						ShortExternalDescription = "New Account Application 3",
						Source = "BPM",
						Status = "Request Received",
						UploadAllowed = true,
						WorkItemType = "New Account Application",
						RankOrder = 1,
						ParentExternalID = Verhoeff.GenerateID(processID),
						ItemAssociations = new WorkItemAssociation[1]
						{
							new WorkItemAssociation
							{
								ExternalID = Verhoeff.GenerateID(childProcessID2),
								AccountID = "ATEST2",
								CustodialAccountNumber = "ACNUMBER2",
								Custodian = "GNW",
								ItemName = "Sample Trust2",
								ItemStatus = ItemStatus.InProcess,
								CreatedDate = DateTime.Now,
								IsActive = true,
							}
						},
					}

				}
			};
			var response = TrackingCenterClient.Clientv3.UpdateParentWorkItem(processID, USERID, parentWorkItem);

			Assert.IsNotNull(response);
			Assert.IsNotNull(response.Data);
		}

		[TestMethod]
		public void CreateNItem()
		{
			//First check Max(ItemID) in Item table of TC database, then MAX(ItemID) + 1 and set the value in processID variables.
			string processID = "N4271907";

			WorkItem3 workItem3 = new WorkItem3()
			{
				AdvisorVisible = true,
				AgentID = "AG6675",
				ClientID = "W0TT39",
				CreatedBy = USERID,
				CreatedDate = DateTime.Now,
				Description = "New Account Paperwork Submission",
				eSigType = "Print Only",
				ExternalId = processID,
				Id = 3291065,
				InternalStatus = "Request Received",
				IsClosable = false,
				ItemAssociations = new WorkItemAssociation[1]
				{
					new WorkItemAssociation
					{
						ID = 0,
						Custodian = "GNW",
						CreatedDate = DateTime.Now,
						IsActive = true,
					}
				},
				KitName = "Test KitName",
				ModifiedDate = DateTime.Now,
				ObjectName = "Test Client Name",
				ResponseAllowed = true,
				ResponseRequired = true,
				Source = WorkItemSource.NewAccount.ToString(),
				Status = "Request Received",
				UploadAllowed = true,
				WorkItemType = "New Account Paperwork Submission"
			};

			var response = TrackingCenterClient.Clientv3.UpdateWorkItem(processID, USERID, workItem3);
			Assert.IsNotNull(response.WorkItem);
			Assert.IsNotNull(response.WorkItem.Id);
			Assert.IsNotNull(response.WorkItem.ExternalId);
		}

		[TestMethod]
		public void CreateChildItem()
		{
			//First check Max(ItemID) in Item table of TC database, then add MAX(ItemID) + 1 and set the value in processID variables.
			string processID = "B3598166"; 

			ChildWorkItem childWorkItem = new ChildWorkItem()
			{
				AdvisorID = "",
				AdvisorVisible = true,
				AgentID = "AGAKCA",
				ClientID = "CA1XM0",
				CreatedBy = USERID,
				CreatedDate = DateTime.Now,
				Description = "New Account Application",
				eSigType = "Print Only",
				ExternalId = processID,
				Id = 0,
				InternalStatus = "Request Received",
				IsClosable = false,
				ItemAssociations = new WorkItemAssociation[2]
				{
					new WorkItemAssociation
					{
						ID = 0,
						ExternalID = processID,
						CustodialAccountNumber = "3237917",
						Custodian = "GNW",
						ItemName = "Sample Trust 1",
						ItemStatus = ItemStatus.InProcess,
						CreatedDate = DateTime.Now,
						IsActive = true,
					},

					new WorkItemAssociation
					{
						ID = 0,
						ExternalID = processID,
						CustodialAccountNumber = "3237918",
						Custodian = "GNW",
						ItemName = "Sample Trust 2",
						ItemStatus = ItemStatus.InProcess,
						CreatedDate = DateTime.Now,
						IsActive = true,
					}
				},
				KitName = "Test KitName",
				ModifiedDate = DateTime.Now,
				ObjectName = "Oxley Family",
				ResponseAllowed = true,
				ResponseRequired = true,
				ShortExternalDescription = "New Account Application 3",
				Source = "BPM",
				Status = "Request Received",
				UploadAllowed = true,
				WorkItemType = "New Account Application",
				RankOrder = 1,
				ParentExternalID = "B035981639"
			};

			var response = TrackingCenterClient.Clientv3.UpdateChildWorkItem(processID, USERID, childWorkItem);
			Assert.IsNotNull(response);
			Assert.IsNotNull(response.Data);
		}

		[TestMethod]
		public void CreateDItem()
		{
			//First check Max(ItemID) in Item table of TC database, then MAX(ItemID) + 1 and set the value in processID variables.
			string processID = "N4271908";

			WorkItem3 workItem3 = new WorkItem3()
			{
				AdvisorVisible = true,
				AgentID = "AG1634",
				ClientID = "CA0FZ3",
				CreatedBy = USERID,
				CreatedDate = DateTime.Now,
				Description = "Investment Solution Change Same Account",
				eSigType = "Print Only",
				ExternalId = processID,
				Id = 3291065,
				InternalStatus = "Request Received",
				IsClosable = false,
				ItemAssociations = new WorkItemAssociation[1]
				{
					new WorkItemAssociation
					{
						ID = 0,
						Custodian = "GNW",
						CreatedDate = DateTime.Now,
						IsActive = true,
					}
				},
				KitName = "Test KitName",
				ModifiedDate = DateTime.Now,
				ObjectName = "Test Client Name",
				ResponseAllowed = true,
				ResponseRequired = true,
				Source = WorkItemSource.NewAccount.ToString(),
				Status = "Request Received",
				UploadAllowed = true,
				WorkItemType = "New Account Paperwork Submission"
			};

			var response = TrackingCenterClient.Clientv3.UpdateWorkItem(processID, USERID, workItem3);
			Assert.IsNotNull(response.WorkItem);
			Assert.IsNotNull(response.WorkItem.Id);
			Assert.IsNotNull(response.WorkItem.ExternalId);
		}

		[TestMethod]
		public void GetWorkItemWebListByAgentID()
		{
			string[] agentID = new string[] { "AG1634" };
			bool computeCount = true;

			var response = TrackingCenterClient.Clientv3.GetWorkitemListWeb(userID: USERID,
																			agentID: agentID,
																			clientID: null,
																			accountID: null,
																			filter: null,
																			sortOrder: null,
																			fromDate: null,
																			toDate: null,
																			start: 0,
																			count: 0,
																			computeCount: computeCount);

			Assert.IsNotNull(response);
			Assert.IsNotNull(response.WorkItems);
			Assert.IsTrue(response.WorkItems.Length > 0);
		}

		[TestMethod]
		public void ReopenNItem()
		{
			string externalID = "N000027324";

			WorkItemResponse3 getResponse = TrackingCenterClient.Clientv3.GetWorkItemDetails(
												externalID,
												"Avinash.Ghawali",
												WorkItemSource.BPM.ToString());

			if(getResponse != null && getResponse.WorkItem != null)
			{
				getResponse.WorkItem.IsClosable = false;
				getResponse.WorkItem.IsReopen = true;
				getResponse.WorkItem.ClosedDate = null;
				getResponse.WorkItem.InternalStatus = "Awaiting Advisor Submission";
				getResponse.WorkItem.Status = "Awaiting Advisor Submission";
				getResponse.WorkItem.ResponseAllowed = true;
				getResponse.WorkItem.ResponseRequired = false;
				getResponse.WorkItem.Source = WorkItemSource.BPM.ToString();

				WorkItemResponse3 updateResponse = TrackingCenterClient.Clientv3.UpdateWorkItem(externalID, "Avinash.Ghawali", getResponse.WorkItem);

				Assert.IsNotNull(updateResponse);
				Assert.IsNotNull(updateResponse.WorkItem);
				Assert.IsNotNull(updateResponse.WorkItem.Id);
			}
		}

		[TestMethod]
		public void GetBPMWorkItemList()
		{
			string[] agentId = ArrayExt.Empty<string>();
			string[] clientId = new string[] { "C205R7", "W0TT38", "W0TT36"};
			string[] accountId = ArrayExt.Empty<string>();
			string[] accountNumber = ArrayExt.Empty<string>();
			string filter = "((WebStatus == \"open\") && (like(ExternalID, 'N%')) && (isparent == \"False\") && (isunadopted == \"False\"))";

			WorkItemsResponse3 response = TrackingCenterClient.Clientv3.GetBPMWorkItemList(agentId,
																						clientId,
																						accountId,
																						accountNumber,
																						filter);
			Assert.IsNotNull(response);
			Assert.IsNotNull(response.WorkItems);
		}
	}
}
