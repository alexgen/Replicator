namespace SyncService
{
    /// <summary>
    /// Provides externally persisted operational context for sync service
    /// </summary>
    public interface ISyncContext
    {
        /// <summary>
        /// Bearer access token which will be passed to service provider for authorization 
        /// </summary>
        string AccessToken { get; }

        /// <summary>
        /// Indexing cursor - contains serialized state of the data source reader
        /// </summary>
        string Cursor { get; set; }
    }
}