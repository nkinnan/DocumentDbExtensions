using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;

namespace ExampleODataFromDocumentDb
{
    /// <summary>
    /// =====================================================================================================================
    ///     NOTE: normally you would have a Document class and a Dto ("Data Transfer Object") class, and thus decouple the 
    ///     OData response format from your underlying storage format.  For this example they will be one and the same
    ///     because we are simply demonstrating how to get OData to work with DocumentDB.
    /// =====================================================================================================================
    /// </summary>
    public class Car : Resource, IDocument
    {
        public Car()
        {
        }

        /// <summary>
        /// =====================================================================================================================
        /// Having this hard-coded check on the setter may save you some corruption pain later on if you need to support multiple 
        /// document types in the same collection, and having this enum available is great for future-proofing even if you don't.
        /// =====================================================================================================================
        /// </summary>
        [JsonProperty]
        public DocumentType DocumentType
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
        [JsonProperty]
        [JsonConverter(typeof(DateTimeDocumentDbJsonConverter))]
        public DateTimeOffset CreateDate { get; set; }

        /// <summary>
        /// Don't forget the converter!
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(DateTimeDocumentDbJsonConverter))]
        public DateTimeOffset? UpdateDate { get; set; }
    }
}