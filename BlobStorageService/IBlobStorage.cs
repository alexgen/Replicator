using System.IO;
using System.Threading.Tasks;

namespace BlobStorageService
{
    /// <summary>
    /// Simple abstraction of a storage service for keeping our blobs
    /// </summary>
    public interface IBlobStorage
    {
        Task PutObjectAsync(string bucketName, string key, byte[] data, string contentType);
        Task<byte[]> GetObjectAsync(string bucketName, string key);
        Task DeleteObjectAsync(string bucketName, string key);
    }
}
