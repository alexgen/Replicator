using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DropboxIndexingService.Data;
using DropboxIndexingService.Extensions;
using DropboxIndexingService.Helpers;
using DropboxIndexingService.Models;
using DropboxRestAPI;
using DropboxRestAPI.Models.Core;
using DropboxRestAPI.Models.Exceptions;
using DropboxRestAPI.Utils;
using SyncService;
using SyncService.Models;

namespace DropboxIndexingService
{
    public delegate DropboxRestAPI.IClient DropBoxClientFactory(string accessToken);

    public class DropBoxSyncService : ISyncService
    {
        private readonly DropBoxClientFactory _clientFactory;
        private readonly DropBoxSyncOptions _options;

        public const long Kbyte = 1024;
        public const long Mbyte = 1024 * Kbyte;

        public DropBoxSyncService(DropBoxClientFactory clientFactory, DropBoxSyncOptions options)
        {
            if (clientFactory == null) throw new ArgumentNullException("clientFactory");
            if (options == null) throw new ArgumentNullException("options");

            _clientFactory = clientFactory;
            _options = options;
        }

        public async Task<SyncResult> Sync(
            ISyncContext syncContext,
            IDocumentStorageProvider storage,
            Func<Task> progressAction = null)
        {
            if (syncContext == null) throw new ArgumentNullException("syncContext");
            if (syncContext.AccessToken == null) throw new ArgumentException("An access token must be provided");

            if (String.IsNullOrEmpty(syncContext.AccessToken))
                throw new InvalidOperationException("Specified data context does not have valid token.");

            var dropbox = _clientFactory(syncContext.AccessToken);

            // read delta using last indexing state
            // NB: this may update sync context, which should be persisted by the caller to enable reuse of the cursor
            var readResult = await ReadDelta(syncContext, storage, dropbox, progressAction);

            Debug.Assert(readResult != null);

            if (readResult.ChangesProcessed <= 0)
                return new SyncResult();

            Debug.Assert(readResult.PendingChanges != null);

            // apply accumulated changes
            var applyResult = await ApplyPendingChanges(storage, dropbox, readResult.PendingChanges, progressAction);

            return applyResult.ToIndexingResult(readResult.ChangesProcessed);
        }

        internal class ReadDeltaResult
        {
            public int ChangesProcessed;
            public ICollection<PendingChange> PendingChanges;
        }

        private async Task<ReadDeltaResult> ReadDelta(ISyncContext context, IDocumentStorageProvider storage, IClient dropbox, Func<Task> progressAction)
        {
            Entries delta;

            // we will load local state lazily upon first use
            var localState = new Lazy<LocalState>(() => new LocalState(storage.StateSnapshot));

            var numChangesProcessed = 0;
            var cursor = context.Cursor;
            do
            {
                delta = await dropbox.Core.Metadata.DeltaAsync(cursor, path_prefix: _options.Path, include_media_info: true);

                Debug.Assert(delta != null);
                Debug.Assert(delta.entries != null);

                // if this is a reset notification (or we start with an empty cursor), 
                // we should reset local state and then restore it by iterating through delta
                if (delta.reset || String.IsNullOrEmpty(cursor))
                {
                    localState.Value.Reset();
                    numChangesProcessed ++;
                }

                foreach (var entry in delta.entries.Where(IsDeleteOrAddFileDelta))
                {
                    numChangesProcessed++;

                    var path = entry.Item1.ToLowerInvariant();

                    if (entry.Item2 == null)
                    {
                        localState.Value.RemoveAtPath(path);
                    }
                    else if (!entry.Item2.is_dir)
                    {
                        localState.Value.AddAtPath(path, entry.Item2);
                    }
                }

                cursor = delta.cursor;

                if (progressAction != null)
                    await progressAction();

            } while (delta.has_more);

            context.Cursor = cursor;


            return new ReadDeltaResult
            {
                ChangesProcessed = numChangesProcessed,
                PendingChanges = localState.IsValueCreated
                    ? localState.Value.ChangeList.Values
                    : null // no changes to process
            };
        }

        private static bool IsDeleteOrAddFileDelta(Tuple<string, MetaData> arg)
        {
            return arg.Item2 == null || !arg.Item2.is_dir;
        }


        internal class ApplyChangesResult
        {
            public int Deleted;
            public int Added;
            public int Updated;

            public int Skipped;

            public SyncResult ToIndexingResult(int sourceChangesProcessed)
            {
                return new SyncResult
                {
                    SourceChangesProcessed = sourceChangesProcessed,

                    Added = this.Added,
                    Updated = this.Updated,
                    Deleted = this.Deleted,
                    Skipped = this.Skipped
                };
            }
        }

        /// <summary>
        /// Process pending changes - download thumbnails and update destination with batch document operations.
        /// </summary>
        private async Task<ApplyChangesResult> ApplyPendingChanges(
            IDocumentStorageProvider storage, 
            IClient dropbox, 
            ICollection<PendingChange> pendingChanges, 
            Func<Task> batchProgressAction)
        {
            var result = new ApplyChangesResult();

            // filter changes according to specified options
            var filteredChanges = pendingChanges
                .Where(c => 
                    c.Change == DocumentAction.Delete 
                    || !_options.WithThumbnailsOnly || c.Meta.thumb_exists 
                    || _options.IncludeSharedFolders || String.IsNullOrEmpty(c.Meta.parent_shared_folder_id)
                    );

            var itemsProcessed = 0;
             
            // split accumulated changes to smaller batches to enable progress reporting and status checks
            foreach (var batch in filteredChanges.Chunkify(_options.ItemsPerBatch))
            {
                Debug.WriteLine("Processing next batch of changes [{0} / {1}]", itemsProcessed, pendingChanges.Count);

                // these will buffer metadata updates
                var documentsToDelete = new List<Document>();
                var documentsToUpdate = new List<Document>();
                var documentsToAdd = new List<Document>();

                // create a list for thumbnail download tasks
                var thumbsToDownload = new List<PendingChange>();

                // sort through updates and adds
                foreach (var change in batch)
                {
                    if (change.Change != DocumentAction.Delete 
                        && _options.DownloadThumbnails && change.Meta.thumb_exists)
                    {
                        thumbsToDownload.Add(change);
                    }
                    else
                    {
                        var document = change.ToDocument();

                        switch (change.Change)
                        {
                            case DocumentAction.Add:
                                documentsToAdd.Add(document);
                                break;
                            case DocumentAction.Update:
                                documentsToUpdate.Add(document);
                                break;
                            case DocumentAction.Delete:
                                document.FilePath = change.DeletedFilePath;
                                documentsToDelete.Add(document);
                                break;
                        }                        
                    }

                }

                var downloadCount = await DownloadThumbnails(dropbox, thumbsToDownload, documentsToAdd, documentsToUpdate);
                result.Skipped += thumbsToDownload.Count - downloadCount;

                // now flush accumulated changes
                if (documentsToDelete.Count > 0)
                {
                    Debug.WriteLine("Deleting documents [{0}]", documentsToDelete.Count);
                    result.Deleted += await storage.DeleteAsync(documentsToDelete);
                }

                if (documentsToAdd.Count > 0)
                {
                    Debug.WriteLine("Adding documents [{0}]", documentsToUpdate.Count + documentsToAdd.Count);
                    result.Added += await storage.AddAsync(documentsToAdd);                    
                }
                if (documentsToUpdate.Count > 0)
                {
                    Debug.WriteLine("Updating documents [{0}]", documentsToUpdate.Count + documentsToAdd.Count);
                    result.Updated += await storage.UpdateAsync(documentsToUpdate);
                }

                if (batchProgressAction != null)
                    await batchProgressAction();

                itemsProcessed += _options.ItemsPerBatch;
            }

            return result;
        }

        private async Task<int> DownloadThumbnails(IClient dropbox, List<PendingChange> thumbsToDownload, List<Document> documentsToAdd, List<Document> documentsToUpdate)
        {
            if (thumbsToDownload.Count == 0)
                return 0;

            Debug.WriteLine("Downloading thumbnails [{0}]", thumbsToDownload.Count);

            var throttle = new AsyncManualResetEvent(true); // setting event opens up the barrier

            // concurrently download all thumbnails
            var downloadResult = await thumbsToDownload.ConcurrentForEachAsync(
                _options.NumberOfConcurrentDownloads,
                c => DownloadSingeThumbnailAsync(c, dropbox, throttle));

            var downloadCount = 0;

            // now process thumbnail transfer results
            foreach (var download in downloadResult)
            {
                var document = download.Change.ToDocument();

                // skip updates or additions if we could not get thumbnail which was requested
                if (download.Success)
                {
                    ++downloadCount;

                    if (download.Change.Change == DocumentAction.Add)
                    {
                        documentsToAdd.Add(document);
                    }
                    else if (download.Change.Change == DocumentAction.Update)
                    {
                        documentsToUpdate.Add(document);
                    }
                }
            }

            return downloadCount;
        }

        internal class ThumbTransferResult
        {
            public bool Success;
            public PendingChange Change;
        }

        /// <summary>
        /// Creates a task for async download of thumbnail corresponding to the updated document.
        /// </summary>
        /// <param name="change">Change record that includes document state and new metadata.</param>
        /// <param name="dropbox">DropBox client</param>
        /// <param name="throttle"></param>
        /// <param name="options"></param>
        /// <returns>An awaitable task that yields status and associated change record.</returns>
        private async Task<ThumbTransferResult> DownloadSingeThumbnailAsync(
            PendingChange change, IClient dropbox, AsyncManualResetEvent throttle)
        {
            var thumbSize = 0;

            const int bufferSize = (int) (150*Kbyte); // this should be sufficient for most thumbnails

            var attempt = 0;
            TimeSpan? retryDelay = null;

            do
                try
                {
                    if (attempt >= _options.MaxRetryAttempts)
                        break;

                    ++attempt;

                    if (retryDelay.HasValue)
                    {
                        Debug.WriteLine("Pausing {0} seconds before retrying.", retryDelay.Value.TotalSeconds);
                        await Task.Delay(retryDelay.Value);
                        retryDelay = null;
                    }

                    // pause if we are being throttled by DropBox
                    await throttle.WaitAsync();

                    var itemPath = change.Meta.path;

                    using (var thumbStream = await dropbox.Core.Metadata.ThumbnailsAsync(itemPath, size: _options.ThumbSizeToRequest))
                    using (var buffer = new MemoryStream())
                    {
                        await thumbStream.CopyToAsync(buffer, bufferSize);
                        change.Thumbnail = buffer.ToArray();

                        thumbSize = change.Thumbnail.Length;
                    }

                    break;
                }
                // we need to eat this exception, to prevent terminating processing for other thumbnails
                catch (TaskCanceledException /*e*/)
                {
                    if (attempt > 1)
                    {
                        // after first attempt we will retry, after second, break out to return failed result
                        Debug.WriteLine("DownloadSingeThumbnailAsync task was canceled.");
                        break;
                    }
                }
                catch (System.Net.WebException)
                {
                    // just retry after failure
                    Debug.WriteLine("Got WebException.");
                }
                catch (HttpException e)
                {
                    // in case of network error - make short delay and retry
                    if (e.HttpCode == 0 && attempt < 3)
                    {
                        retryDelay = TimeSpan.FromSeconds(_options.RetryDelaySeconds);
                        Debug.WriteLine("Got HttpException.");
                    }
                    else break; // this will end the task with a failure code
                }
                catch (System.IO.IOException /*e*/)
                {
                    retryDelay = TimeSpan.FromSeconds(_options.RetryDelaySeconds);
                    Debug.WriteLine("Got IOException.");
                }
                catch (RetryLaterException e)
                {
                    // reset throttle to delay concurrent connections if it was not already set by a different task
                    if (throttle.WaitAsync().IsCompleted)
                    {
                        // reset to make all other tasks wait
                        throttle.Reset();

                        var delay = TimeSpan.FromSeconds((double)(e.RetryAfter ?? _options.RetryDelaySeconds));

                        Debug.WriteLine("The request is being throttled. Pausing for {0} seconds...", delay.TotalSeconds);
                        Task.Delay(delay).Wait();

                        // set it back
                        throttle.Set();
                    }
                }
            while (true);

            if (thumbSize > 0)
            {
                Debug.WriteLine("  Downloaded thumbnail for file {0}[{1}Kb]", change.Meta.path, thumbSize/Kbyte);
            }
            else
            {
                Debug.WriteLine("  Failed downloading thumbnail for file " + change.Meta.path);
            }

            return new ThumbTransferResult
            {
                Success = thumbSize > 0, 
                Change = change
            };
        }
    }
}
