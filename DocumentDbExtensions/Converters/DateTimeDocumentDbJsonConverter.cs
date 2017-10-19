using System;
using System.Globalization;
using Newtonsoft.Json;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// ALL DateTime or DateTimeOffset properties in your DocumentDB document type class MUST be decorated with this attribute
    /// in order for the DateTime query expression translator to work properly.
    /// 
    /// If your DateTime / DateTimeOffset properties are not formatted in DocumentDB identically to the format the expression 
    /// translater generates, then the string comparisons will not work properly when executed on the DocumentDB server.
    /// 
    /// Don't forget to use "new Date().toISOString()" which generates the same string format in your sprocs and triggers as well.
    ///
    /// -- This is actually pulled out of Newtonsoft.Json.dll via ILSpy and then slightly modified. --
    /// 
    /// The original has the ability to set "convert to universal time" and a custom format string, but DocumentDB doesn't allow access to this
    /// since we can only decorate properties with [JsonConvert(typeof(...))] and cannot pass in a properly configured instance of the converter.
    /// 
    /// Thus, we wrote our own which is "already configured" to do what we want, so it doesn't matter if DocDB instantiates it.
    /// 
    /// There is a feature request to make modifying serialization easier with DocDB that you can vote on here: 
    ///     https://feedback.azure.com/forums/263030-documentdb/suggestions/6422364-allow-me-to-set-jsonserializersettings
    /// </summary>
    public class DateTimeDocumentDbJsonConverter : JsonConverter
    {
        #region random helpers I had to pull in
        // Newtonsoft.Json.Utilities.ReflectionUtils
        private static bool IsNullableType(Type t)
        {
            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        // Newtonsoft.Json.Utilities.StringUtils
        private static bool EndsWith(string source, char value)
        {
            return source.Length > 0 && source[source.Length - 1] == value;
        }

        // Newtonsoft.Json.Utilities.ReflectionUtils
        private static Type GetObjectType(object v)
        {
            if (v == null)
            {
                return null;
            }
            return v.GetType();
        }

        // Newtonsoft.Json.JsonPosition
        private static string FormatMessage(IJsonLineInfo lineInfo, string path, string message)
        {
            if (!message.EndsWith(Environment.NewLine, StringComparison.Ordinal))
            {
                message = message.Trim();
                if (!EndsWith(message, '.'))
                {
                    message += ".";
                }
                message += " ";
            }
            message += string.Format(CultureInfo.InvariantCulture, "Path '{0}'", path);
            if (lineInfo != null && lineInfo.HasLineInfo())
            {
                message += string.Format(CultureInfo.InvariantCulture, ", line {0}, position {1}", lineInfo.LineNumber, lineInfo.LinePosition);
            }
            message += ".";
            return message;
        }
        #endregion

        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns>
        ///     <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
        /// </returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTime) || objectType == typeof(DateTime?) ||
                   objectType == typeof(DateTimeOffset) || objectType == typeof(DateTimeOffset?);
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="T:Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            string value2;

            if (value is DateTime)
            {
                DateTime dateTime = (DateTime)value;

                value2 = dateTime.ToDocDbFormat();
            }
            else
            {
                if (!(value is DateTimeOffset))
                {
                    throw new JsonSerializationException(string.Format(CultureInfo.InvariantCulture, "Unexpected value when converting date. Expected DateTime or DateTimeOffset, got {0}.", GetObjectType(value)));
                }

                DateTimeOffset dateTimeOffset = (DateTimeOffset)value;

                value2 = dateTimeOffset.ToDocDbFormat();
            }

            writer.WriteValue(value2);
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader" /> to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing value of object being read.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>The object value.</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            bool flag = IsNullableType(objectType);
            Type left = flag ? Nullable.GetUnderlyingType(objectType) : objectType;
            if (reader.TokenType == JsonToken.Null)
            {
                if (!IsNullableType(objectType))
                {
                    var message = FormatMessage(reader as IJsonLineInfo, reader.Path, string.Format(CultureInfo.InvariantCulture, "Cannot convert null value to {0}.", objectType));
                    throw new JsonSerializationException(message);
                }
                return null;
            }
            else if (reader.TokenType == JsonToken.Date)
            {
                if (!(left == typeof(DateTimeOffset)))
                {
                    return reader.Value;
                }
                if (!(reader.Value is DateTimeOffset))
                {
                    return new DateTimeOffset((DateTime)reader.Value);
                }
                return reader.Value;
            }
            else
            {
                if (reader.TokenType != JsonToken.String)
                {
                    var message = FormatMessage(reader as IJsonLineInfo, reader.Path, string.Format(CultureInfo.InvariantCulture, "Unexpected token parsing date. Expected String, got {0}.", reader.TokenType));
                    throw new JsonSerializationException(message);
                }
                string text = reader.Value.ToString();
                if (string.IsNullOrEmpty(text) && flag)
                {
                    return null;
                }
                if (left == typeof(DateTimeOffset))
                {
                    return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
                else
                {
                    return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
            }
        }
    }
}
