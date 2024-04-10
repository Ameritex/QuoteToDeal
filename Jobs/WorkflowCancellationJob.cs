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
using Newtonsoft.Json;

namespace Quote_To_Deal.Jobs
{
    [DisallowConcurrentExecution]
    public class WorkflowCancellationJob : IJob
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
                var storedQuotes = GetStoredOutstandingQuotes();
                //var storedMaxQuoteNumber = storedQuotes.Max(x => x.quote);

                //lastQuoteNo = storedMaxQuoteNumber > lastQuoteNo ? storedMaxQuoteNumber : lastQuoteNo;
                var tempLastQuote = SettingsHelpers.ReadCacheSetting<string>(Path.Combine(JobPathHelper.BasePath, quotePath), "LastQuote");
                var currLastQuote = JsonConvert.DeserializeObject<NewQuote>(tempLastQuote);

                WriteLog(PrepareLogMessage($"LAST QUOTE:{lastQuoteNo}"));

                var newQuotes = PaperLessAPIControl.GetNewQuotes(currLastQuote.quote, currLastQuote.revision);

                //var newQuotes = new List<NewQuote>();

                newQuotes.AddRange(storedQuotes);

                if(newQuotes == null)
                {
                    return;
                }

                foreach(var newQuote in newQuotes) 
                {
                    if (!IsQuoteExists(newQuote.quote))
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote {newQuote.quote} not stored in DB."));

                        continue;
                    }
                    var quote = PaperLessAPIControl.GetQuoteInformation(newQuote.quote, newQuote.revision);
                    if(quote == null)
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote {newQuote.quote} details not fetched from the Paperless portal."));

                        continue;
                    }

                    if(quote.status != "cancelled")
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote:{newQuote.quote}, status:{quote.status}: IsExpired:{quote.expired}"));

                        continue;
                    }
                    WriteLog(PrepareLogMessage($"Executing Quote:{newQuote.quote}"));

                    UpdateHsDeal(newQuote.quote);
                    UpdateQuoteStatus(newQuote.quote, quote.status);

                    //SettingsHelpers.AddOrUpdateAppSetting($"{Path.Combine(JobPathHelper.BasePath, quotePath)}", $"LastQuote", JsonConvert.SerializeObject(newQuote));
                    //WriteLog(PrepareLogMessage($"Last Quote number {newQuote} saved."));
                }
                if (!newQuotes.Any())
                                {
                    WriteLog(PrepareLogMessage($"No last quote fetched!"));
                }
            }
            catch (Exception ex)
            {
                WriteLog(PrepareLogMessage($"ERROR: {ex.Message}{Environment.NewLine}{ex.InnerException?.Message}{Environment.NewLine}{ex.StackTrace}"));
            }
        }

        private void UpdateHsDeal(long quoteNumber)
        {
            var hsDealId = GetStoredHsDealId(quoteNumber);
            if (hsDealId > 0)
            {
                var data = new DealHubSpotModel
                {
                    Id = hsDealId,
                    Stage = "closedlost"
                };
                _hubspotAPIControl.UpdateDeal(data);
            }
        }

        private IReadOnlyList<Team>? GetProductionTeamMembers()
        {
            return _dbContext.Teams
                .Where(x => x.TeamName == "production")
                .ToList();
        }

        private long GetStoredHsDealId(long? quoteNumber)
        {
            return _dbContext.Quotes.Where(x => x.QuoteNumber == quoteNumber).FirstOrDefault()?.HsDealId ?? 0;
        }

        private IEnumerable<NewQuote> GetStoredOutstandingQuotes()
        {
            var quotes =  _dbContext.Quotes.Where(x => x.Status == "outstanding" && (x.IsWorkflowCancellation ?? false) == false).ToList();
            foreach(var storedQuote in quotes)
            {
                yield return new NewQuote
                {
                    quote = storedQuote.QuoteNumber ?? 0
                };
            }
        }

        private void UpdateQuote(PaperLess.Quote quote)
        {
            var existingQuote = _dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quote.number);
            if (existingQuote != null)
            {
                existingQuote.Status = quote.status;

                _dbContext.SaveChanges();
            }
        }

        private void UpdateQuoteStatus(long quoteNumber, string status)
        {
            var existingQuote = _dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quoteNumber);
            if (existingQuote != null)
            {
                existingQuote.Status = status;
                existingQuote.IsWorkflowCancellation = true;
                _dbContext.SaveChanges();
            }
        }

        private double GetTotalPrice(IReadOnlyList< QuoteItem > quoteItems)
        {
            var totalPrice = 0.00;
            foreach(var quoteItem in quoteItems)
            {
                var amounts = new List<double>();
                foreach (var component in quoteItem.components)
                {

                    amounts.Add( component.quantities.LastOrDefault()?.total_price ?? 0);
                }
                totalPrice += amounts.Max();
            }

            return totalPrice;
        }

        private bool IsQuoteEligible(long quoteNo)
        {
            var quote = _dbContext.Quotes
                .FirstOrDefault(x => x.QuoteNumber == quoteNo 
                                && x.Status != "accepted" 
                                && x.IsWorkflow2 == false);
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

            var quote = new Data.Entities.Quote() {
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
                .Max(x => x.QuoteNumber) ?? 0;
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
            StreamWriter outputFile = new StreamWriter($"{JobPathHelper.BasePath}QTDLogs//workflow2_{DateTime.Now:MMddyyyy}.txt", append: true);
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