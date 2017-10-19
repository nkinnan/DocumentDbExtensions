using System;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// This exception type will be thrown when the retry logic hits maxTime or maxRetries.
    /// </summary>
    [Serializable]
    public class DocumentDbRetriesExceeded : Exception
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="inner"></param>
        public DocumentDbRetriesExceeded(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
