using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace GSS.TrackingCenter.BusinessProcessor.Utility
{
    public static class EntityHelper
	{
		/// <summary>
		/// Shallow copy similar objects, example T1 inherits from T2
		/// </summary>
		public static T1 CopyFrom<T1, T2>(this T1 destination, T2 source)
			where T1 : class
			where T2 : class
		{
			if (source != null)
			{
				if (destination == null)
				{
					throw new ArgumentException("The destination must be not null");
				}

				PropertyInfo[] sourceFields = source.GetType().GetProperties(
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);

				PropertyInfo[] destinationFields = destination.GetType().GetProperties(
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty);

				foreach (var property in sourceFields)
				{
					var dest = destinationFields.FirstOrDefault(x => x.Name.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
					if (dest != null)
						if (dest.CanWrite)
							dest.SetValue(destination, property.GetValue(source, null), null);
				}
			}

			return destination;
		}

		/// <summary>
		/// Deep copies an object of the same type
		/// </summary>
		public static T1 DeepCopy<T1>(T1 source)
			where T1 : class
		{
			T1 destination = null;

			if (source != null)
			{
				if (!typeof(T1).IsSerializable)
				{
					throw new ArgumentException("The type must be serializable.", "source");
				}

				IFormatter formatter = new BinaryFormatter();
				Stream stream = new MemoryStream();
				using (stream)
				{
					formatter.Serialize(stream, source);
					stream.Seek(0, SeekOrigin.Begin);
					destination = (T1)formatter.Deserialize(stream);
				}
			}

			return destination;
		}

		/// <summary>
		/// Deep copy's similar objects, example T1 inherits from T2
		/// </summary>
		public static T1 DeepCopyFrom<T1, T2>(this T1 destination, T2 source)
			where T1 : class
			where T2 : class
		{
			return CopyFrom<T1, T2>(destination, DeepCopy<T2>(source));
		}
	}
}
