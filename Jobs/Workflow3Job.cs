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
using Quote_To_Deal.Hubspot;
using Quote_To_Deal.PaperLess;
using Quote_To_Deal.Models;

namespace Quote_To_Deal.Jobs
{
    [DisallowConcurrentExecution]
    public class Workflow3Job : IJob
    {
        private QtdContext _dbContext;
        private EmailSetting _emailSetting;

        public async Task Execute(IJobExecutionContext context)
        {
            JobDataMap dataMap = context.JobDetail.JobDataMap;

            string connectionString = dataMap.GetString("ConnectionString") ?? "";

            try
            {
                SetDBContext(connectionString);
                SetEmailSetting(dataMap);
                var storedQuotes = GetStoredUnapprovedEligibleQuotes();

                if (storedQuotes == null)
                {
                    return;
                }

                foreach (var storedQuote in storedQuotes)
                {
                    WriteLog(PrepareLogMessage($"Executing Quote:{storedQuote.QuoteNumber}"));

                    var quote = PaperLessAPIControl.GetQuoteInformation(storedQuote.QuoteNumber ?? 0, storedQuote.Revision);
                    if (quote == null)
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote {storedQuote.QuoteNumber} details not fetched from the Paperless portal."));

                        continue;
                    }
                    var dateDiff = Utils.GetBusinessDays(quote.sent_date ?? DateTime.Now, DateTime.Now);

                    if (quote.status != "outstanding" || dateDiff < 7)
                    {
                        //Update quote status
                        UpdateQuote(storedQuote.QuoteNumber, storedQuote.Revision, false, quote.status);
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote {storedQuote.QuoteNumber}. Status:{quote.status}. DateDiff:{dateDiff}"));
                        continue;
                    }

                    var salesPerson = GetSalesPerson(storedQuote.Id);
                    var customer = GetCustomer(storedQuote.Id); 
                    if (salesPerson != null)
                    {
                        try
                        {
                            Utils.SendEmail(_emailSetting, salesPerson, customer,
                                storedQuote.SentDate?.ToShortDateString() ?? DateTime.Now.ToShortDateString(), storedQuote.QuoteNumber ?? 0,
                                "Workflow3_Sales", new List<string> { salesPerson.Email });
                        }
                        catch (Exception ex)
                        {
                            WriteLog(PrepareLogMessage($"EMAIL ERROR: {ex.Message}{Environment.NewLine}{ex.InnerException?.Message}{Environment.NewLine}{ex.StackTrace}"));
                        }
                    }

                    if (customer != null)
                    {
                        try
                        {
                            Utils.SendEmail(_emailSetting, customer, salesPerson,
                                storedQuote.SentDate?.ToShortDateString() ?? "", storedQuote.QuoteNumber ?? 0,
                                "Workflow3_Customer", new List<string> { customer.Email }, salesPerson.FirstName);
                        }
                        catch (Exception ex)
                        {
                            WriteLog(PrepareLogMessage($"EMAIL ERROR: {ex.Message}{Environment.NewLine}{ex.InnerException?.Message}{Environment.NewLine}{ex.StackTrace}"));
                        }
                    }

                    UpdateQuote(storedQuote.QuoteNumber, storedQuote.Revision, true, quote.status);
                }
            }
            catch (Exception ex)
            {
                WriteLog(PrepareLogMessage($"ERROR: {ex.Message}{Environment.NewLine}{ex.InnerException?.Message}{Environment.NewLine}{ex.StackTrace}"));
            }
        }

        private Data.Entities.Customer? GetCustomer(int quoteId)
        {
            var customerId = _dbContext.QuoteCustomer.FirstOrDefault(x => x.QuoteId == quoteId)?.CustomerId;

            if (customerId == null)
                return null;
            var customer = _dbContext.Customers.FirstOrDefault(x => x.Id == customerId);
            return customer;
        }

        private Data.Entities.SalesPerson? GetSalesPerson(int quoteId)
        {
            var salesPersonId = _dbContext.QuoteSalesPerson.FirstOrDefault(x => x.QuoteId == quoteId)?.SalesPersonId;
            if (salesPersonId == null) { return null; }

            return _dbContext.SalesPersons.FirstOrDefault(x => x.Id == salesPersonId);
        }

        private IEnumerable<Data.Entities.Quote> GetStoredUnapprovedEligibleQuotes()
        {
            var unApprovedQuotes = _dbContext.Quotes.Where(x => x.Status == "outstanding" && (x.IsWorkflow3 ?? false) == false).ToList();
            var quotes = unApprovedQuotes.Where(x => Utils.GetBusinessDays(x.SentDate ?? DateTime.Now, DateTime.Now) >= 7);
            return quotes;
        }

        private void UpdateQuote(long? quoteNumber, int? revision, bool updateStatus, string? status = null)
        {
            if(quoteNumber == null) { return; }

            var existingQuote = _dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quoteNumber && x.Revision == revision);
            if (existingQuote != null)
            {
                if (!string.IsNullOrEmpty(status))
                {
                    existingQuote.Status = status;
                }
                if (updateStatus)
                {
                    existingQuote.IsWorkflow3 = true;
                }
                _dbContext.SaveChanges();
            }
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

        private void WriteLog(string message)
        {
            StreamWriter outputFile = new StreamWriter($"{JobPathHelper.BasePath}QTDLogs//workflow3_{DateTime.Now:MMddyyyy}.txt", append: true);
            outputFile.WriteLine(message);
            outputFile.Close();
            outputFile.Dispose();
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
            };
        }
    }
}
