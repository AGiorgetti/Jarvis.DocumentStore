﻿using System;
using System.Linq;
using Jarvis.DocumentStore.Core.Domain.Document.Events;
using Jarvis.DocumentStore.Core.Domain.DocumentDescriptor;
using Jarvis.DocumentStore.Core.Model;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;

namespace Jarvis.DocumentStore.Core.Domain.Document
{
    public class Document : AggregateRoot<DocumentState>
    {
        public Document()
        {
        }

        public void Initialize(DocumentId id, DocumentHandle handle)
        {

            ThrowIfDeleted();

            RaiseEvent(new DocumentInitialized(handle));
        }

        public void Link(DocumentDescriptorId documentId)
        {
            ThrowIfDeleted();

            if (InternalState.LinkedDocument != documentId){
                RaiseEvent(new DocumentLinked(
                    InternalState.Handle, 
                    documentId, 
                    InternalState.LinkedDocument,
                    InternalState.FileName
                ));
            }
        }

        public void SetFileName(FileNameWithExtension fileName)
        {
            ThrowIfDeleted();
            if (InternalState.FileName == fileName)
                return;

            RaiseEvent(new DocumentFileNameSet(InternalState.Handle, fileName));
        }

        public void SetCustomData(DocumentCustomData customData)
        {
            ThrowIfDeleted();

            if (DocumentCustomData.IsEquals(InternalState.CustomData, customData))
                return;

            RaiseEvent(new DocumentCustomDataSet(InternalState.Handle, customData));
        }

        public void Delete()
        {
            if (!InternalState.HasBeenDeleted)
            {
                RaiseEvent(new DocumentDeleted(InternalState.Handle, InternalState.LinkedDocument));
            }
        }

        internal void CopyDocument(DocumentHandle copiedHandle)
        {
            ThrowIfDeleted();
            if (InternalState.LinkedDocument == null)
                throw new DomainException((IIdentity)Id, "Cannot copy document not linked to any Document Descriptor");

            var handleInfo = new DocumentHandleInfo(copiedHandle, InternalState.FileName, InternalState.CustomData);
            RaiseEvent(new DocumentCopied(copiedHandle, InternalState.LinkedDocument, handleInfo));
        }

        void ThrowIfDeleted()
        {
            if (InternalState.HasBeenDeleted)
                throw new DomainException((IIdentity)Id, "Handle has been deleted");
        }

      
    }
}
