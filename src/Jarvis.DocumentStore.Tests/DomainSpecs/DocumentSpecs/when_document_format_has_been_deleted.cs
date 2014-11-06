using System.Collections.Generic;
using CQRS.TestHelpers;
using Jarvis.DocumentStore.Core.Domain.Document;
using Jarvis.DocumentStore.Core.Domain.Document.Events;
using Jarvis.DocumentStore.Core.Model;
using Machine.Specifications;

namespace Jarvis.DocumentStore.Tests.DomainSpecs.DocumentSpecs
{
    [Subject("DocumentFormats")]
    public class when_document_format_has_been_deleted : DocumentSpecifications
    {
        protected static readonly DocumentFormat XmlDocumentFormatId1 = new DocumentFormat("xml");
        protected static readonly BlobId XmlBlobId1 = new BlobId("xml1");

        Establish context =
            () => AggregateSpecification<Core.Domain.Document.Document, DocumentState>.SetUp(new DocumentState(new KeyValuePair<DocumentFormat, BlobId>(XmlDocumentFormatId1, XmlBlobId1)));

        Because of = () => Document.DeleteFormat(XmlDocumentFormatId1);

        It DocumentFormatHasBeenDeleted_event_should_have_been_raised =
            () => AggregateSpecification<Core.Domain.Document.Document, DocumentState>.EventHasBeenRaised<DocumentFormatHasBeenDeleted>().ShouldBeTrue();

        It document_format_do_not_contain_deleted_format =
            () => AggregateSpecification<Core.Domain.Document.Document, DocumentState>.Aggregate.InternalState.Formats.ContainsKey(XmlDocumentFormatId1).ShouldBeFalse();

        It document_format_do_not_contain_blobId =
            () => AggregateSpecification<Core.Domain.Document.Document, DocumentState>.Aggregate.InternalState.Formats.Values.ShouldNotContain(XmlBlobId1);
    }
}