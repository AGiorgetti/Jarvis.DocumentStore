using System.Collections.Generic;
using Jarvis.DocumentStore.Core.Domain.Document;
using Jarvis.DocumentStore.Core.Domain.DocumentDescriptor;
using Jarvis.DocumentStore.Core.Domain.DocumentDescriptor.Events;
using Jarvis.DocumentStore.Core.Model;
using Jarvis.Framework.TestHelpers;
using Machine.Specifications;

namespace Jarvis.DocumentStore.Tests.DomainSpecs.DocumentSpecs
{
    [Subject("DocumentFormats")]
    public class when_document_format_has_been_deleted : DocumentDescriptorSpecifications
    {
        protected static readonly DocumentFormat XmlDocumentFormatId1 = new DocumentFormat("xml");
        protected static readonly BlobId XmlBlobId1 = new BlobId("xml1");

        Establish context =
            () => AggregateSpecification<DocumentDescriptor, DocumentDescriptorState>.SetUp(new DocumentDescriptorState(new KeyValuePair<DocumentFormat, BlobId>(XmlDocumentFormatId1, XmlBlobId1)));

        Because of = () => DocumentDescriptor.DeleteFormat(XmlDocumentFormatId1);

        It DocumentFormatHasBeenDeleted_event_should_have_been_raised =
            () => AggregateSpecification<DocumentDescriptor, DocumentDescriptorState>.EventHasBeenRaised<DocumentFormatHasBeenDeleted>().ShouldBeTrue();

        It document_format_do_not_contain_deleted_format =
            () => AggregateSpecification<DocumentDescriptor, DocumentDescriptorState>.Aggregate.InternalState.Formats.ContainsKey(XmlDocumentFormatId1).ShouldBeFalse();

        It document_format_do_not_contain_blobId =
            () => AggregateSpecification<DocumentDescriptor, DocumentDescriptorState>.Aggregate.InternalState.Formats.Values.ShouldNotContain(XmlBlobId1);
    }
}