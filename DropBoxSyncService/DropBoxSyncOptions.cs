namespace DropboxIndexingService
{
    public class DropBoxSyncOptions
    {
        /// <summary>
        /// Scope indexing to this path. NB: If you change the path, make sure to reset cursor associated with this data source
        /// </summary>
        public string Path;

        /// <summary>
        /// Indicates if shared folders must be indexed
        /// </summary>
        public bool IncludeSharedFolders = false;

        /// <summary>
        /// Indicates if only files with existing thumbnails must be indexed
        /// </summary>
        public bool WithThumbnailsOnly = false;

        /// <summary>
        /// Size of the batch of changes to process at once - this controls memory usage
        /// </summary>
        public int ItemsPerBatch = 200;

        /// <summary>
        /// Indicates if thumbnails must be downloaded
        /// </summary>
        public bool DownloadThumbnails = false;

        // Thumbnail download and processing settings
        /// <summary>
        /// DropBox-specific thumbnail size to download. See DropBox API documentation for reference.
        /// </summary>
        public string ThumbSizeToRequest = "m"; //xs, s, m, l, xl = 1024x768 max

        /// <summary>
        /// Number of download tasks to keep running for downloading thumbnails
        /// </summary>
        public int NumberOfConcurrentDownloads = 10;

        /// <summary>
        /// Max number of attempts to do for each document before giving up 
        /// when a read request is throttled by service provider or there is connect error
        /// </summary>
        public int MaxRetryAttempts = 5;

        /// <summary>
        /// Delay in seconds to make between attempts on a single document
        /// </summary>
        public int RetryDelaySeconds = 10;

    }
}