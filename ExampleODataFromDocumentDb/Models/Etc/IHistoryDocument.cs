using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleODataFromDocumentDb
{
    interface IHistoryDocument
    {
        string ModifiedId { get; }

        string ModifyAction { get; }

        DateTimeOffset ModifyTimestamp { get; }
    }
}
