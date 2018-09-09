using System;
using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// This provides a constant instance across all invocations of FeedResponseHandler on a per-query basis
    /// and aggregates totals.  It can be used to maintain context across calls when overriding the default
    /// feed response handler.
    /// </summary>
    public class FeedResponseContext
    {
        private Stopwatch sw = new Stopwatch();

        /// <summary>
        /// Constructor
        /// </summary>
        public FeedResponseContext()
        {
            sw.Start();
        }

        /// <summary>
        /// User may record their own context here, the library will not touch this field.
        /// </summary>
        public object UserContext { get; set; }

        /// <summary>
        /// Gets the total number of items returned in all responses so far for this feed.
        /// </summary>
        public int TotalCount { get; internal set; }

        /// <summary>
        /// Gets the request charge for this request.
        /// </summary>
        public double TotalRequestCharge { get; internal set; }

        /// <summary>
        /// Total size of all responses
        /// </summary>
        public int TotalResultsJsonStringLength { get; internal set; }

        /// <summary>
        /// Total time taken so far
        /// </summary>
        public TimeSpan TotalExecutionTime { get { return sw.Elapsed; } }

        internal void StopTiming()
        {
            sw.Stop();
        }
    }
}
