using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;
using System.Linq.Expressions;
using CodeFirstStoreFunctions;
using GSS.Entities.DB;
using LinqKit;

namespace GSS.TrackingCenter.DataAccess
{
	public partial class TrackingCenterReadOnlyDataContext : DbContext
	{
		public TrackingCenterReadOnlyDataContext() : base(nameOrConnectionString: ConnectionName)
		{
			var type = typeof(System.Data.Entity.SqlServer.SqlProviderServices);
			Database.SetInitializer<TrackingCenterReadOnlyDataContext>(null);
			this.Database.Log = Console.Write;
		}

		protected override void OnModelCreating(DbModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.Conventions.Add(new FunctionsConvention<TrackingCenterReadOnlyDataContext>("dbo"));
			modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
			modelBuilder.ComplexType<SerachWorkItem>().Property(prop => prop.ItemType).HasColumnType("CHAR(1)").IsUnicode(false);
			modelBuilder.Configurations.AddFromAssembly(this.GetType().Assembly);
		}

		public int GetWorkItemsCount(Expression<Func<SerachWorkItem, bool>> expression)
		{
			var dbQuery = this.getItems().AsExpandable().Where(expression);
			return dbQuery.Count();
		}

		public List<SerachWorkItem> GetSearchWorkItemList(Expression<Func<SerachWorkItem, bool>> expression, int pageNumber, int pageSize)
		{
			return this.getItems()
						.AsExpandable()
						.Where(expression)
						.OrderByDescending(item => item.ModifiedDate)
						.ThenByDescending(item => item.ItemID)
						.Skip((pageNumber - 1) * pageSize)
						.Take(pageSize)
						.ToList();
		}

		[DbFunction("TrackingCenterReadOnlyDataContext", "getItems")]
		private IQueryable<SerachWorkItem> getItems()
		{
			return ((IObjectContextAdapter)this).ObjectContext.CreateQuery<SerachWorkItem>("[TrackingCenterReadOnlyDataContext].[getItems]()");
		}

		private static string ConnectionName
		{
			get
			{
				return ConfigurationManager.ConnectionStrings["TRACKING_CENTER_READONLY_EF"].ToString();
			}
		}
	}
}
