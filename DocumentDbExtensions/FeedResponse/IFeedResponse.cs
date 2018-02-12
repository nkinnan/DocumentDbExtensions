using System.Collections.Specialized;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Wrapper around Azure DocumentDB FeedResponse, exposing properties as read-only
    /// </summary>
    public interface IFeedResponse
    {
        /// <summary>
        /// Gets the activity ID for the request.
        /// </summary>
        string ActivityId { get; }
        /// <summary>
        /// Gets the maximum quota for collection resources within an account.
        /// </summary>
        long CollectionQuota { get; }
        /// <summary>
        /// Maximum size of a collection in kilobytes.
        /// </summary>
        long CollectionSizeQuota { get; }
        /// <summary>
        /// Current size of a collection in kilobytes.
        /// </summary>
        long CollectionSizeUsage { get; }
        /// <summary>
        /// The current number of collection resources within the account.
        /// </summary>
        long CollectionUsage { get; }
        /// <summary>
        /// The content parent location, for example, dbs/foo/colls/bar
        /// </summary>
        string ContentLocation { get; }
        /// <summary>
        /// Gets the number of items returned in the response.
        /// </summary>
        int Count { get; }
        /// <summary>
        /// Gets the current size of this entity.
        /// </summary>
        string CurrentResourceQuotaUsage { get; }
        /// <summary>
        /// Gets the maximum quota for database resources within the account.
        /// </summary>
        long DatabaseQuota { get; }
        /// <summary>
        /// The current number of database resources within the account.
        /// </summary>
        long DatabaseUsage { get; }
        /// <summary>
        /// Gets the maximum size limit for this entity.
        /// </summary>
        string MaxResourceQuota { get; }
        /// <summary>
        /// Gets the maximum quota for permission resources within an account.
        /// </summary>
        long PermissionQuota { get; }
        /// <summary>
        /// The current number of permission resources within the account.
        /// </summary>
        long PermissionUsage { get; }
        /// <summary>
        /// Gets the request charge for this request.
        /// </summary>
        double RequestCharge { get; }
        /// <summary>
        /// Gets the continuation token to be used for continuing enumeration.
        /// </summary>
        string ResponseContinuation { get; }
        /// <summary>
        /// Gets the response headers.
        /// </summary>
        NameValueCollection ResponseHeaders { get; }
        /// <summary>
        /// Gets the session token for use in sesssion consistency reads.
        /// </summary>
        string SessionToken { get; }
        /// <summary>
        /// Gets the maximum quota of stored procedures for a collection.
        /// </summary>
        long StoredProceduresQuota { get; }
        /// <summary>
        /// The current number of stored procedures for a collection.
        /// </summary>
        long StoredProceduresUsage { get; }
        /// <summary>
        /// Gets the maximum quota of triggers for a collection.
        /// </summary>
        long TriggersQuota { get; }
        /// <summary>
        /// The current number of triggers for a collection.
        /// </summary>
        long TriggersUsage { get; }
        /// <summary>
        /// Gets the maximum quota of user defined functions for a collection.
        /// </summary>
        long UserDefinedFunctionsQuota { get; }
        /// <summary>
        /// The current number of user defined functions for a collection.
        /// </summary>
        long UserDefinedFunctionsUsage { get; }
        /// <summary>
        /// Gets the maximum quota for user resources within an account.
        /// </summary>
        long UserQuota { get; }
        /// <summary>
        /// The current number of user resources within the account.
        /// </summary>
        long UserUsage { get; }

    }
}
