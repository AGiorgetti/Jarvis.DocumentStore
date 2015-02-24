﻿using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.DocumentStore.Core.Domain.Document;
using Jarvis.DocumentStore.Core.Domain.Handle.Events;
using Jarvis.DocumentStore.Core.Model;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.IdentitySupport;

namespace Jarvis.DocumentStore.Core.Domain.Handle
{
    public class Handle : AggregateRoot<HandleState>
    {
        public Handle()
        {
        }

        public Handle(HandleState initialState)
            : base(initialState)
        {
        }

        public void Initialize(HandleId id, DocumentHandle handle)
        {
            if (HasBeenCreated)
                throw new DomainException((IIdentity)id, "handle already initialized");
            ThrowIfDeleted();

            RaiseEvent(new HandleInitialized(id, handle));
        }

        public void InitializeAsAttachment(HandleId id, DocumentHandle fatherHandle, DocumentHandle handle)
        {
            if (HasBeenCreated)
                throw new DomainException((IIdentity)id, "handle already initialized");
            ThrowIfDeleted();
            if (fatherHandle == null)
                throw new DomainException((IIdentity)id, "cannot create an attach of an null handle");

            RaiseEvent(new HandleInitialized(id, fatherHandle, handle));
        }

        public void Link(DocumentId documentId)
        {
            ThrowIfDeleted();

            if (InternalState.LinkedDocument != documentId){
                RaiseEvent(new HandleLinked(
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

            RaiseEvent(new HandleFileNameSet(InternalState.Handle, fileName));
        }

        public void SetCustomData(HandleCustomData customData)
        {
            ThrowIfDeleted();

            if (HandleCustomData.IsEquals(InternalState.CustomData, customData))
                return;

            RaiseEvent(new HandleCustomDataSet(InternalState.Handle, customData));
        }

        public void Delete()
        {
            if (!InternalState.HasBeenDeleted)
                RaiseEvent(new HandleDeleted(InternalState.Handle, InternalState.LinkedDocument));
        }

        void ThrowIfDeleted()
        {
            if (InternalState.HasBeenDeleted)
                throw new DomainException((IIdentity)Id, "Handle has been deleted");
        }

      

    }
}
