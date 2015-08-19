using System;

namespace SyncService.Models
{
    /// <summary>
    /// Represents a document in a replica snapshot
    /// </summary>
    public class DocumentIdAndPath
    {
        public Guid Id { get; set; }
        public string FilePath { get; set; }
    }
}