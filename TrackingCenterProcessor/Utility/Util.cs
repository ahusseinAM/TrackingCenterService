namespace GSS.TrackingCenterProcessor
{
	using System;
	using System.Configuration;
	using System.Collections.Generic;
	using System.Linq;

	public class Util
	{
		private static IEnumerable<string> fileExtensions;
		private static string imageFileExtension = null;

		public static string GetConfigValue(string key)
		{
			string value = ConfigurationManager.AppSettings[key];

			if (string.IsNullOrWhiteSpace(value))
			{
				throw new ApplicationException(string.Format("Configuration warning: AppSettings {0} key does not have a value. Implementation may not work properly.", key));
			}

			return value;
		}

		public static IEnumerable<string> FileExtensions
		{
			get
			{
				try
				{
					if (fileExtensions == null)
					{
						imageFileExtension = GetConfigValue("ImageFileExtension");
						fileExtensions = imageFileExtension.Split(',').AsEnumerable();
					}
				}
				catch (Exception ex)
				{
					throw ex;
				}

				return fileExtensions;
			}
		}
	}
}
