using Jarvis.DocumentStore.Core.Domain.Document;
using Jarvis.DocumentStore.Core.Model;
using Jarvis.DocumentStore.Core.Storage;

namespace Jarvis.DocumentStore.Core.Processing.Pipeline
{
    public interface IPipeline
    {
        PipelineId Id { get; }
        bool ShouldHandleFile(DocumentId documentId, IFileDescriptor descriptor);
        void Start(DocumentId documentId, IFileDescriptor descriptor);
        void FormatAvailable(DocumentId documentId, DocumentFormat format, FileId fileId);
        void Attach(IPipelineManager manager);
    }
}