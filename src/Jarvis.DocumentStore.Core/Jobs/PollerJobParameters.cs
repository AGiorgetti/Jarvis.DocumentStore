﻿using System;
using System.Collections.Generic;
using CQRS.Shared.MultitenantSupport;
using Jarvis.DocumentStore.Core.Domain.Document;
using Jarvis.DocumentStore.Core.Model;

namespace Jarvis.DocumentStore.Core.Jobs
{
    public class PollerJobParameters
    {
        public QueuedJobId JobId { get; set; }

        public DocumentFormat InputDocumentFormat { get; set; }

        public TenantId TenantId { get; set; }

        public String FileExtension { get; set; }

        public Dictionary<String, String> All { get; set; }

        public Int32 GetIntOrDefault(string key, int defaultvalue)
        {
            int outValue;
            if (All.ContainsKey(key) &&
                int.TryParse(All[key], out outValue)) return outValue;

            return defaultvalue;
        }
    }
}