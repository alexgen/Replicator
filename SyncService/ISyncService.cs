using System;
using System.Threading.Tasks;
using SyncService.Models;

namespace SyncService
{
    /// <summary>
    /// Represents abstract service for one-way replication of data from a service provider to a storage service
    /// </summary>
    public interface ISyncService
    {
        /// <summary>
        /// Incrementally sync changes from source to a destination storage provider.
        /// </summary>
        /// <param name="syncContext">Context for the sync operation, which includes indexing cursor and access credentials.</param>
        /// <param name="storage">Provides access to the replicate storage.</param>
        /// <param name="progressAction">Progress notification callback, which is called after processing each batch of changes.</param>
        /// <returns>SyncResult which hold summary on number of items processed.</returns>
        Task<SyncResult> Sync(ISyncContext syncContext, IDocumentStorageProvider storage, Func<Task> progressAction = null);
    }
}