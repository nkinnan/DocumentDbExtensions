using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace ExampleODataFromDocumentDb
{
    /// <summary>
    /// ======================================================================================================================
    ///     NOTE: This is a HouseHistoryDto ("Data Transfer Object") class, used in order to decouple the OData response 
    ///     format from the underlying HouseHistoryDocument storage format.  HouseHistoryDto should *never* be written to 
    ///     DocumentDB, but we have decorated the (base class) Id and ETag properties the same as a DocumentDb Resource so 
    ///     that the database can be queried and deserialized into this format.
    /// ======================================================================================================================
    /// </summary>
    [DataContract]
    public class HouseHistoryDto : HouseDto, IDocument, IHistoryDocument
    {
        public HouseHistoryDto()
        {
        }

        /// <summary>
        /// =====================================================================================================================
        /// Having this hard-coded check on the setter may save you some corruption pain later on if you need to support multiple 
        /// document types in the same collection, and having this enum available is great for future-proofing even if you don't.
        /// =====================================================================================================================
        /// </summary>
        [DataMember] // is part of odata model
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
                    throw new InvalidOperationException("Attempt to deserialize something which is not a HouseHistory DTO into the HouseHistory DTO type.");
                }
            }
        }

        [DataMember] // is part of odata model
        public string ModifiedId { get; set; }

        [DataMember] // is part of odata model
        public string ModifyAction { get; set; }

        [DataMember] // is part of odata model
        public DateTimeOffset ModifyTimestamp { get; set; }
    }
}