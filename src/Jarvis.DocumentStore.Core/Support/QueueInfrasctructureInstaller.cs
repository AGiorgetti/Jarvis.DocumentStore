﻿using System;
using System.Collections.Generic;
using Castle.Core.Logging;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using Jarvis.DocumentStore.Core.Jobs;
using Jarvis.DocumentStore.Core.Jobs.OutOfProcessPollingJobs;
using Jarvis.DocumentStore.Shared.Jobs;
using MongoDB.Driver;
using Quartz;
using Jarvis.DocumentStore.Core.Jobs.QueueManager;


namespace Jarvis.DocumentStore.Core.Support
{
    public class QueueInfrasctructureInstaller : IWindsorInstaller
    {
        private String _queueStoreConnectionString;
        private IEnumerable<QueueInfo> _queuesInfo;
        private ILogger _logger;

        public QueueInfrasctructureInstaller(string queueStoreConnectionString, IEnumerable<QueueInfo> queuesInfo)
        {
            _queueStoreConnectionString = queueStoreConnectionString;
            _queuesInfo = queuesInfo;
        }

        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
            _logger = container.Resolve<ILoggerFactory>().Create(typeof(QueueInfrasctructureInstaller));
            var queueDb = GetQueueDb();
            container.Register(
                Component
                    .For<QueueManager, IQueueManager>()
                    .ImplementedBy<QueueManager>()
                    .DependsOn(Dependency.OnValue<IMongoDatabase>(queueDb))
                    .LifeStyle.Singleton,
                Classes.FromAssemblyInThisApplication()
                    .BasedOn<IPollerJob>()
                    .WithServiceFirstInterface(),
                Component.For<QueuedJobStatus>(),
                Component.For<QueuedJobQuartzMonitor>(),
                    //Component
                //    .For<IPollerJobManager>()
                //    .ImplementedBy<InProcessPollerJobManager>(),
               Component
                    .For<IPollerJobManager>()
                    .ImplementedBy<OutOfProcessBaseJobManager>(),
                Component
                    .For<PollerManager>()
                    .ImplementedBy<PollerManager>()
            );

            //now register all handlers
            foreach (var queueInfo in _queuesInfo)
            {
                container.Register(Component.For<QueueHandler>()
                    .ImplementedBy<QueueHandler>()
                    .DependsOn(Dependency.OnValue<QueueInfo>(queueInfo))
                    .DependsOn(Dependency.OnValue<IMongoDatabase>(queueDb))
                    .Named("QueueHandler-" + queueInfo.Name));
            }

                        try
            {
                SetupTimeoutJob(container.Resolve<IScheduler>());
            }
            catch (Exception ex)
            {
                container.Resolve<ILogger>().ErrorFormat(ex, "SetupCleanupJob");
            }
        }

        void SetupTimeoutJob(IScheduler scheduler)
        {
            JobKey jobKey = JobKey.Create("queuedMonitorForTimeout", "sys.cleanup");
            scheduler.DeleteJob(jobKey);

            var job = JobBuilder
                .Create<QueuedJobQuartzMonitor>()
                .WithIdentity(jobKey)
                .Build();

            var trigger = TriggerBuilder.Create()
#if DEBUG
.StartAt(DateTimeOffset.Now.AddSeconds(5))
                .WithSimpleSchedule(b => b.RepeatForever().WithIntervalInMinutes(2))
#else
                .StartAt(DateTimeOffset.Now.AddMinutes(2))
                .WithSimpleSchedule(b=>b.RepeatForever().WithIntervalInMinutes(2))
#endif
.WithPriority(1)
                .Build();

            var nextExcution = scheduler.ScheduleJob(job, trigger);
            _logger.InfoFormat("Scheduled QueuedJobQuartzMonitor: first execution at {0}.", nextExcution);
        }

        IMongoDatabase GetQueueDb()
        {
            var url = new MongoUrl(_queueStoreConnectionString);
            var client = new MongoClient(url);
            return client.GetDatabase(url.DatabaseName);
        }

        IDictionary<string,string> CreateDefaultConfiguration()
        {
            var config = new Dictionary<string, string>();
            config["quartz.scheduler.instanceId"] = Environment.MachineName + "-" + DateTime.Now.ToShortTimeString();
            config["quartz.threadPool.type"] = "Quartz.Simpl.SimpleThreadPool, Quartz";
            config["quartz.threadPool.threadCount"] = (Environment.ProcessorCount *2).ToString();
            config["quartz.threadPool.threadPriority"] = "Normal";
            config["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz";
            return config;
        }
    }
}