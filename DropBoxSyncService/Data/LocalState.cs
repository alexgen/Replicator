using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DropboxRestAPI.Models.Core;
using SyncService.Models;

namespace DropboxIndexingService.Data
{
    /// <summary>
    /// Represents an item in the local state model
    /// </summary>
    public class DocumentStateItem: DocumentIdAndPath
    {
        public DocumentStateItem() {}

        public DocumentStateItem(DocumentIdAndPath document)
        {
            Id = document.Id;
            FilePath = document.FilePath;
        }

        // indicates if specified path was deleted
        public bool IsDeleted { get; set; }
    }


    /// <summary>
    /// Represents local state of a file tree, where input key for an item is path.
    /// It maintains dynamic path-based dictionary for items stored in DB by Id. 
    /// As the state is modified, it aggregates changes for each item.
    /// </summary>
    public class LocalState
    {
        // This dictionary represents current local state of the file tree, indexed by path. 
        private readonly IDictionary<string, DocumentStateItem> _currentState;

        // sorted path list for sub-path search convenience, we need this to detect folders 
        private readonly List<string> _pathIndex;

        // aggregated pending changes (by file Id)
        private readonly PendingChangesQueue _pendingChanges = new PendingChangesQueue();

        public LocalState(IEnumerable<DocumentIdAndPath> currentState)
        {
            _currentState = currentState.Select(d => new DocumentStateItem(d)).ToDictionary(d => d.FilePath);
            _pathIndex = _currentState.Keys.OrderBy(v => v).ToList();
        }

        public LocalState()
        {
            _currentState = new Dictionary<string, DocumentStateItem>();
            _pathIndex = new List<string>();
        }

        public IDictionary<string, DocumentStateItem> Current { get { return _currentState; } }

        public PendingChangesQueue ChangeList { get { return _pendingChanges; } }

        public void Reset()
        {
            foreach (var document in _currentState.Values)
            {
                _pendingChanges.EnqueueRemove(document);

                document.IsDeleted = true;
            }
        }

        public void AddAtPath(string path, MetaData meta)
        {
            // this is add or update action (file)
            // lookup file Id
            DocumentStateItem documentAtPath;
            if (_currentState.TryGetValue(path, out documentAtPath))
            {
                documentAtPath.IsDeleted = false;
                _pendingChanges.EnqueueUpdate(documentAtPath, meta);
            }
            else
            {
                // in case this is a file replacing a folder with sub-items, we need to check to 
                // ensure the sub-items are removed
                if (AnyFilesUnderPath(path))
                {
                    RemoveItemsUnderPath(path);
                }

                // new path, generate new Id
                documentAtPath = new DocumentStateItem
                {
                    Id = Guid.NewGuid(),
                    FilePath = path
                };

                Add(path, documentAtPath);

                _pendingChanges.EnqueueAdd(documentAtPath, meta);
            }            
        }

        public void RemoveAtPath(string path)
        {
            // this is delete action (file or folder)
            // we need to account for the case of recursive deletion of folders 
            // (find files under specified deleted path)

            // first, lookup file at the given path
            DocumentStateItem documentAtPath;
            if (_currentState.TryGetValue(path, out documentAtPath))
            {
                if (!documentAtPath.IsDeleted)
                {
                    // file found, just pend remove for it
                    _pendingChanges.EnqueueRemove(documentAtPath);
                    documentAtPath.IsDeleted = true;
                }
            }
            else
            {
                // in case the delta is delete of a folder, we have to lookup any files stored in the folder
                RemoveItemsUnderPath(path);
            }            
        }

        private void RemoveItemsUnderPath(string path)
        {
            var folderPath = path.EndsWith("/") ? path : path + "/";

            var filesInFolder = FindFilesUnderPath(folderPath);

            if (filesInFolder != null)
                foreach (var filePath in filesInFolder)
                {
                    var fileAtPath = _currentState[filePath];

                    if (!fileAtPath.IsDeleted)
                    {
                        _pendingChanges.EnqueueRemove(fileAtPath);
                        fileAtPath.IsDeleted = true;
                    }
                }
        }

        private bool AnyFilesUnderPath(string path)
        {
            var folderPath = path.EndsWith("/") ? path : path + "/";
            return FindFilesUnderPath(folderPath).Any();
        }

        /// <summary>
        /// Adds specified document to the local state at the specified path
        /// </summary>
        private void Add(string path, DocumentStateItem documentAtPath)
        {
            Debug.Assert(!_currentState.ContainsKey(path), "Specified path already exists in the local state.");

            var insertPos = _pathIndex.BinarySearch(path, StringComparer.InvariantCulture);

            Debug.Assert(insertPos < 0 && ~insertPos <= _pathIndex.Count, "Specified path already exists in the path index.");

            if (insertPos < 0)
            {
                insertPos = ~insertPos;

                if (insertPos < _pathIndex.Count)
                {
                    // insert in the middle
                    _pathIndex.Insert(insertPos, path);
                }
                else
                {
                    // append to the end
                    _pathIndex.Add(path);
                }

                _currentState.Add(path, documentAtPath);
            }

        }

        /// <summary>
        /// Looks up lower and upper bounds for the specified folder path in the list
        /// </summary>
        private IEnumerable<string> FindFilesUnderPath(string folderPath)
        {
            Debug.Assert(folderPath.EndsWith("/"), "folder path is expected to end with a slash");

            List<string> filesAtPath = null;
            // find lower bound
            var startIndex = _pathIndex.BinarySearch(folderPath, StringComparer.InvariantCulture);

            if (startIndex < 0)
            {
                startIndex = ~startIndex;
            }

            if (startIndex < _pathIndex.Count)
            {
                // find upper bound
                var upperBound = folderPath.Substring(0, folderPath.Length - 1) + "~";

                var endIndex = _pathIndex.BinarySearch(startIndex,
                    _pathIndex.Count - startIndex, upperBound, StringComparer.InvariantCulture);

                if (endIndex < 0)
                {
                    endIndex = ~endIndex;
                }

                filesAtPath = _pathIndex.GetRange(
                    startIndex, endIndex - startIndex);
            }

            return filesAtPath ?? new List<string>();
        }
    }
}