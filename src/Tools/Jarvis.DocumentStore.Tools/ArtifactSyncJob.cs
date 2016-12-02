using Jarvis.ConfigurationService.Client;
using Jarvis.DocumentStore.Tools.Helpers;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.DocumentStore.Tools
{
    class ArtifactSyncJob
    {
        internal static void StartSync()
        {
            var tenants = ConfigurationServiceClient.Instance.GetArraySetting("tenants");
            List<SyncUnit> syncUnits = new List<SyncUnit>();
            foreach (String tenant in tenants)
            {
                var sync = ConsoleHelper.AskYesNoQuestion("Do you want to sync tenant {0}", tenant);
                if (sync)
                {
                    var originalConnectionString = ConfigurationServiceClient.Instance.GetSetting("connectionStrings." + tenant + ".originals");
                    var artifactsConnectionString = ConfigurationServiceClient.Instance.GetSetting("connectionStrings." + tenant + ".artifacts");

                    Console.Write("Give me the name of the destination instance:");
                    var destinationInstance = Console.ReadLine();

                    var syncUnitOriginal = new SyncUnit(tenant, originalConnectionString, destinationOriginalConnectionString);
                    syncUnits.Add(syncUnitOriginal);
                    var syncUnitArtifacts = new SyncUnit(tenant, artifactsConnectionString, destinationArtifactsConnectionString);
                    //syncUnits.Add(syncUnitArtifacts);
                }
            }
            List<Task> syncTasks = new List<Task>();
            foreach (var syncUnit in syncUnits)
            {
                syncTasks.Add(syncUnit.Start());
            }

            Task.WaitAll(syncTasks.ToArray());
        }

        private class SyncUnit
        {
            public SyncUnit(string tenant, string sourceOriginalConnectionString, string destinationOriginalConnection)
            {
                this.SourceOriginalConnectionString = sourceOriginalConnectionString;
                this.DestinationOriginalConnectionString = destinationOriginalConnection;
                Tenant = tenant;
            }

            public String SourceOriginalConnectionString { get; set; }

            public String DestinationOriginalConnectionString { get; set; }

            public String Tenant { get; set; }

            internal Task Start()
            {
                return Task.Factory.StartNew(Sync);
            }

            IGridFSBucket sourceBucket;
            MongoUrl sourceMongoUrl;

            IGridFSBucket destinationBucket;
            MongoUrl destinationMongoUrl;
            private void Initialize()
            {
                IMongoDatabase sourceDatabase;
                sourceMongoUrl = new MongoUrl(SourceOriginalConnectionString);
                sourceDatabase = new MongoClient(sourceMongoUrl).GetDatabase(sourceMongoUrl.DatabaseName);

                sourceBucket = new GridFSBucket(sourceDatabase, new GridFSBucketOptions
                {
                    BucketName = "original",
                    ChunkSizeBytes = 1048576, // 1MB
                });

                IMongoDatabase destinationDatabase;
                destinationMongoUrl = new MongoUrl(DestinationOriginalConnectionString);
                destinationDatabase = new MongoClient(destinationMongoUrl).GetDatabase(destinationMongoUrl.DatabaseName);

                destinationBucket = new GridFSBucket(destinationDatabase, new GridFSBucketOptions
                {
                    BucketName = "original",
                    ChunkSizeBytes = 1048576, // 1MB
                });
            }

            private void Sync()
            {
                Initialize();
                DateTime lastTimestamp = DateTime.MinValue;

                HashSet<String> blobIdCopied = new HashSet<String>();
                while (true)
                {
                    Console.WriteLine("Syncincg {0} - {1}",Tenant, sourceMongoUrl.DatabaseName);

                    var filter = Builders<GridFSFileInfo>.Filter.And(
                        Builders<GridFSFileInfo>.Filter.Gte(x => x.UploadDateTime, lastTimestamp)
                    );

                    var sort = Builders<GridFSFileInfo>.Sort.Descending(x => x.UploadDateTime);
                    var options = new GridFSFindOptions
                    {
                        Limit = 500,
                        Sort = sort
                    };

                    using (var cursor = sourceBucket.Find(filter, options))
                    {
                        foreach (var element in cursor.ToEnumerable())
                        {
                            using (var destinationStream = destinationBucket.OpenUploadStream(element.Filename))
                            {
                                sourceBucket.DownloadToStream(element.Id, destinationStream);
                            }
                        }
                        // fileInfo either has the matching file information or is null
                    }

                    Thread.Sleep(2000);
                }
            }
        }
    }
}
