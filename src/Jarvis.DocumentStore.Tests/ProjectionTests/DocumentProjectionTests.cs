﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.DocumentStore.Core.Domain.Document;
using Jarvis.DocumentStore.Core.Domain.Document.Events;
using Jarvis.DocumentStore.Core.Domain.DocumentDescriptor;
using Jarvis.DocumentStore.Core.EventHandlers;
using Jarvis.DocumentStore.Core.Model;
using Jarvis.DocumentStore.Core.ReadModel;
using Jarvis.DocumentStore.Tests.Support;
using Jarvis.Framework.Shared.Domain.Serialization;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using NUnit.Framework;

namespace Jarvis.DocumentStore.Tests.ProjectionTests
{
    [TestFixture]
    public class DocumentProjectionTests
    {

        readonly DocumentHandle _documentHandle = new DocumentHandle("a");
        readonly DocumentHandle _attachmentHandle = new DocumentHandle("attach1");
        readonly DocumentHandle _attachmentNestedHandle = new DocumentHandle("attach2");
        readonly DocumentId _documentId1 = new DocumentId(1);
        readonly DocumentId _documentId2 = new DocumentId(2);
        readonly DocumentId _documentId3 = new DocumentId(3);
        readonly DocumentDescriptorId _document1 = new DocumentDescriptorId(1);
        readonly DocumentDescriptorId _document2 = new DocumentDescriptorId(2);
        readonly FileNameWithExtension _fileName1 = new FileNameWithExtension("a", "file");

        DocumentWriter _writer;
        DocumentProjection _sut;

        [SetUp]
        public void SetUp()
        {
            MongoDbTestConnectionProvider.ReadModelDb.Drop();

            var mngr = new IdentityManager(new CounterService(MongoDbTestConnectionProvider.ReadModelDb));
            mngr.RegisterIdentitiesFromAssembly(typeof(DocumentDescriptorId).Assembly);

            EventStoreIdentityBsonSerializer.IdentityConverter = mngr;

            EventStoreIdentityCustomBsonTypeMapper.Register<DocumentDescriptorId>();
            EventStoreIdentityCustomBsonTypeMapper.Register<DocumentId>();
            StringValueCustomBsonTypeMapper.Register<BlobId>();
            StringValueCustomBsonTypeMapper.Register<DocumentHandle>();
            StringValueCustomBsonTypeMapper.Register<FileHash>();

            _writer = new DocumentWriter(MongoDbTestConnectionProvider.ReadModelDb);
            _sut = new DocumentProjection(_writer);
        }

        [Test]
        public void Promise()
        {
            _writer.Promise(_documentHandle, 1);

            var h = _writer.FindOneById(_documentHandle);
            Assert.NotNull(h);
            Assert.IsNull(h.DocumentId);
            Assert.AreEqual(0, h.ProjectedAt);
            Assert.AreEqual(1, h.CreatetAt);
            Assert.IsNull(h.FileName);
        }

        [Test]
        public void Create()
        {
            _writer.Create(_documentHandle);
            var h = _writer.FindOneById(_documentHandle);

            Assert.NotNull(h);
            Assert.IsNull(h.DocumentId);
            Assert.AreEqual(0, h.ProjectedAt);
            Assert.AreEqual(0, h.CreatetAt);
        }

        [Test]
        public void SetFileName()
        {
            _writer.Create(_documentHandle);
            _writer.SetFileName(_documentHandle, _fileName1, 10 );
            var h = _writer.FindOneById(_documentHandle);

            Assert.AreEqual(_fileName1, h.FileName);
        
        }

        [Test]
        public void add_attachment()
        {
            _sut.On(new DocumentInitialized(_documentId1, _documentHandle) {AggregateId =  _documentId1});
            _sut.On(new DocumentHasNewAttachment(_documentHandle, _attachmentHandle) { AggregateId = _documentId1 });

            var h = _writer.FindOneById(_documentHandle);
            Assert.That(h.Attachments, Is.EquivalentTo(new [] { _attachmentHandle }));
        }

        [Test]
        public void add_attachment_then_delete()
        {
            _sut.On(new DocumentInitialized(_documentId1, _documentHandle) { AggregateId = _documentId1 });
            _sut.On(new DocumentHasNewAttachment(_documentHandle, _attachmentHandle) { AggregateId = _documentId2 });
            _sut.On(new DocumentDeleted(_attachmentHandle, _document2) { AggregateId = _documentId1 });

            var h = _writer.FindOneById(_documentHandle);
            Assert.That(h.Attachments, Is.Empty);
        }

        [Test]
        public void add_attachment_nested()
        {
            _sut.On(new DocumentInitialized(_documentId1, _documentHandle) { AggregateId = _documentId1 });
            _sut.On(new DocumentInitialized(_documentId2, _documentHandle) { AggregateId = _documentId2 });
            _sut.On(new DocumentInitialized(_documentId3, _documentHandle) { AggregateId = _documentId3 });

            _sut.On(new DocumentHasNewAttachment(_documentHandle, _attachmentHandle) { AggregateId = _documentId1 });
            _sut.On(new DocumentHasNewAttachment(_attachmentHandle, _attachmentNestedHandle) { AggregateId = _documentId1 });

            var h = _writer.FindOneById(_documentHandle);
            Assert.That(h.Attachments, Is.EquivalentTo(new[] { _attachmentHandle, _attachmentNestedHandle }));

            h = _writer.FindOneById(_attachmentHandle);
            Assert.That(h.Attachments, Is.EquivalentTo(new[] { _attachmentNestedHandle }));
        }

        [Test]
        public void update_custom_data()
        {
            _writer.Create(_documentHandle);
            var handleCustomData = new DocumentCustomData() { { "a", "b" } };
            _writer.UpdateCustomData(_documentHandle, handleCustomData);
            var h = _writer.FindOneById(_documentHandle);

            Assert.NotNull(h.CustomData);
            Assert.AreEqual("b", (string)h.CustomData["a"]);
        }

        [Test]
        [TestCase(2, 10, false)]
        [TestCase(2, 11, false)]
        [TestCase(1, 9, true)]
        public void Projected(int expectedDocId, int projectedAt, bool isPending)
        {
            var expectedDocumentId = new DocumentDescriptorId(expectedDocId);
            _writer.Promise(_documentHandle, 10);
            _writer.LinkDocument(_documentHandle, _document2, projectedAt);

            var h = _writer.FindOneById(_documentHandle);
            Assert.NotNull(h);
            Assert.AreEqual(10, h.CreatetAt);
            Assert.IsNull(h.FileName);

            if (h.ProjectedAt >= h.CreatetAt)
            {
                Assert.AreEqual(expectedDocumentId, h.DocumentId);
                Assert.AreEqual(projectedAt, h.ProjectedAt);
            }

            Assert.AreEqual(isPending, h.IsPending());
        }

        [Test]
        public void should_delete()
        {
            _writer.Create(_documentHandle);
            _writer.Promise(_documentHandle, 10);
            
            _writer.Delete(_documentHandle, 11);
            var h = _writer.FindOneById(_documentHandle);
            Assert.IsNull(h);
        }

        [Test]
        public void should_not_delete()
        {
            _writer.Create(_documentHandle);
            _writer.Promise(_documentHandle, 10);
            
            _writer.Delete(_documentHandle, 9);
            var h = _writer.FindOneById(_documentHandle);
            Assert.IsNotNull(h);
        }

        [Test]
        public void should_not_update_a_deleted_handle()
        {
            // arrage
            _writer.Create(_documentHandle);
            _writer.Promise(_documentHandle, 10);

            var h1 = _writer.FindOneById(_documentHandle);

            // act
            _writer.Delete(_documentHandle, 20);
            _writer.LinkDocument(_documentHandle, _document2, 15);
            var h2 = _writer.FindOneById(_documentHandle);
            _writer.LinkDocument(_documentHandle, _document2, 55);
            var h3 = _writer.FindOneById(_documentHandle);

            // assert
            Assert.IsNotNull(h1);
            Assert.IsNull(h2);
            Assert.IsNull(h3);
        }
    }
}
