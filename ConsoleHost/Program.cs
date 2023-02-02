using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;

namespace GSS.TrackingCenterService
{
	public class Program
	{
		private static readonly string CR = "\r\n";
		private ServiceHost[] mHost = null;

		static void Main(string[] args)
		{
			Program pgm = new Program();
			pgm.StartServices();
		}

		private ServiceHost StartAService(Type ServiceType)
		{
			ServiceHost NewServiceHost = null;
			bool bSuccess = true;
			StringBuilder sb = new StringBuilder();
			try
			{
				sb.Append("\r\nTrackingCenter Service listening at...\r\n");

				NewServiceHost = new ServiceHost(ServiceType);
				NewServiceHost.Open();

				IEnumerator<Uri> iter = NewServiceHost.BaseAddresses.GetEnumerator();
				while (iter.MoveNext())
				{
					sb.Append(iter.Current.ToString() + CR);
				}

			}
			catch (Exception ex)
			{
				bSuccess = false;
				sb.Append(ex.Message + CR);
			}
			finally
			{
				Console.WriteLine(sb.ToString());

				if (!bSuccess)
					StopService();
			}

			return NewServiceHost;
		}

		private void StartServices()
		{
			mHost = new ServiceHost[3];

			GSSTrackingCenterService3.Initialize();
			mHost[0] = StartAService(typeof(GSSTrackingCenterService3));

			GSSTrackingCenterService2.Initialize();
			mHost[1] = StartAService(typeof(GSSTrackingCenterService2));

			GSSTrackingCenterService1.Initialize();
			mHost[2] = StartAService(typeof(GSSTrackingCenterService1));

			Console.ReadLine();
		}

		private void StopService()
		{
			if (mHost != null)
			{
				for (int i = 0; i < mHost.Length; i++)
				{
					if (mHost[i] != null)
					{
						if (mHost[i].State == CommunicationState.Opened)
						{
							mHost[i].Close();
						}
					}
				}

				mHost = null;
			}

			Console.WriteLine("StopService: GSSTrackingCenterService1");
			Console.WriteLine("StopService: GSSTrackingCenterService2");
			Console.WriteLine("StopService: GSSTrackingCenterService3");
		}
	}
}

