using System.Collections.Specialized;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// FeedResponse callback type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public enum FeedResponseType
    {
        BeforeEnumeration,
        PageReceived,
        AfterEnumeration,
        EnumerationAborted
    }
}
