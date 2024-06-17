using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Quote_To_Deal.Data;
using Quote_To_Deal.Common;
using Quote_To_Deal.Models;

namespace Quote_To_Deal.Jobs
{
    [DisallowConcurrentExecution]
    public class Workflow1OnceJob : IJob
    {
        private QtdContext _dbContext;
        private EmailSetting _emailSetting;

        public async Task Execute(IJobExecutionContext context)
        {
            JobDataMap dataMap = context.JobDetail.JobDataMap;

            string connectionString = dataMap.GetString("ConnectionString") ?? "";
            string hsApiKey = dataMap.GetString("PrivateAppKey") ?? "";
            long lastQuoteNo = dataMap.GetLong("LastQuoteNumber");
            string quotePath = dataMap.GetString("QuotePath") ?? "";

            try
            {
                SetDBContext(connectionString);
                SetEmailSetting(dataMap);

                var groupedQuotes = GetQuoteEmails();

                foreach (var groupedQuote in groupedQuotes)
                {
                    var customerEmail = groupedQuote.Key;
                    if (!string.IsNullOrEmpty(customerEmail))
                    {
                        try
                        {
                            if (!IsEmailUnsubscribed(customerEmail))
                            {
                               var salesPersonQuotes =  groupedQuote.Value
                                                                .GroupBy(x => x.salesperson.first_name)
                                                                .ToDictionary(g => g.Key, g => g.ToList());

                                foreach(var salesPersonQuote in salesPersonQuotes)
                                {
                                    Utils.SendEmail(_emailSetting, salesPersonQuote.Value, "Workflow1Once", new List<string> { customerEmail }, 
                                        salesPersonName: salesPersonQuote.Key);
                                    UpdateOnceADayEmailSentStatus(customerEmail);
                                }
                                WriteLog(PrepareLogMessage($"Once a day email sent successfully. email:{customerEmail}, quoteNos:{string.Join(",", groupedQuote.Value.Select(x => x.number).ToList())}"));
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteLog(PrepareLogMessage($"EMAIL ERROR: {ex.Message}{Environment.NewLine}{ex.InnerException?.Message}{Environment.NewLine}{ex.StackTrace}"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog(PrepareLogMessage($"ERROR: {ex.Message}{Environment.NewLine}{ex.InnerException?.Message}{Environment.NewLine}{ex.StackTrace}"));
            }
        }

        private void UpdateOnceADayEmailSentStatus(string email)
        {
            var onceADayEmail = _dbContext.OnceADayEmails.Where(x => x.Email == email && (x.IsSend ?? false) == false).ToList();

            if (onceADayEmail.Any())
            {
                onceADayEmail.ForEach(x =>
                {
                    x.IsSend = true;
                    x.SentDate = DateTime.Now;
                });

                _dbContext.UpdateRange(onceADayEmail);
                _dbContext.SaveChanges();
            }
        }

        private Dictionary<string, List<PaperLess.Quote>> GetQuoteEmails()
        {
            var tempQuotes = _dbContext.OnceADayEmails.Where(x => (x.IsSend ?? false) == false).Select(x => new { x.QuoteNumber, x.RevisionNumber }).ToList();

            var quotes = new List<PaperLess.Quote>();
            foreach (var quote in tempQuotes)
            {
                var storedQuote = _dbContext.Quotes.Where(x => x.QuoteNumber == quote.QuoteNumber && x.Revision == quote.RevisionNumber).FirstOrDefault();
                if(storedQuote == null)
                {
                    continue;
                }

                var customerId = _dbContext.QuoteCustomer.FirstOrDefault(x => x.QuoteId == storedQuote.Id)?.CustomerId ?? 0;
                var customer = _dbContext.Customers.FirstOrDefault(x => x.Id == customerId);

                var salesPersonId = _dbContext.QuoteSalesPerson.FirstOrDefault(x => x.QuoteId == storedQuote.Id)?.SalesPersonId ?? 0;
                var salesPerson = _dbContext.SalesPersons.FirstOrDefault(x => x.Id == salesPersonId);

                quotes.Add(new PaperLess.Quote
                {
                    number = quote.QuoteNumber,
                    customer = new PaperLess.Customer
                    {
                        email = customer?.Email ?? "",
                        first_name = customer?.FirstName ?? "",
                        last_name = customer?.LastName ?? ""
                    },
                    salesperson = new PaperLess.SalesPerson
                    {
                        email = salesPerson?.Email ?? "",
                        first_name = salesPerson?.FirstName ?? "",
                        last_name = salesPerson?.LastName ?? ""
                    }
                });
            }

            return quotes
                .GroupBy(x => x.customer.email)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private bool IsEmailUnsubscribed(string email)
        {
            var unsubscribedEmail = _dbContext.UnsubscribeEmails.FirstOrDefault(x => x.Email == email && (x.IsUnsubscribed ?? false) == true);
            return unsubscribedEmail != null;
        }

        private void SetDBContext(string connectionString)
        {
            var services = new ServiceCollection();
            services.AddDbContextPool<QtdContext>(options =>
            options.UseSqlServer(connectionString));
            var serviceProvider = services.BuildServiceProvider();
            _dbContext = serviceProvider.GetService<QtdContext>();
        }

        private string PrepareLogMessage(string message)
        {
            return Environment.NewLine + $"{DateTime.Now:G}-{message}";
        }
        private void SetEmailSetting(JobDataMap dataMap)
        {
            _emailSetting = new EmailSetting
            {
                UserEmail = dataMap.GetString("UserEmail"),
                Host = dataMap.GetString("SmtpHost"),
                Port = dataMap.GetInt("SmtpPort"),
                EnableSsl = dataMap.GetBoolean("SmtpEnableSsl"),
                Password = dataMap.GetString("EmailPassword"),
                EmailSetupBaseUrl = dataMap.GetString("EmailSetupBaseUrl"),
                SetupEndpoint = dataMap.GetString("SetupEndpoint"),
                UnsubscribeEndpoint = dataMap.GetString("UnsubscribeEndpoint")
            };
        }

        private void WriteLog(string message)
        {
            StreamWriter outputFile = new StreamWriter($"{JobPathHelper.BasePath}QTDLogs//workflow1Once_{DateTime.Now:MMddyyyy}.txt", append: true);
            outputFile.WriteLine(message);
            outputFile.Close();
            outputFile.Dispose();
        }
    }
}