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
using System.Data;
using RestSharp.Extensions;
using Newtonsoft.Json;

namespace Quote_To_Deal.Jobs
{
    [DisallowConcurrentExecution]
    public class Workflow4Job : IJob
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

                var (storedQuotes, maxQuoteNo) = GetEligibleQuotes();
                //var maxQuoteNo = storedQuotes.Max(x => x.quote);

                //var currLastQuote = SettingsHelpers.ReadCacheSetting<NewQuote?>(Path.Combine(JobPathHelper.BasePath, quotePath), "LastQuote");

                var tempLastQuote = SettingsHelpers.ReadCacheSetting<string>(Path.Combine(JobPathHelper.BasePath, quotePath), "LastQuote");

                var currLastQuote = JsonConvert.DeserializeObject<NewQuote>(tempLastQuote);

                var newQuotes = PaperLessAPIControl.GetNewQuotes(currLastQuote.quote, currLastQuote.revision);

                newQuotes.AddRange(storedQuotes);

                //var newQuotes = new List<NewQuote>()
                //{
                //    new NewQuote { quote = 8805, revision = null }
                //};

                if (newQuotes == null)
                {
                    return;
                }

                foreach (var newQuote in newQuotes)
                {
                    WriteLog(PrepareLogMessage($"Executing Quote:{newQuote.quote}"));
                    if (!IsQuoteExists(newQuote.quote, newQuote.revision))
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote {newQuote.quote}. Quote not stored in DB."));

                        continue;
                    }
                    var quote = PaperLessAPIControl.GetQuoteInformation(newQuote.quote, newQuote.revision);
                    if (quote == null)
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote {newQuote.quote} details not fetched from the Paperless portal."));

                        continue;
                    }

                    if (quote.status != "accepted")
                    {
                        UpdateQuote(newQuote.quote, newQuote.revision, false, quote.status);
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote {newQuote.quote}. Status:{quote.status}."));
                        continue;
                    }

                    if (!quote.order_numbers.Any())
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote {newQuote.quote}. Order not found."));

                        continue;
                    }

                    var orderId = quote.order_numbers.Max();

                    var order = PaperLessAPIControl.GetOrderInformation(orderId);

                    var dateDiff = Utils.GetBusinessDays(order.ships_on ?? DateTime.Now, DateTime.Now);

                    if (order.status != "completed" || dateDiff < 2)
                    {
                        //UpdateQuote(newQuote.quote, false, quote.status);
                        UpdateOrder(newQuote.quote, newQuote.revision, false, order);
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote {newQuote.quote}. Order:{order.number}. OrderStatus:{order.status}. DateDiff:{dateDiff}"));
                        continue;
                    }

                    UpdateHsDeal(newQuote.quote);
                    UpdateOrder(newQuote.quote, newQuote.revision, true, order);

                    if (!string.IsNullOrEmpty(quote.customer.email))
                    {
                        var (quoteId, customerId) = GetQuoteCustomerIds(newQuote.quote);
                        try
                        {
                            Utils.SendEmail(_emailSetting, new List<PaperLess.Quote> { quote }, "Workflow4", new List<string> { quote.customer.email }
                                        , order.ships_on.GetValueOrDefault().ToShortDateString(), EncryptDecrypt.Encrypt(customerId.ToString())
                                        , EncryptDecrypt.Encrypt(quoteId.ToString()));
                        }
                        catch (Exception ex)
                        {
                            WriteLog(PrepareLogMessage($"EMAIL ERROR: {ex.Message}{Environment.NewLine}{ex.InnerException?.Message}{Environment.NewLine}{ex.StackTrace}"));
                        }
                    }

                    SettingsHelpers.AddOrUpdateAppSetting($"{Path.Combine(JobPathHelper.BasePath, quotePath)}", $"LastOrder", order.number.ToString());
                    WriteLog(PrepareLogMessage($"Last Order number {order.number} saved."));
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
                    Stage = "152024072"//Completed
                };
                _hubspotAPIControl.UpdateDeal(data);
            }
        }

        private (int, int) GetQuoteCustomerIds(long quoteNumber)
        {
            var quote = _dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quoteNumber);

            var customerId = _dbContext.QuoteCustomer.FirstOrDefault(x => x.QuoteId == quote.Id)?.CustomerId ?? 0;
            return (quote.Id, customerId);
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

        private (IEnumerable<NewQuote>, long) GetEligibleQuotes()
        {
            var quotes = _dbContext.Quotes.Where(x => (x.Status == "outstanding" 
                                                                || x.Status == "accepted" 
                                                                || (!string.IsNullOrEmpty(x.OrderStatus) && x.OrderStatus != "completed")) 
                                                                && (x.IsWorkflow4 ?? false) == false)
                        .ToList();

            var maxQuoteNumber = quotes.OrderByDescending(x => x.CreatedDate).FirstOrDefault()?.QuoteNumber  ?? 0;

            //var quotes = unApprovedQuotes.Where(x => Utils.GetBusinessDays(x.ShipOn ?? DateTime.Now, DateTime.Now) >= 2);
            var newQuotes = new List<NewQuote>();
            foreach (var storedQuote in quotes)
            {
                newQuotes.Add(
                    new NewQuote
                    {
                        quote = storedQuote.QuoteNumber ?? 0
                    });
            }
            return (newQuotes, maxQuoteNumber);
        }

        private void UpdateQuote(long? quoteNumber, int? revision, bool updateStatus, string? status = null)
        {
            if (quoteNumber == null) { return; }

            var existingQuote = _dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quoteNumber && x.Revision == revision);
            if (existingQuote != null)
            {
                if (!string.IsNullOrEmpty(status))
                {
                    existingQuote.Status= status;
                }
                if (updateStatus)
                {
                    existingQuote.IsWorkflow4 = true;
                }

                _dbContext.SaveChanges();
            }
        }

        private void UpdateOrder(long? quoteNumber, int? revision, bool updateStatus, OrderModel order)
        {
            if (quoteNumber == null) { return; }

            var existingQuote = _dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quoteNumber && x.Revision == revision);
            if (existingQuote != null)
            {
                existingQuote.OrderStatus = order.status;
                existingQuote.OrderNumber = order.number;
                if (updateStatus)
                {
                    existingQuote.IsWorkflow4 = true;
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

        private bool IsQuoteEligible(long quoteNo)
        {
            var quote = _dbContext.Quotes
                .FirstOrDefault(x => x.QuoteNumber == quoteNo
                                && x.Status == "outstanding"
                                && x.IsWorkflow4 == false);
            return quote != null;
        }

        private bool IsQuoteExists(long quoteNo, int? revision)
        {
            var quote = _dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quoteNo && x.Revision == revision);
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
                IsWorkflow4 = true,
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
            StreamWriter outputFile = new StreamWriter($"{JobPathHelper.BasePath}QTDLogs//workflow4_{DateTime.Now:MMddyyyy}.txt", append: true);
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
