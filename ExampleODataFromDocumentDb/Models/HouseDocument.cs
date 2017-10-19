using System;
using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ExampleODataFromDocumentDb
{
    /// <summary>
    /// ======================================================================================================================
    ///     NOTE: This is a HouseDocument class, it is the authoritative format of a House record in DocumentDB.
    ///     This is the only type that should be written to a DocumentDB House record, but House records 
    ///     can also be deserialized into HouseDto ("Data Transfer Object") used by OData.
    /// ======================================================================================================================
    /// </summary>
    public class HouseDocument : Resource, IDocument
    {
        public HouseDocument()
        {
        }

        /// <summary>
        /// Expose the underlying Resource Id (primary key) with cannonicalization
        /// </summary>
        public override string Id
        {
            get
            {
                return base.Id;
            }
            set
            {
                base.Id = Guid.Parse(value).ToString("D");
            }
        }

        /// <summary>
        /// =====================================================================================================================
        /// Having this hard-coded check on the setter may save you some corruption pain later on if you need to support multiple 
        /// document types in the same collection, and having this enum available is great for future-proofing even if you don't.
        /// =====================================================================================================================
        /// </summary>
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
                    throw new InvalidOperationException("Attempt to deserialize something which is not a House document into the House document type.");
                }
            }
        }

        /// <summary>
        /// Don't forget the converter!
        /// </summary>
        [JsonConverter(typeof(DateTimeDocumentDbJsonConverter))] // serialize in a format compatible with querying
        public DateTimeOffset CreateDate { get; set; }

        /// <summary>
        /// Don't forget the converter!
        /// </summary>
        [JsonConverter(typeof(DateTimeDocumentDbJsonConverter))] // serialize in a format compatible with querying
        public DateTimeOffset? UpdateDate { get; set; }

        // ========= everything below here is specific to the "House" type and should be kept in sync with HouseDto

        public string TestName { get; set; }

        /// <summary>
        /// Don't forget the converter!
        /// </summary>
        [JsonConverter(typeof(DateTimeDocumentDbJsonConverter))] // serialize in a format compatible with querying
        public DateTimeOffset TestDateTimeOffset { get; set; }

        /// <summary>
        /// Don't forget the converter!
        /// </summary>
        [JsonConverter(typeof(DateTimeDocumentDbJsonConverter))] // serialize in a format compatible with querying
        public DateTimeOffset? TestDateTimeOffsetNullable { get; set; }

        // note: I'm using inline classes here just to prevent the example from becoming too large, you would probably want to create new source files for them in practice
        public class Window
        {
            // note: I'm using inline classes here just to prevent the example from becoming too large, you would probably want to create new source files for them in practice
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

        public Window TestSkylight { get; set; }

        public List<Window> TestWindows { get; set; }
    }
}