using System.Collections.Generic;
using System.Linq;
using Jarvis.DocumentStore.Core.Domain.DocumentDescriptor.Events;
using Jarvis.DocumentStore.Core.Model;
using Jarvis.Framework.Kernel.Engine;

namespace Jarvis.DocumentStore.Core.Domain.DocumentDescriptor
{
    public class DocumentDescriptorState : AggregateState
    {
        private readonly HashSet<DocumentHandle> _handles = new Quartz.Collection.HashSet<DocumentHandle>();

        public DocumentDescriptorState(params KeyValuePair<DocumentFormat, BlobId>[] formats)
            : this()
        {
            foreach (var keyValuePair in formats)
            {
                Formats.Add(keyValuePair);
            }
        }

        public DocumentDescriptorState()
        {
            Formats = new Dictionary<DocumentFormat, BlobId>();
        }

        public IDictionary<DocumentFormat, BlobId> Formats { get; private set; }
        public BlobId BlobId { get; private set; }

        public bool HasBeenDeleted { get; private set; }

        private void When(DocumentDescriptorDeleted e)
        {
            HasBeenDeleted = true;
        }

        private void When(DocumentDescriptorCreated e)
        {
            BlobId = e.BlobId;
        }

        private void When(FormatAddedToDocumentDescriptor e)
        {
            Formats.Add(e.DocumentFormat, e.BlobId);
        }

        private void When(DocumentFormatHasBeenDeleted e)
        {
            Formats.Remove(e.DocumentFormat);
        }

        private void When(DocumentHandleAttached e)
        {
            _handles.Add(e.Handle);
        }

        private void When(DocumentHandleDetached e)
        {
            _handles.Remove(e.Handle);
        }

        public bool HasFormat(DocumentFormat documentFormat)
        {
            return Formats.ContainsKey(documentFormat);
        }

        public bool IsValidHandle(DocumentHandle handle)
        {
            return _handles.Contains(handle);
        }

        public bool HasActiveHandles()
        {
            return _handles.Any();
        }
    }
}