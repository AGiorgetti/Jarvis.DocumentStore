﻿using System.Collections.Generic;
using System.IO;
using Jarvis.DocumentStore.JobsHost.Helpers;
using Jarvis.DocumentStore.Shared.Jobs;
using System;
using System.Configuration;
using Jarvis.DocumentStore.Client.Model;
using Jarvis.DocumentStore.Shared.Helpers;
using Path = Jarvis.DocumentStore.Shared.Helpers.DsPath;
using File = Jarvis.DocumentStore.Shared.Helpers.DsFile;
using System.Threading.Tasks;

namespace Jarvis.DocumentStore.Jobs.VideoThumbnails
{
    public class VlcBasedVideoThumbnailOutOfProcessJob : AbstractOutOfProcessPollerJob
    {

        public VlcBasedVideoThumbnailOutOfProcessJob()
        {
            base.PipelineId = "video";
            base.QueueName = "videoThumb";
        }

        protected async override Task<ProcessResult> OnPolling(PollerJobParameters parameters, string workingFolder)
        {
            String format = parameters.All.GetOrDefault(JobKeys.ThumbnailFormat) ?? "png";
            Int32 secondsOffset = Int32.Parse(parameters.All.GetOrDefault("thumb_seconds_offset") ?? "10");

            Logger.DebugFormat("Conversion for jobId {0} in format {1} starting", parameters.JobId, format);

            String vlcExecutable = Helper.GetExecutableLocation();
            if (!File.Exists(vlcExecutable))
            {
                String error = String.Format("Unable to find VLC.exe executable in standard folders. You can specify VLC directory with 'vlc_location' job parameter or with 'vlc_location' app config configuration");
                Logger.ErrorFormat(error);
                Console.WriteLine("Unable to start converter, press a key to close.");
                Console.ReadKey();
                throw new ApplicationException(error);
            }

            var worker = new VlcCommandLineThumbnailCreator(vlcExecutable, format, Logger);

            String networkStream = base.GetBlobUriForJobBlob(parameters.TenantId, parameters.JobId);
            String thumbNail = worker.CreateThumbnail(networkStream, workingFolder, secondsOffset);

            if (String.IsNullOrEmpty(thumbNail))
            {
                Logger.WarnFormat("Conversion returned no thumbnail for file {0} - job {1}", parameters.FileName, parameters.JobId);
            }
            else
            {
                await AddFormatToDocumentFromFile(
                    parameters.TenantId,
                    parameters.JobId,
                    new DocumentFormat(DocumentFormats.RasterImage),
                    thumbNail,
                    new Dictionary<string, object>());

                Logger.DebugFormat("Conversion of {0} in format {1} done", parameters.JobId, format);
            }
            return ProcessResult.Ok;
        }

    }
}
