using System;
using DropboxRestAPI.Models.Core;

namespace DropboxIndexingService.Models
{
    /// <summary>
    /// Represents accumulated change for a single document
    /// </summary>
    public class PendingChange
    {
        public DocumentAction Change;

        public Guid Id;

        public string DeletedFilePath;

        public MetaData Meta;

        public byte[] Thumbnail { get; set; }
    }
}