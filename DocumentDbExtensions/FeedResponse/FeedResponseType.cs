using System.Collections.Specialized;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// FeedResponse callback type
    /// </summary>
    public enum FeedResponseType
    {
        /// <summary>
        /// Called before enumeration of results begins
        /// </summary>
        BeforeEnumeration,
        /// <summary>
        /// Called for each page
        /// </summary>
        PageReceived,
        /// <summary>
        /// Called after enumeration has finished
        /// </summary>
        AfterEnumeration,
        /// <summary>
        /// Called if enumeration is aborted
        /// </summary>
        EnumerationAborted
    }
}
