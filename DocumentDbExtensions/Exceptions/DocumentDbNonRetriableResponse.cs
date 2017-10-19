using System;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// This exception type will be thrown if the default ShouldRetry logic encounters a response that is expected but non-retriable such as Conflict or NotFound.
    /// </summary>
    [Serializable]
    public class DocumentDbNonRetriableResponse : Exception
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="inner"></param>
        public DocumentDbNonRetriableResponse(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}