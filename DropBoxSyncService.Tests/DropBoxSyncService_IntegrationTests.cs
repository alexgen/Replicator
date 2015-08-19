using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DropboxIndexingService;
using DropboxRestAPI;
using DropboxRestAPI.Http;
using DropboxRestAPI.RequestsGenerators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SyncService;
using SyncService.Models;

namespace DropBoxIndexingService.Tests
{
    [TestClass]
    public class DropBoxSyncService_IntegrationTests
    {
        /// <summary>
        /// This is an integration test for dropbox sync service
        /// </summary>

        [TestMethod]
        public void Sync()
        {
            // create initial state
            var initialState = new List<DocumentIdAndPath>
            {
                new DocumentIdAndPath
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/a",
                },
                new DocumentIdAndPath
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/b",
                },
                new DocumentIdAndPath
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/c",
                },
            };

            // setup storage mock
            List<Document> addedDocuments, updatedDocuments, removedDocuments;
            var storageProviderMock = CreateStorageProviderMock(initialState, out addedDocuments, out updatedDocuments, out removedDocuments);

            DropBoxClientFactory clientFactory = 
                (token) => new Client(new HttpClientFactory(), new RequestGenerator(),  new Options()  { AccessToken = token, UseSandbox = true });

            var options = new DropBoxSyncOptions
            {
                Path = "/Tests",
                IncludeSharedFolders = false,
                WithThumbnailsOnly = false,
                ItemsPerBatch = 100,
                DownloadThumbnails = true,
                NumberOfConcurrentDownloads = 10,
                MaxRetryAttempts = 5,
                RetryDelaySeconds = 10,
            };

            var service = new DropBoxSyncService(clientFactory, options);

            var contextMock = new Mock<ISyncContext>();
            contextMock.SetupGet(c => c.AccessToken).Returns(ConfigurationManager.AppSettings["DropBoxAccessToken"]);

            var result = service.Sync(contextMock.Object, storageProviderMock.Object).Result;

            Assert.IsNotNull(result);

            Assert.AreEqual(addedDocuments.Count, result.Added);
            Assert.AreEqual(removedDocuments.Count, result.Deleted);
            Assert.AreEqual(updatedDocuments.Count, result.Updated);

        }


        // our mock storage provider will collect all changes committed by sync service
        private static Mock<IDocumentStorageProvider> CreateStorageProviderMock(
            IEnumerable<DocumentIdAndPath> initialState, 
            out List<Document> addedDocuments, 
            out List<Document> updatedDocuments,
            out List<Document> removedDocuments)
        {
            var storageProviderMock = new Mock<IDocumentStorageProvider>();
            storageProviderMock.SetupGet(p => p.StateSnapshot).Returns(initialState);

            var added = new List<Document>();
            var updated = new List<Document>();
            var removed = new List<Document>();

            storageProviderMock
                .Setup(p => p.AddAsync(It.IsAny<IEnumerable<Document>>()))
                .Returns((IEnumerable<Document> docs) => Task.FromResult(docs.Count()))
                .Callback((IEnumerable<Document> docs) => Task.Run(() =>
                {
                    added.AddRange(docs);
                    docs.ToList().ForEach(d => Debug.WriteLine("Added {0}:{1}", d.FilePath, d.Id));
                    
                }));

            storageProviderMock
                .Setup(p => p.UpdateAsync(It.IsAny<IEnumerable<Document>>()))
                .Returns((IEnumerable<Document> docs) => Task.FromResult(docs.Count()))
                .Callback((IEnumerable<Document> docs) => Task.Run(() =>
                {
                    updated.AddRange(docs);
                    docs.ToList().ForEach(d => Debug.WriteLine("Updated {0}:{1}", d.FilePath, d.Id));

                }));

            storageProviderMock
                .Setup(p => p.DeleteAsync(It.IsAny<IEnumerable<Document>>()))
                .Returns((IEnumerable<Document> docs) => Task.FromResult(docs.Count()))
                .Callback((IEnumerable<Document> docs) => Task.Run(() =>
                {
                    removed.AddRange(docs);
                    docs.ToList().ForEach(d => Debug.WriteLine("Deleted {0}:{1}", d.FilePath, d.Id));
                }));

            addedDocuments = added;
            updatedDocuments = updated;
            removedDocuments = removed;

            return storageProviderMock;
        }
    }
}
