using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Encapsulates a page of documents plus a continuation token
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DocumentsPage<T> : IReadOnlyCollection<T>, IEnumerable<T>, IEnumerable
    {
        internal DocumentsPage(IReadOnlyList<T> documents, string continuationToken)
        {
            this.Documents = documents;
            this.ContinuationToken = continuationToken;
        }

        /// <summary>
        /// The encapsulated read-only list of documents returned for this page
        /// </summary>
        public IReadOnlyList<T> Documents { get; private set; }

        /// <summary>
        /// The continuation token used to get the next page
        /// </summary>
        public string ContinuationToken { get; private set; }

        /// <summary>
        /// Indicates whether additional pages are available
        /// </summary>
        public bool MoreResultsAvailable
        {
            get
            {
                return ContinuationToken != null;
            }
        }

        /// <summary>
        /// Count of returned documents
        /// </summary>
        public int Count { get { return Documents.Count; } }

        /// <summary>
        /// IEnumerable implementation
        /// </summary>
        /// <returns>Enumerator for the documents returned in this page</returns>
        public IEnumerator<T> GetEnumerator()
        {
            return Documents.GetEnumerator();
        }

        /// <summary>
        /// IEnumerable implementation
        /// </summary>
        /// <returns>Enumerator for the documents returned in this page</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return Documents.GetEnumerator();
        }
    }
}
