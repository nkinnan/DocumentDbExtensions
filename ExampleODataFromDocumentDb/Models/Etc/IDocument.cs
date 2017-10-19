using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleODataFromDocumentDb
{
    interface IDocument
    {
        string Id { get; }
        DocumentType DocumentType { get; }
        DateTimeOffset CreateDate { get; }
        DateTimeOffset? UpdateDate { get; }
    }
}
