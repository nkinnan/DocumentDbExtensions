using System;
using System.Globalization;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Extensions to DateTime and DateTimeOffset.  These extensions output ISO 8601 formatted strings in the same format 
    /// as DocumentDB stored procedures, triggers, etc will get if they call (in JavaScript) "new Date().toISOString()"
    /// </summary>
    public static class DateTimeFormatExtensions
    {
        /// <summary>
        /// Matches what DocumentDB triggers/sprocs generate with the JavaScript: new Date().toISOString();
        /// AKA - ISO 8601 format (or, one variant of it, with a particular precision, etc.)
        /// A real example of a trigger generated value: "2016-02-17T23:12:46.959Z"
        /// </summary>
        public const string FormatString =              @"yyyy-MM-ddTHH:mm:ss.fffZ"; 

        /// <summary>
        /// Output a string in ISO 8601 format, compatible with DocumentDB triggers/sprocs that call "new Date().toISOString();"
        /// </summary>
        /// <param name="dt">"this"</param>
        /// <returns>The string representation in ISO 8601 format.</returns>
        public static string ToDocDbFormat(this DateTime? dt)
        {
            if (dt == null)
            {
                return null;
            }
            else
            {
                return ToDocDbFormat(dt.Value);
            }
        }

        /// <summary>
        /// Output a string in ISO 8601 format, compatible with DocumentDB triggers/sprocs that call "new Date().toISOString();"
        /// </summary>
        /// <param name="dt">"this"</param>
        /// <returns>The string representation in ISO 8601 format.</returns>
        public static string ToDocDbFormat(this DateTime dt)
        {
            return dt.ToUniversalTime().ToString(FormatString, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Output a string in ISO 8601 format, compatible with DocumentDB triggers/sprocs that call "new Date().toISOString();"
        /// </summary>
        /// <param name="dt">"this"</param>
        /// <returns>The string representation in ISO 8601 format.</returns>
        public static string ToDocDbFormat(this DateTimeOffset? dt)
        {
            if (dt == null)
            {
                return null;
            }
            else
            {
                return ToDocDbFormat((DateTimeOffset)dt);
            }
        }

        /// <summary>
        /// Output a string in ISO 8601 format, compatible with DocumentDB triggers/sprocs that call "new Date().toISOString();"
        /// </summary>
        /// <param name="dto">"this"</param>
        /// <returns>The string representation in ISO 8601 format.</returns>
        public static string ToDocDbFormat(this DateTimeOffset dto)
        {
            return dto.ToUniversalTime().ToString(FormatString, CultureInfo.InvariantCulture);
        }
    }
}
