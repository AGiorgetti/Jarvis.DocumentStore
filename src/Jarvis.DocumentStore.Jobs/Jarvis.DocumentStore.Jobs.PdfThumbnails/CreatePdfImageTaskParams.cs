﻿namespace Jarvis.DocumentStore.Jobs.PdfThumbnails
{
    public class CreatePdfImageTaskParams
    {
        public enum ImageFormat
        {
            Png,
            Jpg
        }

        public CreatePdfImageTaskParams()
        {
            FromPage = 1;
            Pages = 1;
            Dpi = 72;
            Format = ImageFormat.Png;
        }

        public int FromPage { get; set; }
        public int Pages { get; set; }
        public int Dpi { get; set; }
        public ImageFormat Format { get; set; }
    }
}