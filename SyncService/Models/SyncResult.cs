namespace SyncService.Models
{
    // Results summary
    public class SyncResult
    {
        /// <summary>
        /// Total number of source changes processed (does not include folders)
        /// </summary>
        public int SourceChangesProcessed;

        /// <summary>
        /// number of deleted documents
        /// </summary>
        public int Deleted;

        /// <summary>
        /// number of added documents
        /// </summary>
        public int Added;

        /// <summary>
        /// number of updated documents
        /// </summary>
        public int Updated;

        /// <summary>
        /// number of skipped documents (thumbs unavailable or failed to download)
        /// </summary>
        public int Skipped;
    }
}