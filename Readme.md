Replicator is a [work in progress] little library that implements indexing of DropBox files to a custom storage provider. 

It was built with web runtime in mind and intention to provide reusable component which is able to pull file metadata and thumbnails from dropbox.

This library depends on [DropboxRestAPI](https://github.com/saguiitay/DropboxRestAPI), which provides nice async interface into DropBox API in C#.
(It is installed automatically from NuGet when you build the solution)


What it does
------------
* Reads and filters changes from dropbox.
* Downloads thumbnails (if requested) in mutliple connections concurrently and buffers them in memory, which improves overall download performance.
* Handles dropbox API request throttling.
* Updates storage provider by posting batches of changes (add, update, delete).
* Incrementally updates existing storage, so subsequent sync calls can resume from where it had finished.
* Maintains stable mapping for dropbox file paths, so that changes on the same path will reuse allocated Ids.


How to use it
-------------

Implement ISyncContext and IDocumentStorageProvider.
Obtain access token for a dropbox account and specify sync options to the service.
Run Sync method.

See included integration tests for a working example.

    DropBoxClientFactory clientFactory = 
        (token) => new Client(
            new HttpClientFactory(), 
            new RequestGenerator(),  
            new Options()  
            { 
                AccessToken = token, 
            });

    var options = new DropBoxSyncOptions
    {
        Path = "/",
        IncludeSharedFolders = false,
        WithThumbnailsOnly = false,
        ItemsPerBatch = 100,
        DownloadThumbnails = true,
        NumberOfConcurrentDownloads = 10,
        MaxRetryAttempts = 5,
        RetryDelaySeconds = 10,
    };

    var service = new DropBoxSyncService(clientFactory, options);

    // define your context and storage provider
    ...

    var result = await service.Sync(syncContext, storageProvider, progressCallback);

    



