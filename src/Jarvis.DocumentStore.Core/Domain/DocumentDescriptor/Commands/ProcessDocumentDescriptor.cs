using Jarvis.DocumentStore.Core.Model;

namespace Jarvis.DocumentStore.Core.Domain.DocumentDescriptor.Commands
{
    public class ProcessDocumentDescriptor : DocumentDescriptorCommand
    {
        public ProcessDocumentDescriptor(DocumentDescriptorId aggregateId, DocumentHandle handle) : base(aggregateId)
        {
            Handle = handle;
        }

        public DocumentHandle Handle { get; private set; }
    }
}