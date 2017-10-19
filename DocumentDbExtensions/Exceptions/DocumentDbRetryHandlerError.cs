using System;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// This exception type will be thrown if the ShouldRetry logic itself throws.
    /// </summary>
    [Serializable]
    public class DocumentDbRetryHandlerError : Exception
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="inner"></param>
        public DocumentDbRetryHandlerError(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
