using System;

namespace SyncService.Models
{
    /// <summary>
    /// Represents complete document obtained from the sync source 
    /// </summary>
    public class Document
    {
        public Guid Id { get; set; }
        public string Filename { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public ulong FileSize { get; set; }
        public string FilePath { get; set; }

        // the following fields will be populated in case there is meaningful metadata in the embedded image metadata
        public DateTime? TakenAt { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Actual thumbnail of the document, if available
        public byte[] Thumbnail { get; set; }

    }
}
