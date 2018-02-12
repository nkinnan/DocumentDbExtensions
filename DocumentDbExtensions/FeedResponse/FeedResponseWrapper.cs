using System.Collections.Specialized;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// The only purpose of this is to adapt FeedResponse into IFeedResponse which hides all enumeration ability
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class FeedResponseWrapper<T> : IFeedResponse
    {
        private FeedResponse<T> feedResponse;

        public FeedResponseWrapper(FeedResponse<T> feedResponse)
        {
            this.feedResponse = feedResponse;
        }

        //
        // Summary:
        //     Gets the activity ID for the request.
        public string ActivityId { get { return feedResponse.ActivityId; } }
        //
        // Summary:
        //     Gets the maximum quota for collection resources within an account.
        public long CollectionQuota { get { return feedResponse.CollectionQuota; } }
        //
        // Summary:
        //     Maximum size of a collection in kilobytes.
        public long CollectionSizeQuota { get { return feedResponse.CollectionSizeQuota; } }
        //
        // Summary:
        //     Current size of a collection in kilobytes.
        public long CollectionSizeUsage { get { return feedResponse.CollectionSizeUsage; } }
        //
        // Summary:
        //     The current number of collection resources within the account.
        public long CollectionUsage { get { return feedResponse.CollectionUsage; } }
        //
        // Summary:
        //     The content parent location, for example, dbs/foo/colls/bar
        public string ContentLocation { get { return feedResponse.ContentLocation; } }
        //
        // Summary:
        //     Gets the number of items returned in the response.
        public int Count { get { return feedResponse.Count; } }
        //
        // Summary:
        //     Gets the current size of this entity.
        public string CurrentResourceQuotaUsage { get { return feedResponse.CurrentResourceQuotaUsage; } }
        //
        // Summary:
        //     Gets the maximum quota for database resources within the account.
        public long DatabaseQuota { get { return feedResponse.DatabaseQuota; } }
        //
        // Summary:
        //     The current number of database resources within the account.
        public long DatabaseUsage { get { return feedResponse.DatabaseUsage; } }
        //
        // Summary:
        //     Gets the maximum size limit for this entity.
        public string MaxResourceQuota { get { return feedResponse.MaxResourceQuota; } }
        //
        // Summary:
        //     Gets the maximum quota for permission resources within an account.
        public long PermissionQuota { get { return feedResponse.PermissionQuota; } }
        //
        // Summary:
        //     The current number of permission resources within the account.
        public long PermissionUsage { get { return feedResponse.PermissionUsage; } }
        //
        // Summary:
        //     Gets the request charge for this request.
        public double RequestCharge { get { return feedResponse.RequestCharge; } }
        //
        // Summary:
        //     Gets the continuation token to be used for continuing enumeration.
        public string ResponseContinuation { get { return feedResponse.ResponseContinuation; } }
        //
        // Summary:
        //     Gets the response headers.
        public NameValueCollection ResponseHeaders { get { return feedResponse.ResponseHeaders; } }
        //
        // Summary:
        //     Gets the session token for use in sesssion consistency reads.
        public string SessionToken { get { return feedResponse.SessionToken; } }
        //
        // Summary:
        //     Gets the maximum quota of stored procedures for a collection.
        public long StoredProceduresQuota { get { return feedResponse.StoredProceduresQuota; } }
        //
        // Summary:
        //     The current number of stored procedures for a collection.
        public long StoredProceduresUsage { get { return feedResponse.StoredProceduresUsage; } }
        //
        // Summary:
        //     Gets the maximum quota of triggers for a collection.
        public long TriggersQuota { get { return feedResponse.TriggersQuota; } }
        //
        // Summary:
        //     The current number of triggers for a collection.
        public long TriggersUsage { get { return feedResponse.TriggersUsage; } }
        //
        // Summary:
        //     Gets the maximum quota of user defined functions for a collection.
        public long UserDefinedFunctionsQuota { get { return feedResponse.UserDefinedFunctionsQuota; } }
        //
        // Summary:
        //     The current number of user defined functions for a collection.
        public long UserDefinedFunctionsUsage { get { return feedResponse.UserDefinedFunctionsUsage; } }
        //
        // Summary:
        //     Gets the maximum quota for user resources within an account.
        public long UserQuota { get { return feedResponse.UserQuota; } }
        //
        // Summary:
        //     The current number of user resources within the account.
        public long UserUsage { get { return feedResponse.UserUsage; } }
    }
}
