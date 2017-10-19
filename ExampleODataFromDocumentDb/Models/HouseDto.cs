using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Microsoft.Azure.Documents;
using System.Collections.Generic;

namespace ExampleODataFromDocumentDb
{
    /// <summary>
    /// ======================================================================================================================
    ///     NOTE: This is a HouseDto ("Data Transfer Object") class, used in order to decouple the OData response 
    ///     format from the underlying HouseDocument storage format.  HouseDto should *never* be written to 
    ///     DocumentDB, but we have decorated the Id and ETag properties the same as a DocumentDb Resource so 
    ///     that the database can be queried and deserialized into this format.
    /// ======================================================================================================================
    /// </summary>
    [DataContract] // is part of odata model
    public class HouseDto : IDocument
    {
        public HouseDto()
        {
        }

        /// <summary>
        /// Expose the underlying Resource Id (primary key) to OData
        /// </summary>
        [Key] // odata will use this as the primary key
        [DataMember] // is part of odata model
        [JsonProperty(PropertyName = "id")] // when deserializing the DTO directly from DocumentDB, the underlying property name is different (see Resource)
        public string Id { get; set; }

        /// <summary>
        /// Expose the underlying Resource ETag to OData
        /// </summary>
        [Timestamp] // odata will use this as an ETag source
        [DataMember] // is part of odata model
        [JsonProperty(PropertyName = "_etag")] // when deserializing the DTO directly from DocumentDB, the underlying property name is different (see Resource)
        public string ETag { get; set; }

        /// <summary>
        /// =====================================================================================================================
        /// Having this hard-coded check on the setter may save you some corruption pain later on if you need to support multiple 
        /// document types in the same collection, and having this enum available is great for future-proofing even if you don't.
        /// =====================================================================================================================
        /// </summary>
        [DataMember] // is part of odata model
        public virtual DocumentType DocumentType
        {
            get
            {
                return DocumentType.House;
            }
            set
            {
                if (value != DocumentType.House)
                {
                    throw new InvalidOperationException("Attempt to deserialize something which is not a House DTO into the House DTO type.");
                }
            }
        }

        [DataMember] // is part of odata model
        public DateTimeOffset CreateDate { get; set; }

        [DataMember] // is part of odata model
        public DateTimeOffset? UpdateDate { get; set; }

        // ========= everything below here is specific to the "House" type and should be kept in sync with HouseDocument

        /// <summary>
        /// Name property
        /// </summary>
        [DataMember] // is part of odata model
        public string TestName { get; set; }

        [DataMember] // is part of odata model
        [JsonConverter(typeof(DateTimeDocumentDbJsonConverter))] // serialize in a format compatible with querying
        public DateTimeOffset TestDateTimeOffset { get; set; }

        [DataMember] // is part of odata model
        [JsonConverter(typeof(DateTimeDocumentDbJsonConverter))] // serialize in a format compatible with querying
        public DateTimeOffset? TestDateTimeOffsetNullable { get; set; }

        // note: I'm using inline classes here just to prevent the example from becoming too large, you would probably want to create new source files for them in practice
        // note: nested classes don't need their properties decorated with [DataMember], only any first-level properties on the root DTO type must be marked
        public class Window
        {
            // note: I'm using inline classes here just to prevent the example from becoming too large, you would probably want to create new source files for them in practice
            // note: nested classes don't need their properties decorated with [DataMember], only any first-level properties on the root DTO type must be marked
            public class GlassPane
            {
                public int WidthInches { get; set; }

                public int HeightInches { get; set; }
            }

            public int WidthInches { get; set; }

            public int HeightInches { get; set; }

            public List<GlassPane> Panes { get; set; }

            [JsonConverter(typeof(DateTimeDocumentDbJsonConverter))] // serialize in a format compatible with querying
            public DateTimeOffset? InstalledDate { get; set; }
        }

        [DataMember] // is part of odata model
        public Window TestSkylight { get; set; }

        [DataMember] // is part of odata model
        public List<Window> TestWindows { get; set; }
    }
}