using System;
using System.Collections.Generic;
using System.Linq;
using DropboxIndexingService.Data;
using DropboxIndexingService.Extensions;
using DropboxIndexingService.Models;
using DropboxRestAPI.Models.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SyncService.Models;

namespace DropBoxIndexingService.Tests
{
    public class LocalStateFixture
    {
        protected static MetaData CreateMetadata(string path)
        {
            return new MetaData
            {
                bytes = 123,
                client_mtime = DateTime.UtcNow.ToDropBoxTimeString(),
                modified = DateTime.UtcNow.ToDropBoxTimeString(),
                path = path,
                photo_info = new PhotoInfo { time_taken = DateTime.UtcNow.ToDropBoxTimeString() },
                thumb_exists = true
            };
        }        
    }

    [TestClass]
    public class WhenLocalStateIsEmpty : LocalStateFixture
    {

        protected readonly LocalState State = new LocalState();


        [TestMethod]
        public void AddAtPath()
        {
            // add new item to the local state
            State.AddAtPath("/test", CreateMetadata("/test"));

            // this should yield single pending change
            Assert.AreEqual(1, State.ChangeList.Count);

            // the local state should have one item
            Assert.AreEqual(1, State.Current.Count);

        }

        [TestMethod]
        public void AddAtPath_RemoveAtPath()
        {
            // add new item to the local state
            State.AddAtPath("/test", CreateMetadata("/test"));
            // then remove the added item
            State.RemoveAtPath("/test");

            // this should not have any changes pending
            Assert.AreEqual(0, State.ChangeList.Count);

            // the local state should keep deleted item marked accordingly
            Assert.AreEqual(1, State.Current.Count);
            Assert.IsTrue(State.Current["/test"].IsDeleted);
        }



        [TestMethod]
        public void RemoveAtPath()
        {
            State.RemoveAtPath("/test");

            // this should not have any changes pending
            Assert.AreEqual(0, State.ChangeList.Count);

            // the local state should be clear
            Assert.AreEqual(0, State.Current.Count);
        }
    }

    [TestClass]
    public class WhenLocalStateHasAnItem : LocalStateFixture
    {
        private const string TestPath = "/test";

        protected readonly LocalState State;

        public WhenLocalStateHasAnItem()
        {
            var dbstate = new List<DocumentIdAndPath>
            {
                new DocumentIdAndPath
                {
                    Id = Guid.NewGuid(),
                    FilePath = TestPath,
                }
            };

            State = new LocalState(dbstate);
        }

        [TestMethod]
        public void AddAtPath()
        {
            // adding a new item at the path must generate update
            State.AddAtPath(TestPath, CreateMetadata(TestPath));

            // this should yield single pending change
            Assert.AreEqual(1, State.ChangeList.Count);

            // the local state should have one item
            Assert.AreEqual(1, State.Current.Count);

        }

        [TestMethod]
        public void RemoveAtPath()
        {
            State.RemoveAtPath(TestPath);

            Assert.AreEqual(1, State.ChangeList.Count);

            // the local state should have the same item marked as deleted
            Assert.AreEqual(1, State.Current.Count);
            Assert.IsTrue(State.Current[TestPath].IsDeleted);
            
        }

        [TestMethod]
        public void RemoveAtPath_AddAtPath()
        {
            State.RemoveAtPath(TestPath);
            State.AddAtPath(TestPath, CreateMetadata(TestPath));

            // this should yield single pending change
            Assert.AreEqual(1, State.ChangeList.Count);

            // the local state should have one item
            Assert.AreEqual(1, State.Current.Count);
        }
    }

    [TestClass]
    public class WhenLocalStateHasMultipleItemsInAFolder : LocalStateFixture
    {
        private const string TestFolderPath = "/test";

        private readonly List<string> _files = new List<string>
        {
            "/abc",
            TestFolderPath + "/file1",
            TestFolderPath + "/file3",
            "/testFile4"
        };

        protected readonly LocalState State;

        public WhenLocalStateHasMultipleItemsInAFolder()
        {
            var dbstate = new List<DocumentIdAndPath>();
            _files.ForEach(f => dbstate.Add(CreateDocumentIdAndPath(f)));

            State = new LocalState(dbstate);
        }

        private static DocumentIdAndPath CreateDocumentIdAndPath(string path)
        {
            return new DocumentIdAndPath
            {
                FilePath = path,
                Id = Guid.NewGuid()
            };
        }

        [TestMethod]
        public void RemoveParentFolder()
        {
            State.RemoveAtPath(TestFolderPath);

            // this should yield pending change for each file in the folder
            Assert.AreEqual(2, State.ChangeList.Count);

            // the local state should keep all items, with 2 of them marked as deleted
            Assert.AreEqual(_files.Count, State.Current.Count);

            Assert.AreEqual(2, State.Current.Values.Count(f => f.IsDeleted));
        }

    }
}
