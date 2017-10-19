using System;
using System.Runtime.Serialization;
using Microsoft.Azure.Documents;
using Newtonsoft.Json;

namespace ExampleODataFromDocumentDb
{
    /// <summary>
    /// ======================================================================================================================
    ///     NOTE: This is a HouseHistoryDocument class, it is the authoritative format of a HouseHistory record in DocumentDB.
    ///     This is the only type that should be written to a DocumentDB HouseHistory record, but HouseHistory records can 
    ///     also be deserialized into HouseDto ("Data Transfer Object") used by OData.
    /// ======================================================================================================================
    /// </summary>
    public class HouseHistoryDocument : HouseDocument, IDocument, IHistoryDocument
    {
        public HouseHistoryDocument()
        {
        }

        /// <summary>
        /// =====================================================================================================================
        /// Having this hard-coded check on the setter may save you some corruption pain later on if you need to support multiple 
        /// document types in the same collection, and having this enum available is great for future-proofing even if you don't.
        /// =====================================================================================================================
        /// </summary>
        public override DocumentType DocumentType
        {
            get
            {
                return DocumentType.HouseHistory;
            }
            set
            {
                if (value != DocumentType.HouseHistory)
                {
                    throw new InvalidOperationException("Attempt to deserialize something which is not a HouseHistory document into the HouseHistory document type.");
                }
            }
        }

        public string ModifiedId { get; set; }

        public string ModifyAction { get; set; }

        /// <summary>
        /// Don't forget the converter!
        /// </summary>
        [JsonConverter(typeof(DateTimeDocumentDbJsonConverter))] // serialize to docdb in a format compatible with querying
        public DateTimeOffset ModifyTimestamp { get; set; }
    }
}