using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Ceen.Security.Login
{
	/// <summary>
	/// A string serializer with minimal overhead and support.
	/// This module is used to store the short-term storage items as strings in memory.
	/// </summary>
	public static class PrimitiveSerializer
	{
		/// <summary>
		/// The field separator.
		/// </summary>
		public const string FIELD_SEPARATOR = "&";

		/// <summary>
		/// Escapes special characters in a string
		/// </summary>
		/// <returns>The escaped string.</returns>
		/// <param name="input">The string to escape.</param>
		public static string EscapeString(string input)
		{
			return Uri.EscapeDataString(input);
		}

		/// <summary>
		/// Unescapes the string, removing escaped characters.
		/// </summary>
		/// <returns>The unescaped string.</returns>
		/// <param name="input">The escaped string.</param>
		public static string UnescapeString(string input)
		{
			return QueryStringSerializer.UnescapeDataString(input);
		}

		/// <summary>
		/// Provides a string representation of an object
		/// </summary>
		/// <param name="item">The item to serialize.</param>
		public static string Serialize<T>(T item)
		{
			return Serialize(item, typeof(T));
		}

		/// <summary>
		/// Gets the members of a given type
		/// </summary>
		/// <returns>The members.</returns>
		/// <param name="itemtype">Itemtype.</param>
		public static IEnumerable<MemberInfo> GetMembers(Type itemtype)
		{
			if (itemtype.IsPrimitive || itemtype == typeof(string))
				throw new ArgumentException($"Cannot use serializer with primitive type: {itemtype.FullName}", nameof(itemtype));

			return itemtype
				.GetProperties(BindingFlags.Instance | BindingFlags.Public)
				.OfType<MemberInfo>()
				.Union(
					itemtype
					.GetFields(BindingFlags.Instance | BindingFlags.Public)
					.OfType<MemberInfo>()
				);
		}

		/// <summary>
		/// Provides a string representation of an object
		/// </summary>
		/// <param name="item">The item to serialize.</param>
		public static string Serialize(object item, Type itemtype)
		{
			if (item == null)
				throw new ArgumentNullException(nameof(item));

			if (itemtype.IsPrimitive || itemtype == typeof(string))
				throw new ArgumentException($"Cannot use serializer with primitive type: {itemtype.FullName}", nameof(itemtype));

			var values = GetMembers(itemtype)
				.Select(x =>
					x is PropertyInfo
						? SerializeElement(((PropertyInfo)x).GetValue(item), ((PropertyInfo)x).PropertyType)
						: SerializeElement(((FieldInfo)x).GetValue(item), ((FieldInfo)x).FieldType)
					   );

			return string.Join(FIELD_SEPARATOR, values);
		}

		/// <summary>
		/// Deserializes a string representation of a property or field to its native representation
		/// </summary>
		/// <returns>The native object.</returns>
		/// <param name="value">The string value to deserialize.</param>
		/// <param name="entrytype">The type of the field or property.</param>
		public static object DeserializeElement(string value, Type entrytype)
		{
			if (entrytype == typeof(string))
				return value;
			else if (entrytype == typeof(DateTime))
				return new DateTime(long.Parse(value));
			else if (entrytype.IsEnum)
				return Enum.Parse(entrytype, value, false);
			else if (entrytype.IsPrimitive)
				return Convert.ChangeType(value, entrytype);
			else
				throw new ArgumentException($"The field type is not supported: {entrytype.FullName}");
		}

		/// <summary>
		/// Serializes a property or field value to a string presentation
		/// </summary>
		/// <returns>The serialized representation.</returns>
		/// <param name="value">The value to serialize as a string.</param>
		/// <param name="entrytype">The type of the field or property.</param>
		public static string SerializeElement(object value, Type entrytype)
		{
			if (entrytype == typeof(string))
				return value == null ? string.Empty : (string)value;
			else if (entrytype == typeof(DateTime))
				return ((DateTime)value).Ticks.ToString();
			else if (entrytype.IsEnum || entrytype.IsPrimitive)
				return entrytype.ToString();
			else
				throw new ArgumentException($"The field type is not supported: {entrytype.FullName}");				
		}

		/// <summary>
		/// Deserialize the specified value.
		/// </summary>
		/// <param name="value">The serialized string representation.</param>
		/// <typeparam name="T">The type of data to deserialize parameter.</typeparam>
		public static T Deserialize<T>(string value)
			where T : new()
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			var item = new T();
			var values = value.Split(new string[] { FIELD_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries);
			var ix = 0;
			foreach (var p in GetMembers(typeof(T)))
			{
				if (ix >= values.Length)
					throw new Exception($"Failed to deserialize {values.Length} fields into a {typeof(T).FullName} as it has too many fields");
				
				if (p is PropertyInfo)
					((PropertyInfo)p).SetValue(item, DeserializeElement(UnescapeString(values[ix++]), ((PropertyInfo)p).PropertyType), null);
				else
					((FieldInfo)p).SetValue(item, DeserializeElement(UnescapeString(values[ix++]), ((FieldInfo)p).FieldType));
			}

			if (ix != values.Length)
				throw new Exception($"Failed to deserialize {values.Length} fields into a {typeof(T).FullName} as it has too few fields");

			return item;
		}
		 
	}
}
