﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.DocumentStore.Core.Domain.Document;
using Jarvis.DocumentStore.Core.Domain.Document.Events;
using Jarvis.DocumentStore.Core.ReadModel;
using NEventStore;

namespace Jarvis.DocumentStore.Core.EvenstoreHooks
{
    public class ReadModelPromisesHook : PipelineHookBase
    {
        private readonly IHandleWriter _handleWriter;
        private static readonly string DocumentTypeName = typeof (DocumentDescriptor).FullName;

        public ReadModelPromisesHook(IHandleWriter handleWriter)
        {
            _handleWriter = handleWriter;
        }

        public override void PostCommit(ICommit committed)
        {
            if (!committed.Headers.ContainsKey("AggregateType"))
                return;

            var type = (string)committed.Headers["AggregateType"];
            if (type != DocumentTypeName)
                return;

            HandleDocumentCreation(committed);
        }

        void HandleDocumentCreation(ICommit committed)
        {
            var docCreated = committed.Events
                .Where(x => x.Body is DocumentDescriptorCreated)
                .Select(x => (DocumentDescriptorCreated) x.Body)
                .FirstOrDefault();

            if (docCreated != null)
            {
                _handleWriter.Promise(
                    docCreated.HandleInfo.Handle,
                    LongCheckpoint.Parse(committed.CheckpointToken).LongValue
                );
            }
        }
    }
}
