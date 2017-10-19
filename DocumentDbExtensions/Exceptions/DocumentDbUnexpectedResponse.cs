using System;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// This exception type will be thrown if the default ShouldRetry logic encounters a response it can't understand (should not happen).
    /// </summary>
    [Serializable]
    public class DocumentDbUnexpectedResponse : Exception
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="inner"></param>
        public DocumentDbUnexpectedResponse(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
