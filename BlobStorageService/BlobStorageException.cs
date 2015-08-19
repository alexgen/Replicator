using System;

namespace BlobStorageService
{
    /// <summary>
    /// Service specific exceptions
    /// </summary>
    public class BlobStorageException : Exception
    {
        public BlobStorageException()
            : base("Blob storage exception")
        {

        }
        public BlobStorageException(string message, Exception innerException)
            : base(message, innerException)
        {

        }

    }
}