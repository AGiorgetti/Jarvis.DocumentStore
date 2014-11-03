﻿using System;
using System.Diagnostics;
using System.IO;
using Castle.Core.Logging;
using Jarvis.DocumentStore.Core.Services;

namespace Jarvis.DocumentStore.Core.Processing.Conversions
{
    /// <summary>
    /// Office / OpenOffice => pdf with Headless Libreoffice
    /// TODO: switch to https://wiki.openoffice.org/wiki/AODL when complete pdf support is available
    /// </summary>
    public class LibreOfficeConversion : ILibreOfficeConversion
    {
        public ILogger Logger { get; set; }
        
        readonly ConfigService _config;

        public LibreOfficeConversion(ConfigService config)
        {
            _config = config;
        }

        public string Run(string sourceFile, string outType)
        {
            Logger.DebugFormat("Starting conversion of blobId {0} to {1}", sourceFile, outType);
            string pathToLibreOffice = _config.GetPathToLibreOffice();
            var outputFile = Path.ChangeExtension(sourceFile, outType);

            string arguments = string.Format("--headless -convert-to {2} -outdir \"{0}\"  \"{1}\" ",
                Path.GetDirectoryName(sourceFile),
                sourceFile,
                outType
            );

            var psi = new ProcessStartInfo(pathToLibreOffice, arguments)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Minimized
            };

            Logger.DebugFormat("Command: {0} {1}", pathToLibreOffice, arguments);

            using (var p = Process.Start(psi))
            {
                Logger.Debug("Process started");
                p.WaitForExit();
                Logger.Debug("Process ended");
            }

            if(!File.Exists(outputFile))
                throw new Exception("Conversion failed");

            return outputFile;
        }
    }
}
