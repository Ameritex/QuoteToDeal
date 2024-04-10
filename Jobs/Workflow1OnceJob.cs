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
using HubSpot.NET.Api.Task.Dto;
using Newtonsoft.Json;
using System.Collections;

namespace Quote_To_Deal.Jobs
{
    [DisallowConcurrentExecution]
    public class Workflow1OnceJob : IJob
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
                var storedMaxQuoteNumber = GetMaxQuoteNumberFromDB();

                //SettingsHelpers.AddOrUpdateAppSetting($"{Path.Combine(JobPathHelper.BasePath, quotePath)}",
                //$"Test", Newtonsoft.Json.JsonConvert.SerializeObject( new NewQuote { quote = 123, revision=null}));

                var tempLastQuote = SettingsHelpers.ReadCacheSetting<string>(Path.Combine(JobPathHelper.BasePath, quotePath), "LastQuote");

                var currLastQuote = JsonConvert.DeserializeObject<NewQuote>(tempLastQuote);

                //lastQuoteNo = storedMaxQuoteNumber > lastQuoteNo ? storedMaxQuoteNumber : lastQuoteNo;
                //lastQuoteNo = lastQuoteNo;
                WriteLog(PrepareLogMessage($"LAST QUOTE:{currLastQuote.quote}, REVISION:{currLastQuote.revision}"));

                var newQuotes = PaperLessAPIControl.GetNewQuotes(currLastQuote.quote, currLastQuote.revision);

                if (!newQuotes.Any())
                {
                    WriteLog(PrepareLogMessage($"No last quote fetched!"));
                    return;
                }

                var quotes = new List<PaperLess.Quote>();

                foreach (var newQuote in newQuotes)
                {
                    if (IsQuoteExists(newQuote.quote, newQuote.revision))
                    {
                        continue;
                    }

                    if (!IsQuoteEligible(newQuote.quote, newQuote.revision))
                    {
                        continue;
                    }

                    var quote = PaperLessAPIControl.GetQuoteInformation(newQuote.quote, newQuote.revision);
                    if (quote == null)
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote {newQuote.quote} details not fetched from the Paperless portal."));

                        continue;
                    }

                    if ((quote.status != "draft" && quote.status != "accepted" && quote.status != "outstanding") || quote.expired)
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote:{newQuote.quote}, status:{quote.status}: IsExpired:{quote.expired}"));

                        continue;
                    }
                    WriteLog(PrepareLogMessage($"Executing Quote:{newQuote.quote}"));

                    quotes.Add(quote);
                }

                Dictionary<string, List<PaperLess.Quote>> groupedQuotes = quotes
                                                                            .GroupBy(x => x.customer.email)
                                                                            .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var groupedQuote in groupedQuotes)
                {
                    var customerEmail = groupedQuote.Key;
                   foreach (var quote in groupedQuote.Value)
                    {
                        var hsContactId = _hubspotAPIControl.GetContactIdByEmail(customerEmail);

                        if (hsContactId == 0)
                        {
                            //create new contact in hubspot
                            var newContact = new ContactHubSpotModel
                            {
                                Email = quote.customer.email,
                                FirstName = quote.customer.first_name,
                                LastName = quote.customer.last_name,
                                Phone = quote.customer.phone
                            };

                            var hsCompanyId = _hubspotAPIControl.GetCompanyIdByName(quote.customer.company?.business_name ?? "");

                            if (hsCompanyId == 0)
                            {
                                WriteLog(PrepareLogMessage($"--SKIP-- Company {quote.customer.company?.business_name} not found in HubSpot"));
                                continue;
                            }

                            hsContactId = _hubspotAPIControl.CreateContact(newContact, hsCompanyId);

                            WriteLog(PrepareLogMessage($"New hubspot contact with email {newContact.Email} created. HubspotContactId: {hsContactId}"));
                        }

                        var hsSalesPersonId = _hubspotAPIControl.GetContactIdByEmail(quote.salesperson.email);

                        if (hsSalesPersonId == 0)
                        {
                            //create new contact in hubspot
                            var newSalesPerson = new ContactHubSpotModel
                            {
                                Email = quote.salesperson.email,
                                FirstName = quote.salesperson.first_name,
                                LastName = quote.salesperson.last_name
                            };

                            hsSalesPersonId = _hubspotAPIControl.CreateContact(newSalesPerson, _hsAmeritexCompanyId);

                            WriteLog(PrepareLogMessage($"New hubspot sales person with email {newSalesPerson.Email} created. HubspotSalesPersonId: {hsSalesPersonId}"));
                        }

                        var hsOwnerId = _hubspotAPIControl.GetOwnerId(quote.salesperson.email);
                        var data = new DealHubSpotModel
                        {
                            Name = $"{quote.customer.first_name ?? "Unknown"} {quote.customer.last_name ?? "Unknown"}-{quote.number}",
                            Stage = GetStageId(quote.status, quote.sent_date),
                            CloseDate = quote.expired_date ?? DateTime.Now,
                            Amount = GetTotalPrice(quote.quote_items),
                            DealType = "newbusiness",
                            Pipeline = "default",
                            DateCreated = quote.created ?? DateTime.Now,
                            OwnerId = hsOwnerId,
                        };

                        var hsDealId = _hubspotAPIControl.CreateDeal(data, hsContactId);
                        WriteLog(PrepareLogMessage($"New Hubspot deal created successfully with Id:{hsDealId}"));

                        SaveQuote(quote, hsContactId, hsDealId);

                        WriteLog(PrepareLogMessage($"Quote saved in DB succefully."));

                        TaskHubSpotModel taskData = new TaskHubSpotModel()
                        {
                            Notes = $"The Quote {quote.number} has been sent to the customer at {quote.customer.email}. Please follow up within 24 hours on priority basis.",
                            DueDate = DateTime.Now.AddDays(1),
                            Priority = "High",
                            Status = "Waiting",
                            Subject = $"New Quote {quote.number}- Follow-up Required",
                            Type = "To-do",
                            OwnerId = hsSalesPersonId
                        };

                        _hubspotAPIControl.CreateTask(taskData, hsSalesPersonId, _hsAmeritexCompanyId,
                            $"The quote {quote.number} has been to the client. Please follow up within 24 hours on priority basis.\r\nFollowing is customer's information\r\nName: {quote.customer.first_name} {quote.customer.last_name}\r\nPhone: {quote.customer.phone}\r\nEmail: {quote.customer.email} ",
                            $"Follow up new quote {quote.number}");
                    }

                    if (!string.IsNullOrEmpty(customerEmail))
                    {
                        try
                        {
                            //if (!IsEmailUnsubscribed(customerEmail))
                            {
                                Utils.SendEmail(_emailSetting, groupedQuote.Value, "Workflow1Once", new List<string> { customerEmail });
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteLog(PrepareLogMessage($"EMAIL ERROR: {ex.Message}{Environment.NewLine}{ex.InnerException?.Message}{Environment.NewLine}{ex.StackTrace}"));
                        }
                    }

                    var lastCustomerQuote = groupedQuote.Value.OrderByDescending(x => x.created).FirstOrDefault();

                    SettingsHelpers.AddOrUpdateAppSetting($"{Path.Combine(JobPathHelper.BasePath, quotePath)}", $"LastQuote", JsonConvert.SerializeObject(lastCustomerQuote));
                    WriteLog(PrepareLogMessage($"Last Quote number {lastCustomerQuote} saved."));
                }
            }
            catch (Exception ex)
            {
                WriteLog(PrepareLogMessage($"ERROR: {ex.Message}{Environment.NewLine}{ex.InnerException?.Message}{Environment.NewLine}{ex.StackTrace}"));
            }
        }

        private bool IsEmailUnsubscribed(string email)
        {
            var unsubscribedEmail = _dbContext.UnsubscribeEmails.FirstOrDefault(x => x.Email == email);
            return unsubscribedEmail != null;
        }

        private void UpdateQuote(PaperLess.Quote quote)
        {
            var existingQuote = _dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quote.number && x.Revision == quote.revision_number);
            if (existingQuote != null)
            {
                existingQuote.Status = quote.status;
                existingQuote.IsExpired = quote.expired;

                _dbContext.SaveChanges();
            }
        }

        private bool IsQuoteEligible(long quoteNumber, int? revision)
        {
            return !(_dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quoteNumber && x.Revision == revision)?.IsWorkflow1 ?? false);
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

        private string GetStageId(string quoteStatus, DateTime? sentDate)
        {
            var stageId = "";
            
            switch (quoteStatus)
            {
                case "accepted":
                   stageId =  "152024071";
                        break;
                case "closed":
                    stageId = "152024072";
                    break;
                case "draft":
                    stageId = "appointmentscheduled";
                    break;
                case "outstanding" :
                    if (sentDate.HasValue)
                    {
                        stageId = "presentationscheduled";
                    }
                   
                    break;
            }

            return stageId;
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
                IsWorkflow1 = true,
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
            return _dbContext.Quotes.OrderByDescending(x => x.CreatedDate).FirstOrDefault()?.QuoteNumber ?? 0;
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

        private void WriteLog(string message)
        {
            StreamWriter outputFile = new StreamWriter($"{JobPathHelper.BasePath}QTDLogs//workflow1Once_{DateTime.Now:MMddyyyy}.txt", append: true);
            outputFile.WriteLine(message);
            outputFile.Close();
            outputFile.Dispose();
        }
    }
}