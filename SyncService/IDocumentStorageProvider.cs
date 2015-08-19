using System.Collections.Generic;
using System.Threading.Tasks;
using SyncService.Models;

namespace SyncService
{
    /// <summary>
    /// Storage provider for document metadata
    /// </summary>
    public interface IDocumentStorageProvider
    {
        /// <summary>
        /// Provides a snapshot of the initial replicate state in a form of document id-path map
        /// </summary>
        IEnumerable<DocumentIdAndPath> StateSnapshot { get; }

        /// <summary>
        /// Add documents to the storage
        /// </summary>
        /// <returns>number of successfully added</returns>
        Task<int> AddAsync(IEnumerable<Document> documents);

        /// <summary>
        /// Update documents in the storage
        /// </summary>
        /// <returns>number of successfully updated</returns>
        Task<int> UpdateAsync(IEnumerable<Document> documents);

        /// <summary>
        /// Delete documents to the storage
        /// </summary>
        /// <returns>number of successfully deleted</returns>
        Task<int> DeleteAsync(IEnumerable<Document> documents);
    }
}