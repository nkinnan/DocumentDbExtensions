using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Documents
{
    public class DocumentsPage<T>
    {
        internal DocumentsPage(IReadOnlyList<T> documents, string continuationToken)
        {
            this.Documents = documents;
            this.ContinuationToken = continuationToken;
        }

        public IReadOnlyList<T> Documents { get; private set; }

        public string ContinuationToken { get; private set; }

        public bool MoreResultsAvailable
        {
            get
            {
                return ContinuationToken != null;
            }
        }
    }
}
