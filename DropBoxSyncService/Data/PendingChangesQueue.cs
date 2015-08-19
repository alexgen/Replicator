using System;
using System.Collections.Generic;
using System.Diagnostics;
using DropboxIndexingService.Models;
using DropboxRestAPI.Models.Core;
using SyncService.Models;

namespace DropboxIndexingService.Data
{
    /// <summary>
    /// Represents list of aggregated changes for a set of documents identified by their IDs.
    /// When changes are added to the set, they get accumulated for each document Id.
    /// </summary>
    public class PendingChangesQueue: Dictionary<Guid, PendingChange>
    {
        public void EnqueueAdd(DocumentIdAndPath document, MetaData meta)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (meta == null) throw new ArgumentNullException("meta");

            PendingChange item;
            // if there is a pending change (it must be remove), replace it with update
            if (TryGetValue(document.Id, out item))
            {
                Debug.Assert(item.Change == DocumentAction.Delete);

                item.Meta = meta;
                item.Change = DocumentAction.Update;
            }
            else
            {
                // if no changes pending, add new one
                Add(document.Id, 
                    new PendingChange
                    {
                        Id = document.Id,
                        Change = DocumentAction.Add,
                        Meta = meta,
                    });
            }

        }
        public void EnqueueUpdate(DocumentIdAndPath document, MetaData meta)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (meta == null) throw new ArgumentNullException("meta");

            PendingChange item;
            // if there is a pending change (it must be add), replace its metadata
            if (TryGetValue(document.Id, out item))
            {
                Debug.Assert(item.Change == DocumentAction.Add || item.Change == DocumentAction.Delete);

                item.Change = DocumentAction.Update;
                item.Meta = meta;
            }
            else
            {
                // if no changes pending, add new one
                Add(document.Id,
                    new PendingChange
                    {
                        Id = document.Id,
                        Change = DocumentAction.Update,
                        Meta = meta,
                    });
            }
           
        }
        public void EnqueueRemove(DocumentIdAndPath document)
        {
            PendingChange item;
            // if there is a pending change, remove it (the change must not be pending delete)
            if (TryGetValue(document.Id, out item))
            {
                Debug.Assert(item.Change != DocumentAction.Delete);
                if (item.Change == DocumentAction.Add)
                {
                    Remove(document.Id);
                }
                else
                {
                    item.Change = DocumentAction.Delete;
                    item.DeletedFilePath = document.FilePath;
                }
            }
            else
            {
                // pend delete now
                Add(document.Id,
                    new PendingChange
                    {
                        Id = document.Id,
                        Change = DocumentAction.Delete,
                        DeletedFilePath = document.FilePath
                    });
            }
        }
    }
}