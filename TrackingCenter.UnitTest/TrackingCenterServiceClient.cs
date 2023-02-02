namespace GSS.TrackingCenter.IntegrationTest
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using GSS.ServiceClient;

	public class TrackingCenterClient
	{
		private readonly static string TrackingCenterServiceV3 = "PROXY.TRACKING_CENTER_V3";
		private static object lockObj = new object();
		private static TrackingCenterServiceClient clientv3 = null;

		public static TrackingCenterServiceClient Clientv3
		{
			get
			{
				try
				{
					lock (lockObj)
					{
						if (clientv3 == null)
						{
							clientv3 = new TrackingCenterServiceClient(TrackingCenterServiceV3);
						}
					}

					return clientv3;
				}
				catch (Exception ex)
				{

					throw ex;
				}
			}
		}
	}
}
