﻿using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp;
using Jarvis.DocumentStore.Shared.Model;

namespace Jarvis.DocumentStore.Jobs.Tika
{
    public static class ContentFormatBuilder
    {
        public static DocumentContent CreateFromTikaPlain(String tikaFullContent)
        {
            if (String.IsNullOrEmpty(tikaFullContent)) return DocumentContent.NullContent;

            var doc = DocumentBuilder.Html(tikaFullContent);

            var allMeta = doc.QuerySelectorAll("meta");

            var meta = allMeta.SelectMany(x => x.Attributes.Select(y => new
            {
                key = y.Name,
                value = y.Value
            }))
                .GroupBy(g => g.key)
            .SelectMany(grp => grp.Count() == 1
                ? grp
                : grp.Select((x, i) => new
                {
                    key = x.key + "-" + (i + 1),
                    value = x.value
                }))
            .Select(x => new DocumentContent.MetadataHeader(x.key, x.value))
            .ToList();

            var pages = doc.QuerySelectorAll("div.page");
            var pagesList = new List<DocumentContent.DocumentPage>();
            for (int i = 0; i < pages.Length; i++)
            {
                var page = pages[i];
                pagesList.Add(new DocumentContent.DocumentPage(i+1, page.TextContent));
            }

            if (pages.Length == 0)
            {
                meta.Add(new DocumentContent.MetadataHeader(DocumentContent.MetadataWithoutPageInfo,"true"));
                var documentContent = doc.QuerySelector("body").TextContent;
                if (!String.IsNullOrEmpty(documentContent))
                {
                    pagesList.Add(new DocumentContent.DocumentPage(1, documentContent));
                }
            }

            return new DocumentContent(pagesList.ToArray(), meta.ToArray());
        }
    }
}