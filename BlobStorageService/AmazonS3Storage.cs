using System.Diagnostics;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BlobStorageService
{
    /// <summary>
    /// Amazon S3 based implementation of the storage service
    /// </summary>
    public sealed class AmazonS3Storage: IBlobStorage
    {
        private readonly IAmazonS3 _client;
        public AmazonS3Storage(IAmazonS3 client)
        {
            if (client == null) throw new ArgumentNullException("client");
            _client = client;
        }

        public async Task PutObjectAsync(string bucketName, string key, byte[] data, string contentType)
        {
            Debug.Assert(!String.IsNullOrEmpty(bucketName), "Bucket name must be specified.");
            Debug.Assert(!String.IsNullOrEmpty(key), "Object key must be specified.");
            Debug.Assert(data != null, "No data to upload.");
            Debug.Assert(!String.IsNullOrEmpty(contentType), "Content type must be specified.");

            try
            {
                var putRequest = new PutObjectRequest()
                {
                    BucketName = bucketName,
                    Key = key,
                    ContentType = contentType,
                    InputStream = new MemoryStream(data, false)
                };

                await _client.PutObjectAsync(putRequest);

            }
            catch (AmazonServiceException e)
            {
                throw new BlobStorageException("Error storing object in the storage.", e);
            }
        }

        public async Task<byte[]> GetObjectAsync(string bucketName, string key)
        {
            Debug.Assert(!String.IsNullOrEmpty(bucketName), "Bucket name must be specified.");
            Debug.Assert(!String.IsNullOrEmpty(key), "Object key must be specified.");

            var getRequest = new GetObjectRequest()
            {
                BucketName = bucketName,
                Key = key
            };

            try
            {
                var response = await _client.GetObjectAsync(getRequest);
                var ms = new MemoryStream();
                await response.ResponseStream.CopyToAsync(ms);
                return ms.ToArray();
            }
            catch (AmazonServiceException e)
            {
                throw new BlobStorageException("Error getting object from the storage.", e);
            }

        }

        public async Task DeleteObjectAsync(string bucketName, string key)
        {
            Debug.Assert(!String.IsNullOrEmpty(bucketName), "Bucket name must be specified.");
            Debug.Assert(!String.IsNullOrEmpty(key), "Object key must be specified.");

            var deleteRequest = new DeleteObjectRequest()
            {
                BucketName = bucketName,
                Key = key
            };

            try
            {
                await _client.DeleteObjectAsync(deleteRequest);
            }
            catch (AmazonServiceException e)
            {
                throw new BlobStorageException("Error deleting object from the storage.", e);
            }
        }

    }
}