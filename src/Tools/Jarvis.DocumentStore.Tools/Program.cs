﻿using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.DocumentStore.Tools
{
    public class Program
    {
        static void Main(string[] args)
        {
            Menu();
            CommandLoop(c =>
            {
                switch (c)
                {
                    case "1":
                        CheckQueueScheduledJob();
                        break;
                    case "2":
                        CheckOrphanedBlobs.PerformCheck(DateTime.UtcNow);
                        break;
                    case "q":
                        return true;
                }

                Menu();
                return false;
            });
        }

        static void CommandLoop(Func<string, bool> action, String prompt = ">")
        {
            while (true)
            {
                Console.Write(prompt);
                var command = Console.ReadLine();
                if (command == null)
                    continue;

                command = command.ToLowerInvariant().Trim();

                if (action(command))
                    return;
            }
        }

        static void Message(string msg)
        {
            Console.WriteLine("");
            Console.WriteLine(msg);
            Console.WriteLine("(return to continue)");
            Console.ReadLine();
        }

        static void Banner(string title)
        {
            Console.WriteLine("-----------------------------");
            Console.WriteLine(title);
            Console.WriteLine("-----------------------------");
        }

        static void Menu()
        {
            Console.Clear();
            Banner("Menu");
            Console.WriteLine("1 - Check tika scheduled job");
            Console.WriteLine("2 - Find orphaned blobs");
            Console.WriteLine("");
            Console.WriteLine("Q - esci");
        }

        static void CheckQueueScheduledJob()
        {
            Console.WriteLine("Check all queued tika job that have no original in document descriptor");
            var urlQueue = new MongoUrl(ConfigurationManager.ConnectionStrings["queuesDb"].ConnectionString);
            var clientQueue = new MongoClient(urlQueue);

            var dbQueue = clientQueue.GetDatabase(urlQueue.DatabaseName);
            IMongoCollection<BsonDocument> _queueCollection = dbQueue.GetCollection<BsonDocument>("queue.tika");

            HashSet<String> blobIdQueued = new HashSet<string>();
            var allBlobIdQueued = _queueCollection
                .Find(Builders<BsonDocument>.Filter.Empty)
                .Project(Builders<BsonDocument>.Projection.Include("BlobId"))
                .ToList();

            foreach (var blobId in allBlobIdQueued)
            {
                blobIdQueued.Add(blobId["BlobId"].AsString);
            }

            var urlDs = new MongoUrl(ConfigurationManager.ConnectionStrings["mainDb"].ConnectionString);
            var clientDs = new MongoClient(urlDs);

            var dbDs = clientDs.GetDatabase(urlDs.DatabaseName);
            IMongoCollection<BsonDocument> _ddCollection = dbDs.GetCollection<BsonDocument>("rm.DocumentDescriptor");

            HashSet<String> blobIdDeDuplicated = new HashSet<string>();
            var allBlobIdDeDuplicated = _ddCollection
                .Find(Builders<BsonDocument>.Filter.Empty)
                .Project(Builders<BsonDocument>.Projection.Include("Formats.v.BlobId"))
                .ToList();

            foreach (var blobId in allBlobIdDeDuplicated)
            {
                var blobIdOriginal = blobId["Formats"].AsBsonArray
                    .Select(b => b["v"]["BlobId"].AsString)
                    .Where(s => s.StartsWith("original"))
                    .Distinct();
                foreach (var id in blobIdOriginal)
                {
                    blobIdDeDuplicated.Add(id);
                }
                if (blobIdOriginal.Count() > 1)
                {
                    Console.WriteLine("Descriptor with more than one original {0}", blobId["_id"]);
                }
            }

            //now check
            Int32 nonExisting = 0;
            foreach (var blobId in blobIdQueued)
            {
                if (!blobIdDeDuplicated.Contains(blobId))
                {
                    Console.WriteLine("Blob {0} queued but it does not belongs to any descriptor", blobId);
                    nonExisting++;
                }
            }

            Console.WriteLine("Found {0} blob id in tika.queue that does not belongs to any descriptor", nonExisting);
            Console.WriteLine("Press a key to continue");
            Console.ReadKey();
        }
    }
}
