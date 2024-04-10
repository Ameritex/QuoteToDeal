using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Quote_To_Deal.Data;
using Quote_To_Deal.Common;
using HubSpot.NET.Api.Contact;
using HubSpot.NET.Core;
using Quote_To_Deal.Hubspot;
using HubSpot.NET.Core.Requests;
using Quote_To_Deal.PaperLess;
using Quote_To_Deal.Extensions;
using Quote_To_Deal.Data.Entities;
using Microsoft.IdentityModel.Protocols.Configuration;
using HubSpot.NET.Api.Deal.Dto;
using Quote_To_Deal.Models;
using HubSpot.NET.Api.Contact.Dto;
using Quote_To_Deal.Common;
using System.Reflection.Metadata.Ecma335;
using ServiceStack;

namespace Quote_To_Deal.Jobs
{
    [DisallowConcurrentExecution]
    public class Workflow3Job : IJob
    {
        private QtdContext _dbContext;
        private HubspotAPIControl _hubspotAPIControl;
        private EmailSetting _emailSetting;
        private long _hsAmeritexCompanyId;

        public async Task Execute(IJobExecutionContext context)
        {
            JobDataMap dataMap = context.JobDetail.JobDataMap;

            string connectionString = dataMap.GetString("ConnectionString");
            string hsApiKey = dataMap.GetString("PrivateAppKey");
            long lastQuoteNo = dataMap.GetLong("LastQuoteNumber");
            string quotePath = dataMap.GetString("QuotePath");
            string message = "";

            _hsAmeritexCompanyId = dataMap.GetLong("AmeritexCompanyId");

            try
            {
                SetDBContext(connectionString);
                SetHubSpotAPI(hsApiKey);
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
                                storedQuote.SentDate?.ToShortDateString(), storedQuote.QuoteNumber ?? 0,
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
                                "Workflow3_Customer", new List<string> { customer.Email });
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

        private long GetStoredHsDealId(long? quoteNumber)
        {
            return _dbContext.Quotes.Where(x => x.QuoteNumber == quoteNumber).FirstOrDefault()?.HsDealId ?? 0;
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

        private double GetTotalPrice(IReadOnlyList<QuoteItem> quoteItems)
        {
            var totalPrice = 0.00;
            foreach (var quoteItem in quoteItems)
            {
                var amounts = new List<double>();
                foreach (var component in quoteItem.components)
                {

                    amounts.Add(component.quantities.LastOrDefault()?.total_price ?? 0);
                }
                totalPrice += amounts.Max();
            }

            return totalPrice;
        }

        private bool IsQuoteEligible(long quoteNo, int? revision)
        {
            var quote = _dbContext.Quotes
                .FirstOrDefault(x => x.QuoteNumber == quoteNo
                                && x.Revision == revision 
                                && x.Status == "outstanding"
                                && x.IsWorkflow3 == false);
            return quote != null;
        }

        private bool IsQuoteExists(long quoteNo)
        {
            var quote = _dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quoteNo);
            return quote != null;
        }

        private void SaveQuote(PaperLess.Quote paperlessQuote, long hubspotContactId, long hubspotDealId)
        {
            var quoteCustomer = paperlessQuote.customer;
            var quoteSalesPerson = paperlessQuote.salesperson;

            var customerId = 0;
            var existingCustomer = _dbContext.Customers.FirstOrDefault(x => x.Email == quoteCustomer.email);
            if (existingCustomer == null)
            {
                var customer = new Data.Entities.Customer()
                {
                    Email = quoteCustomer.email,
                    FirstName = quoteCustomer.first_name,
                    LastName = quoteCustomer.last_name,
                    HsContactId = hubspotContactId,
                    Phone = quoteCustomer.phone,
                    PhoneExt = quoteCustomer.phone_ext,
                };

                _dbContext.Customers.Add(customer);
                _dbContext.SaveChanges();
                customerId = _dbContext.Customers.Max(x => x.Id);
            }
            else
            {
                customerId = existingCustomer.Id;
            }

            var quote = new Data.Entities.Quote()
            {
                CreatedDate = DateTime.Now,
                Email = paperlessQuote.email,
                ExpireDate = paperlessQuote.expired_date,
                FirstName = paperlessQuote.first_name,
                LastName = paperlessQuote.last_name,
                IsExpired = paperlessQuote.expired,
                Phone = paperlessQuote.phone,
                QuoteNumber = paperlessQuote.number,
                SentDate = paperlessQuote.sent_date,
                Status = paperlessQuote.status,
                TotalPrice = GetTotalPrice(paperlessQuote.quote_items),
                HsDealId = hubspotDealId,
                IsWorkflow3 = true,
                Revision = paperlessQuote.revision_number
            };

            _dbContext.Quotes.Add(quote);

            _dbContext.SaveChanges();

            var quoteId = _dbContext.Quotes.Max(x => x.Id);

            _dbContext.QuoteCustomer.Add(new QuoteCustomer
            {
                CustomerId = customerId,
                QuoteId = quoteId
            });

            var salesPersonId = 0;
            var salesPerson = _dbContext.SalesPersons.FirstOrDefault(x => x.Email == paperlessQuote.salesperson.email);
            if (salesPerson == null)
            {
                //var newSalesPerson = paperlessQuote.salesperson.ConvertTo<Data.Entities.SalesPerson>();
                var newSalesPerson = new Data.Entities.SalesPerson
                {
                    Email = paperlessQuote.salesperson.email,
                    FirstName = paperlessQuote.salesperson.first_name,
                    LastName = paperlessQuote.salesperson.last_name
                };

                _dbContext.SalesPersons.Add(newSalesPerson);

                _dbContext.SaveChanges();

                salesPersonId = _dbContext.SalesPersons.Max(x => x.Id);
            }
            else
            {
                salesPersonId = salesPerson.Id;

            }

            _dbContext.QuoteSalesPerson.Add(new QuoteSalesPerson { QuoteId = quoteId, SalesPersonId = salesPersonId });

            _dbContext.SaveChanges();

        }

        private long GetMaxQuoteNumberFromDB()
        {
            return _dbContext.Quotes
                .Where(x => x.Status == "outstanding")
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefault()
                ?.QuoteNumber
                ?? 0;
        }

        private void SetHubSpotAPI(string? hsApiKey)
        {
            _hubspotAPIControl = new HubspotAPIControl(hsApiKey);
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
