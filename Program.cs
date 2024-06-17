// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Quartz.Impl;
using Quartz;
using Quote_To_Deal.Jobs;
using Quote_To_Deal.PaperLess;
using Quote_To_Deal.Common;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using Quote_To_Deal.Models;
using Newtonsoft.Json;
using ServiceStack.Text;

namespace Quote_To_Deal
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }
        public static void StartWorkflow1Job(IConfiguration config)
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            var scheduler = schedulerFactory.GetScheduler().Result;
            var configKey = $"Quartz:Workflow1";
            var cronSchedule = config[configKey];
            var quotePath = Path.Combine(config[$"BasePath"], config[$"Paperless:QuotePath"]);

            ITrigger trigger = TriggerBuilder.Create()
            .WithCronSchedule(cronSchedule)
            .WithPriority(1)
            .Build();

            IJobDetail job = JobBuilder.Create<Workflow1Job>()
                        .WithIdentity("Workflow 1")
                        .Build();
            job.JobDataMap.Put("ConnectionString", config.GetConnectionString("DBContextConnectionString"));
            job.JobDataMap.Put("PrivateAppKey", config[$"Hubspot:PrivateAppKey"]);

            long tempQuote = 1;
            long.TryParse(config[$"PaperLess:LastQuoteNumber"], out tempQuote);
            var lastQuote = SettingsHelpers.ReadCacheSetting<long?>(quotePath, "LastQuote") ?? tempQuote;
            job.JobDataMap.Put("LastQuoteNumber", lastQuote);

            job.JobDataMap.Put("AmeritexCompanyId", config[$"Hubspot:AmeritexCompanyId"]);
            job.JobDataMap.Put("QuotePath", config[$"Paperless:QuotePath"]);
            PaperLessAPIControl.API_KEY = config[$"Paperless:ApiKey"];

            job.JobDataMap.Put("UserEmail", config[$"Smtp:UserEmail"]);
            job.JobDataMap.Put("EmailPassword", config[$"Smtp:Password"]);
            job.JobDataMap.Put("SmtpPort", config[$"Smtp:Port"]);
            job.JobDataMap.Put("SmtpHost", config[$"Smtp:Host"]);
            job.JobDataMap.Put("SmtpEnableSsl", config[$"Smtp:EnableSsl"]);
            job.JobDataMap.Put("EmailSetupBaseUrl", config[$"EmailSetup:BaseUrl"]);
            job.JobDataMap.Put("SetupEndpoint", config[$"EmailSetup:SetupEndpoint"]);
            job.JobDataMap.Put("UnsubscribeEndpoint", config[$"EmailSetup:UnsubscribeEndpoint"]);
            job.JobDataMap.Put("IsTestingOnceADay", config[$"IsTestingOnceADay"]);

            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
        }
        public static void StartWorkflow1OnceJob(IConfiguration config)
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            var scheduler = schedulerFactory.GetScheduler().Result;
            var configKey = $"Quartz:Workflow1";
            //var configKey = $"Quartz:Workflow1OnceADay";
            var cronSchedule = config[configKey];
            var quotePath = Path.Combine(config[$"BasePath"], config[$"Paperless:QuotePath"]);

            ITrigger trigger = TriggerBuilder.Create()
            .WithCronSchedule(cronSchedule)
            .WithPriority(1)
            .Build();

            IJobDetail job = JobBuilder.Create<Workflow1OnceJob>()
                        .WithIdentity("Workflow 1 Once")
                        .Build();
            job.JobDataMap.Put("ConnectionString", config.GetConnectionString("DBContextConnectionString"));
            job.JobDataMap.Put("PrivateAppKey", config[$"Hubspot:PrivateAppKey"]);

            long tempQuote = 1;
            long.TryParse(config[$"PaperLess:LastQuoteNumber"], out tempQuote);
            var lastQuote = SettingsHelpers.ReadCacheSetting<long?>(quotePath, "LastQuote") ?? tempQuote;
            job.JobDataMap.Put("LastQuoteNumber", lastQuote);

            job.JobDataMap.Put("AmeritexCompanyId", config[$"Hubspot:AmeritexCompanyId"]);
            job.JobDataMap.Put("QuotePath", config[$"Paperless:QuotePath"]);
            PaperLessAPIControl.API_KEY = config[$"Paperless:ApiKey"];

            job.JobDataMap.Put("UserEmail", config[$"Smtp:UserEmail"]);
            job.JobDataMap.Put("EmailPassword", config[$"Smtp:Password"]);
            job.JobDataMap.Put("SmtpPort", config[$"Smtp:Port"]);
            job.JobDataMap.Put("SmtpHost", config[$"Smtp:Host"]);
            job.JobDataMap.Put("SmtpEnableSsl", config[$"Smtp:EnableSsl"]);
            job.JobDataMap.Put("EmailSetupBaseUrl", config[$"EmailSetup:BaseUrl"]);
            job.JobDataMap.Put("SetupEndpoint", config[$"EmailSetup:SetupEndpoint"]);
            job.JobDataMap.Put("UnsubscribeEndpoint", config[$"EmailSetup:UnsubscribeEndpoint"]);
            job.JobDataMap.Put("IsTestingOnceADay", config[$"IsTestingOnceADay"]);
            
            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
        }
        public static void StartTestWorkflow(IConfiguration config)
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            var scheduler = schedulerFactory.GetScheduler().Result;
            var configKey = $"Quartz:Workflow1";
            var cronSchedule = config[configKey];
            var quotePath = Path.Combine(config[$"BasePath"], config[$"Paperless:QuotePath"]);

            ITrigger trigger = TriggerBuilder.Create()
            .WithCronSchedule(cronSchedule)
            .WithPriority(1)
            .Build();

            IJobDetail job = JobBuilder.Create<testWorkflow>()
                        .WithIdentity("Test workflow")
                        .Build();
            job.JobDataMap.Put("ConnectionString", config.GetConnectionString("DBContextConnectionString"));
            job.JobDataMap.Put("PrivateAppKey", config[$"Hubspot:PrivateAppKey"]);

            long tempQuote = 1;
            long.TryParse(config[$"PaperLess:LastQuoteNumber"], out tempQuote);
            var lastQuote = SettingsHelpers.ReadCacheSetting<long?>(quotePath, "LastQuote") ?? tempQuote;
            job.JobDataMap.Put("LastQuoteNumber", lastQuote);

            job.JobDataMap.Put("AmeritexCompanyId", config[$"Hubspot:AmeritexCompanyId"]);
            job.JobDataMap.Put("QuotePath", config[$"Paperless:QuotePath"]);
            PaperLessAPIControl.API_KEY = config[$"Paperless:ApiKey"];

            job.JobDataMap.Put("UserEmail", config[$"Smtp:UserEmail"]);
            job.JobDataMap.Put("EmailPassword", config[$"Smtp:Password"]);
            job.JobDataMap.Put("SmtpPort", config[$"Smtp:Port"]);
            job.JobDataMap.Put("SmtpHost", config[$"Smtp:Host"]);
            job.JobDataMap.Put("SmtpEnableSsl", config[$"Smtp:EnableSsl"]);
            job.JobDataMap.Put("EmailSetupBaseUrl", config[$"EmailSetup:BaseUrl"]);
            job.JobDataMap.Put("SetupEndpoint", config[$"EmailSetup:SetupEndpoint"]);
            job.JobDataMap.Put("UnsubscribeEndpoint", config[$"EmailSetup:UnsubscribeEndpoint"]);
            job.JobDataMap.Put("IsTestingOnceADay", config[$"IsTestingOnceADay"]);

            var salesPersonEmailCreds = config.GetSection("SalespersonEmailCreds").Get<List<SalespersonEmailCreds>>();

            job.JobDataMap.Put("SalespersonEmailCreds", JsonConvert.SerializeObject(salesPersonEmailCreds));

            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
        }
        public static void StartWorkflow2Job(IConfiguration config)
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            var scheduler = schedulerFactory.GetScheduler().Result;
            var configKey = $"Quartz:Workflow2";
            var cronSchedule = config[configKey];
            var quotePath = Path.Combine(config[$"BasePath"], config[$"Paperless:QuotePath"]);

            ITrigger trigger = TriggerBuilder.Create()
            .WithCronSchedule(cronSchedule)
            .WithPriority(1)
            .Build();

            IJobDetail job = JobBuilder.Create<Workflow2Job>()
                        .WithIdentity("Workflow 2")
                        .Build();
            job.JobDataMap.Put("ConnectionString", config.GetConnectionString("DBContextConnectionString"));
            job.JobDataMap.Put("PrivateAppKey", config[$"Hubspot:PrivateAppKey"]);

            long tempQuote = 1;
            long.TryParse(config[$"PaperLess:LastQuoteNumber"], out tempQuote);
            var lastQuote = SettingsHelpers.ReadCacheSetting<long?>(quotePath, "LastQuote") ?? tempQuote;
            job.JobDataMap.Put("LastQuoteNumber", lastQuote);

            job.JobDataMap.Put("AmeritexCompanyId", config[$"Hubspot:AmeritexCompanyId"]);
            job.JobDataMap.Put("QuotePath", config[$"Paperless:QuotePath"]);

            job.JobDataMap.Put("UserEmail", config[$"Smtp:UserEmail"]);
            job.JobDataMap.Put("EmailPassword", config[$"Smtp:Password"]);
            job.JobDataMap.Put("SmtpPort", config[$"Smtp:Port"]);
            job.JobDataMap.Put("SmtpHost", config[$"Smtp:Host"]);
            job.JobDataMap.Put("SmtpEnableSsl", config[$"Smtp:EnableSsl"]);

            PaperLessAPIControl.API_KEY = config[$"Paperless:ApiKey"];

            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
        }
        public static void StartWorkflowCancellationJob(IConfiguration config)
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            var scheduler = schedulerFactory.GetScheduler().Result;
            var configKey = $"Quartz:WorkflowCancellation";
            var cronSchedule = config[configKey];
            var quotePath = Path.Combine(config[$"BasePath"], config[$"Paperless:QuotePath"]);

            ITrigger trigger = TriggerBuilder.Create()
            .WithCronSchedule(cronSchedule)
            .WithPriority(1)
            .Build();

            IJobDetail job = JobBuilder.Create<WorkflowCancellationJob>()
                        .WithIdentity("Workflow Cancellation")
                        .Build();
            job.JobDataMap.Put("ConnectionString", config.GetConnectionString("DBContextConnectionString"));
            job.JobDataMap.Put("PrivateAppKey", config[$"Hubspot:PrivateAppKey"]);

            long tempQuote = 1;
            long.TryParse(config[$"PaperLess:LastQuoteNumber"], out tempQuote);
            var lastQuote = SettingsHelpers.ReadCacheSetting<long?>(quotePath, "LastQuote") ?? tempQuote;
            job.JobDataMap.Put("LastQuoteNumber", lastQuote);

            job.JobDataMap.Put("AmeritexCompanyId", config[$"Hubspot:AmeritexCompanyId"]);
            job.JobDataMap.Put("QuotePath", config[$"Paperless:QuotePath"]);

            job.JobDataMap.Put("UserEmail", config[$"Smtp:UserEmail"]);
            job.JobDataMap.Put("EmailPassword", config[$"Smtp:Password"]);
            job.JobDataMap.Put("SmtpPort", config[$"Smtp:Port"]);
            job.JobDataMap.Put("SmtpHost", config[$"Smtp:Host"]);
            job.JobDataMap.Put("SmtpEnableSsl", config[$"Smtp:EnableSsl"]);

            PaperLessAPIControl.API_KEY = config[$"Paperless:ApiKey"];

            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
        }
        public static void StartWorkflow3Job(IConfiguration config)
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            var scheduler = schedulerFactory.GetScheduler().Result;
            var configKey = $"Quartz:Workflow3";
            var cronSchedule = config[configKey];
            var quotePath = Path.Combine(config[$"BasePath"], config[$"Paperless:QuotePath"]);

            ITrigger trigger = TriggerBuilder.Create()
            .WithCronSchedule(cronSchedule)
            .WithPriority(1)
            .Build();

            IJobDetail job = JobBuilder.Create<Workflow3Job>()
                        .WithIdentity("Workflow 3")
                        .Build();
            job.JobDataMap.Put("ConnectionString", config.GetConnectionString("DBContextConnectionString"));
            job.JobDataMap.Put("PrivateAppKey", config[$"Hubspot:PrivateAppKey"]);

            long tempQuote = 1;
            long.TryParse(config[$"PaperLess:LastQuoteNumber"], out tempQuote);
            var lastQuote = SettingsHelpers.ReadCacheSetting<long?>(quotePath, "LastQuote") ?? tempQuote;
            job.JobDataMap.Put("LastQuoteNumber", lastQuote);

            job.JobDataMap.Put("AmeritexCompanyId", config[$"Hubspot:AmeritexCompanyId"]);
            job.JobDataMap.Put("QuotePath", config[$"Paperless:QuotePath"]);

            job.JobDataMap.Put("UserEmail", config[$"Smtp:UserEmail"]);
            job.JobDataMap.Put("EmailPassword", config[$"Smtp:Password"]);
            job.JobDataMap.Put("SmtpPort", config[$"Smtp:Port"]);
            job.JobDataMap.Put("SmtpHost", config[$"Smtp:Host"]);
            job.JobDataMap.Put("SmtpEnableSsl", config[$"Smtp:EnableSsl"]);

            PaperLessAPIControl.API_KEY = config[$"Paperless:ApiKey"];

            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
        }
        public static void StartWorkflow4Job(IConfiguration config)
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            var scheduler = schedulerFactory.GetScheduler().Result;
            var configKey = $"Quartz:Workflow4";
            var cronSchedule = config[configKey];
            var quotePath = Path.Combine(config[$"BasePath"], config[$"Paperless:QuotePath"]);

            ITrigger trigger = TriggerBuilder.Create()
            .WithCronSchedule(cronSchedule)
            .WithPriority(1)
            .Build();

            IJobDetail job = JobBuilder.Create<Workflow4Job>()
                        .WithIdentity("Workflow 4")
                        .Build();
            job.JobDataMap.Put("ConnectionString", config.GetConnectionString("DBContextConnectionString"));
            job.JobDataMap.Put("PrivateAppKey", config[$"Hubspot:PrivateAppKey"]);

            long tempQuote = 1;
            long.TryParse(config[$"PaperLess:LastQuoteNumber"], out tempQuote);
            var lastQuote = SettingsHelpers.ReadCacheSetting<long?>(quotePath, "LastQuote") ?? tempQuote;
            job.JobDataMap.Put("LastQuoteNumber", lastQuote);

            job.JobDataMap.Put("AmeritexCompanyId", config[$"Hubspot:AmeritexCompanyId"]);
            job.JobDataMap.Put("QuotePath", config[$"Paperless:QuotePath"]);

            job.JobDataMap.Put("UserEmail", config[$"Smtp:UserEmail"]);
            job.JobDataMap.Put("EmailPassword", config[$"Smtp:Password"]);
            job.JobDataMap.Put("SmtpPort", config[$"Smtp:Port"]);
            job.JobDataMap.Put("SmtpHost", config[$"Smtp:Host"]);
            job.JobDataMap.Put("SmtpEnableSsl", config[$"Smtp:EnableSsl"]);

            PaperLessAPIControl.API_KEY = config[$"Paperless:ApiKey"];

            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
        }
        public static void StartWorkflow4ByOrderJob(IConfiguration config)
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            var scheduler = schedulerFactory.GetScheduler().Result;
            var configKey = $"Quartz:Workflow4";
            var cronSchedule = config[configKey];
            var quotePath = Path.Combine(config[$"BasePath"], config[$"Paperless:QuotePath"]);

            ITrigger trigger = TriggerBuilder.Create()
            .WithCronSchedule(cronSchedule)
            .WithPriority(1)
            .Build();

            IJobDetail job = JobBuilder.Create<Workflow4ByOrderJob>()
                        .WithIdentity("Workflow 4 By Order")
                        .Build();
            job.JobDataMap.Put("ConnectionString", config.GetConnectionString("DBContextConnectionString"));
            job.JobDataMap.Put("PrivateAppKey", config[$"Hubspot:PrivateAppKey"]);

            long tempOrder = 1;
            long.TryParse(config[$"PaperLess:LastOrderNumber"], out tempOrder);
            var lastOrder = SettingsHelpers.ReadCacheSetting<long?>(quotePath, "LastOrder") ?? tempOrder;
            job.JobDataMap.Put("LastOrderNumber", lastOrder);

            job.JobDataMap.Put("AmeritexCompanyId", config[$"Hubspot:AmeritexCompanyId"]);
            job.JobDataMap.Put("QuotePath", config[$"Paperless:QuotePath"]);

            job.JobDataMap.Put("UserEmail", config[$"Smtp:UserEmail"]);
            job.JobDataMap.Put("EmailPassword", config[$"Smtp:Password"]);
            job.JobDataMap.Put("SmtpPort", config[$"Smtp:Port"]);
            job.JobDataMap.Put("SmtpHost", config[$"Smtp:Host"]);
            job.JobDataMap.Put("SmtpEnableSsl", config[$"Smtp:EnableSsl"]);

            PaperLessAPIControl.API_KEY = config[$"Paperless:ApiKey"];

            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
        }
        public static void StartWorkflow5Job(IConfiguration config)
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            var scheduler = schedulerFactory.GetScheduler().Result;
            var configKey = $"Quartz:Workflow5";
            var cronSchedule = config[configKey];
            var quotePath = Path.Combine(config[$"BasePath"], config[$"Paperless:QuotePath"]);

            ITrigger trigger = TriggerBuilder.Create()
            .WithCronSchedule(cronSchedule)
            .WithPriority(1)
            .Build();

            IJobDetail job = JobBuilder.Create<Workflow5Job>()
                        .WithIdentity("Workflow 5")
                        .Build();
            job.JobDataMap.Put("ConnectionString", config.GetConnectionString("DBContextConnectionString"));
            job.JobDataMap.Put("PrivateAppKey", config[$"Hubspot:PrivateAppKey"]);
            job.JobDataMap.Put("AmeritexCompanyId", config[$"Hubspot:AmeritexCompanyId"]);
            job.JobDataMap.Put("AmeritexCompanyId", config[$"Hubspot:AmeritexCompanyId"]);

            long tempQuote = 1;
            long.TryParse(config[$"PaperLess:LastQuoteNumber"], out tempQuote);
            var lastQuote = SettingsHelpers.ReadCacheSetting<long?>(quotePath, "LastQuote") ?? tempQuote;
            job.JobDataMap.Put("LastQuoteNumber", lastQuote);

            job.JobDataMap.Put("QuotePath", config[$"Paperless:QuotePath"]);

            job.JobDataMap.Put("UserEmail", config[$"Smtp:UserEmail"]);
            job.JobDataMap.Put("EmailPassword", config[$"Smtp:Password"]);
            job.JobDataMap.Put("SmtpPort", config[$"Smtp:Port"]);
            job.JobDataMap.Put("SmtpHost", config[$"Smtp:Host"]);
            job.JobDataMap.Put("SmtpEnableSsl", config[$"Smtp:EnableSsl"]);

            PaperLessAPIControl.API_KEY = config[$"Paperless:ApiKey"];

            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();
        }
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
               {
                   var configuration = hostContext.Configuration;

                   Common.JobPathHelper.BasePath = configuration["BasePath"];
                   Common.JobPathHelper.BaseUrl = configuration["BaseUrl"];

                   var encrytionKey = configuration["EncrytionKey"] ?? "ameritex";
                   var isTestingEmail = bool.Parse(configuration["IsTestingEmail"]);
                   var surveyLink = configuration["SurveyLink"];
                   var isDevOnly = bool.Parse(configuration["IsDevOnly"]);
                   var encrytionConfig = new EncryptDecrypt(encrytionKey);

                   var salesPersonEmailCreds = configuration.GetSection("SalespersonEmailCreds").Get<List<SalespersonEmailCreds>>();
                   var UtilConfig = new Utils(isTestingEmail, isDevOnly, surveyLink, salesPersonEmailCreds);

                   services.AddSingleton(encrytionConfig);

                   //StartWorkflow1Job(configuration);
                   StartWorkflow1OnceJob(configuration);
                   //StartWorkflow2Job(configuration);
                   //StartWorkflow3Job(configuration);
                   //StartWorkflow4Job(configuration);
                   //StartWorkflow4ByOrderJob(configuration);
                   //StartWorkflow5Job(configuration);
                   //StartWorkflowCancellationJob(configuration);
                   //StartTestWorkflow(configuration);
               }
           );
        }
    }
}
