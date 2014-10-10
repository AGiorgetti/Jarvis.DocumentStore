﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Castle.Core.Logging;
using Jarvis.DocumentStore.Core.Model;
using Jarvis.DocumentStore.Core.Services;
using Jarvis.DocumentStore.Core.Storage;
using uno;
using uno.util;
using unoidl.com.sun.star.beans;
using unoidl.com.sun.star.frame;
using unoidl.com.sun.star.lang;


namespace Jarvis.DocumentStore.Core.Processing.Conversions
{
    /// <summary>
    /// http://tinyway.wordpress.com/2011/03/30/how-to-convert-office-documents-to-pdf-using-open-office-in-c/
    /// </summary>
    public class LibreOfficeUnoConversion
    {
        public ILogger Logger { get; set; }
        
        readonly IFileStore _fileStore;
        readonly ConfigService _config;

        public LibreOfficeUnoConversion(IFileStore fileStore, ConfigService config)
        {
            _fileStore = fileStore;
            _config = config;
        }

        public FileId Run(FileId fileId, string outType)
        {
            Logger.DebugFormat("Starting conversion of fileId {0} to {1}", fileId, outType);
            var workingFolder = _config.GetWorkingFolder(fileId);

            var sourceFile = _fileStore.Download(fileId, workingFolder);
            var outputFile = Path.ChangeExtension(sourceFile, outType);

            Logger.DebugFormat("Converting: {0} to {1}", sourceFile, outputFile);
            ConvertToPdf(sourceFile, outputFile);

            if(!File.Exists(outputFile))
                throw new Exception("Conversion failed");

            var newFileId = new FileId(fileId + "." + outType);
            _fileStore.Upload(newFileId, outputFile);

            try
            {
                Logger.DebugFormat("Removing working folder {0}", workingFolder);
                Directory.Delete(workingFolder, true);
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat(ex, "Unable to delete folder {0}", workingFolder);
            }

            return newFileId;
        }

        public void ConvertToPdf(string inputFile, string outputFile)
        {
            if (ConvertExtensionToFilterType(Path.GetExtension(inputFile)) == null)
                throw new InvalidProgramException("Unknown file type for OpenOffice. File = " + inputFile);

            StartOpenOffice();

            //Get a ComponentContext
            var xLocalContext =
                Bootstrap.bootstrap();
            //Get MultiServiceFactory
            var xRemoteFactory =
                (XMultiServiceFactory)
                xLocalContext.getServiceManager();
            //Get a CompontLoader
            var aLoader =
                (XComponentLoader)xRemoteFactory.createInstance("com.sun.star.frame.Desktop");
            //Load the sourcefile

            XComponent xComponent = null;
            try
            {
                xComponent = InitDocument(aLoader,
                                          PathConverter(inputFile), "_blank");
                //Wait for loading
                while (xComponent == null)
                {
                    Thread.Sleep(1000);
                }

                // save/export the document
                SaveDocument(xComponent, inputFile, PathConverter(outputFile));
            }
            finally
            {
                if (xComponent != null) xComponent.dispose();
            }
        }
        
        private void StartOpenOffice()
        {
            var ps = Process.GetProcessesByName("soffice.exe");
            if (ps.Length != 0)
                throw new InvalidProgramException("OpenOffice not found.  Is OpenOffice installed?");
            if (ps.Length > 0)
                return;
            var p = new Process
            {
                StartInfo =
                {
                    Arguments = "-headless -nofirststartwizard",
                    FileName = "soffice.exe",
                    CreateNoWindow = true
                }
            };
            var result = p.Start();

            if (result == false)
                throw new InvalidProgramException("OpenOffice failed to start.");
        }

        private XComponent InitDocument(XComponentLoader aLoader, string file, string target)
        {
            var openProps = new PropertyValue[1];
            openProps[0] = new PropertyValue { Name = "Hidden", Value = new Any(true) };

            var xComponent = aLoader.loadComponentFromURL(
                file, target, 0,
                openProps);

            return xComponent;
        }

        private void SaveDocument(XComponent xComponent, string sourceFile, string destinationFile)
        {
            var propertyValues = new PropertyValue[2];
            // Setting the flag for overwriting
            propertyValues[1] = new PropertyValue { Name = "Overwrite", Value = new Any(true) };
            //// Setting the filter name
            propertyValues[0] = new PropertyValue
            {
                Name = "FilterName",
                Value = new Any(ConvertExtensionToFilterType(Path.GetExtension(sourceFile)))
            };
            ((XStorable)xComponent).storeToURL(destinationFile, propertyValues);
        }

        private string PathConverter(string file)
        {
            if (string.IsNullOrEmpty(file))
                throw new NullReferenceException("Null or empty path passed to OpenOffice");

            return String.Format("file:///{0}", file.Replace(@"\", "/"));
        }

        public string ConvertExtensionToFilterType(string extension)
        {
            switch (extension)
            {
                case ".doc":
                case ".docx":
                case ".txt":
                case ".rtf":
                case ".html":
                case ".htm":
                case ".xml":
                case ".odt":
                case ".wps":
                case ".wpd":
                    return "writer_pdf_Export";
                case ".xls":
                case ".xlsb":
                case ".xlsx":
                case ".ods":
                    return "calc_pdf_Export";
                case ".ppt":
                case ".ppsx":
                case ".pptx":
                case ".odp":
                    return "impress_pdf_Export";

                default:
                    return null;
            }
        }
    }
}
