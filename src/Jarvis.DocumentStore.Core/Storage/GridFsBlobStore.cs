using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Castle.Core.Logging;
using Jarvis.DocumentStore.Core.Domain.DocumentDescriptor;
using Jarvis.DocumentStore.Core.Model;
using Jarvis.Framework.Shared.IdentitySupport;
using MongoDB.Bson;
using MongoDB.Driver;
using Path = Jarvis.DocumentStore.Shared.Helpers.DsPath;
using File = Jarvis.DocumentStore.Shared.Helpers.DsFile;
using MongoDB.Driver.GridFS;

namespace Jarvis.DocumentStore.Core.Storage
{
    public class GridFsBlobStore : IBlobStore
    {
        public ILogger Logger { get; set; }
        readonly IMongoDatabase _database;
        private readonly ICounterService _counterService;
        readonly ConcurrentDictionary<DocumentFormat, GridFSBucket> _fs = 
            new ConcurrentDictionary<DocumentFormat, GridFSBucket>();

        public GridFsBlobStore(IMongoDatabase database, ICounterService counterService)
        {
            _database = database;
            _counterService = counterService;

            LoadFormatsFromDatabase();
        }

        private void LoadFormatsFromDatabase()
        {
            var cnames = _database.ListCollections()
                .ToEnumerable()
                .Select(doc => doc["name"].AsString)
                .ToArray();
            var names = cnames.Where(x => x.EndsWith(".files")).Select(x => x.Substring(0, x.LastIndexOf(".files"))).ToArray();

            foreach (var name in names)
            {
                GetGridFsByFormat(new DocumentFormat(name));
            }
        }

        public IBlobWriter CreateNew(DocumentFormat format, FileNameWithExtension fname)
        {
            var blobId = new BlobId(format, _counterService.GetNext(format));
            var gridFs = GetGridFsByFormat(format);
            Logger.DebugFormat("Creating file {0} on {1}", blobId, gridFs.Database.DatabaseNamespace.DatabaseName);
            var stream = gridFs.OpenUploadStream(fname, new GridFSUploadOptions()
            {
                Metadata = new BsonDocument
                {
                    { "contentType",  MimeTypes.GetMimeType(fname)},
                    { "uploadDate", DateTime.UtcNow },
                    { "_id" , (string) blobId}
                }
            });

            return new BlobWriter(blobId, stream, fname);
        }

        public Stream CreateNew(BlobId blobId, FileNameWithExtension fname)
        {
            var gridFs = GetGridFsByBlobId(blobId);

            Logger.DebugFormat("Creating file {0} on {1}", blobId, gridFs.Database.DatabaseNamespace.DatabaseName);
            Delete(blobId);
            return gridFs.OpenUploadStream(fname, new GridFSUploadOptions()
            {
                Metadata = new BsonDocument
                {
                    { "contentType",  MimeTypes.GetMimeType(fname)},
                    { "uploadDate", DateTime.UtcNow },
                    { "_id" , (string) blobId}
                }
            });
        }

        public IBlobDescriptor GetDescriptor(BlobId blobId)
        {
            var gridFs = GetGridFsByBlobId(blobId);

            Logger.DebugFormat("GetDescriptor for file {0} on {1}", blobId, gridFs.Database.DatabaseNamespace.DatabaseName);
            var s = gridFs.Find(Builders<GridFSFileInfo>.Filter.Eq("_id", (String)blobId)).FirstOrDefault();
            if (s == null)
            {
                var message = string.Format("Descriptor for file {0} not found!", blobId);
                Logger.DebugFormat(message);
                throw new Exception(message);
            }
            return new GridFsBlobDescriptor(blobId, s);
        }

        public void Delete(BlobId blobId)
        {
            var gridFs = GetGridFsByBlobId(blobId);
            Logger.DebugFormat("Deleting file {0} on {1}", blobId, gridFs.Database.DatabaseNamespace.DatabaseName);
            gridFs.Delete((string)blobId);
        }

        public string Download(BlobId blobId, string folder)
        {
            var gridFs = GetGridFsByBlobId(blobId);

            Logger.DebugFormat("Downloading file {0} on {1} to folder {2}", blobId, gridFs.Database.DatabaseNamespace.DatabaseName, folder);

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var s = gridFs.Find(Builders<GridFSFileInfo>.Filter.Eq("_id", (String)blobId)).FirstOrDefault();

            var localFileName = Path.Combine(folder, s.Metadata["name"]);
            gridFs.Download(localFileName, s);
            return localFileName;
        }

        public BlobId Upload(DocumentFormat format, string pathToFile)
        {
            using (var inStream = File.OpenRead(pathToFile))
            {
                return Upload(format, new FileNameWithExtension(Path.GetFileName(pathToFile)), inStream);
            }
        }

        public BlobId Upload(DocumentFormat format, FileNameWithExtension fileName, Stream sourceStrem)
        {
            var gridFs = GetGridFsByFormat(format);
            using (var writer = CreateNew(format, fileName))
            {
                Logger.DebugFormat("Uploading file {0} named {1} on {2}", writer.BlobId, fileName, gridFs.DatabaseName);
                sourceStrem.CopyTo(writer.WriteStream);
                return writer.BlobId;
            }
        }

        public BlobStoreInfo GetInfo()
        {
            var aggregation = new AggregateArgs()
            {
                Pipeline = new[] { BsonDocument.Parse("{$group:{_id:1, size:{$sum:'$length'}, count:{$sum:1}}}") }
            };

            var allInfos = _fs.Values
                .Select(x => x.Files.Aggregate(aggregation).FirstOrDefault())
                .Where(x => x != null)
                .Select(x => new { size = x["size"].ToInt64(), files = x["count"].ToInt64() })
                .ToArray();

            return new BlobStoreInfo(
                allInfos.Sum(x => x.size),
                allInfos.Sum(x => x.files)
            );
        }

        GridFSBucket GetGridFsByFormat(DocumentFormat format)
        {
            return _fs.GetOrAdd(format, CreateGridFsForFormat);
        }

        GridFSBucket CreateGridFsForFormat(DocumentFormat format)
        {
            var settings = new GridFSBucketOptions()
            {
                BucketName = format
            };

            return new GridFSBucket(_database, settings);
        }

        GridFSBucket GetGridFsByBlobId(BlobId id)
        {
            return GetGridFsByFormat(id.Format);
        }
    }
}
