using System.Collections.Specialized;

namespace Microsoft.Azure.Documents
{
    public interface IFeedResponse
    {
        //
        // Summary:
        //     Gets the activity ID for the request.
        string ActivityId { get; }
        //
        // Summary:
        //     Gets the maximum quota for collection resources within an account.
        long CollectionQuota { get; }
        //
        // Summary:
        //     Maximum size of a collection in kilobytes.
        long CollectionSizeQuota { get; }
        //
        // Summary:
        //     Current size of a collection in kilobytes.
        long CollectionSizeUsage { get; }
        //
        // Summary:
        //     The current number of collection resources within the account.
        long CollectionUsage { get; }
        //
        // Summary:
        //     The content parent location, for example, dbs/foo/colls/bar
        string ContentLocation { get; }
        //
        // Summary:
        //     Gets the number of items returned in the response.
        int Count { get; }
        //
        // Summary:
        //     Gets the current size of this entity.
        string CurrentResourceQuotaUsage { get; }
        //
        // Summary:
        //     Gets the maximum quota for database resources within the account.
        long DatabaseQuota { get; }
        //
        // Summary:
        //     The current number of database resources within the account.
        long DatabaseUsage { get; }
        //
        // Summary:
        //     Gets the maximum size limit for this entity.
        string MaxResourceQuota { get; }
        //
        // Summary:
        //     Gets the maximum quota for permission resources within an account.
        long PermissionQuota { get; }
        //
        // Summary:
        //     The current number of permission resources within the account.
        long PermissionUsage { get; }
        //
        // Summary:
        //     Gets the request charge for this request.
        double RequestCharge { get; }
        //
        // Summary:
        //     Gets the continuation token to be used for continuing enumeration.
        string ResponseContinuation { get; }
        //
        // Summary:
        //     Gets the response headers.
        NameValueCollection ResponseHeaders { get; }
        //
        // Summary:
        //     Gets the session token for use in sesssion consistency reads.
        string SessionToken { get; }
        //
        // Summary:
        //     Gets the maximum quota of stored procedures for a collection.
        long StoredProceduresQuota { get; }
        //
        // Summary:
        //     The current number of stored procedures for a collection.
        long StoredProceduresUsage { get; }
        //
        // Summary:
        //     Gets the maximum quota of triggers for a collection.
        long TriggersQuota { get; }
        //
        // Summary:
        //     The current number of triggers for a collection.
        long TriggersUsage { get; }
        //
        // Summary:
        //     Gets the maximum quota of user defined functions for a collection.
        long UserDefinedFunctionsQuota { get; }
        //
        // Summary:
        //     The current number of user defined functions for a collection.
        long UserDefinedFunctionsUsage { get; }
        //
        // Summary:
        //     Gets the maximum quota for user resources within an account.
        long UserQuota { get; }
        //
        // Summary:
        //     The current number of user resources within the account.
        long UserUsage { get; }

    }
}
