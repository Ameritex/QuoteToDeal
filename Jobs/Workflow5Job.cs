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
using ServiceStack;
using HubSpot.NET.Api.Task.Dto;
using Microsoft.Extensions.Hosting;
using System.Data.Entity.SqlServer;
using Newtonsoft.Json;
using System.Data;

namespace Quote_To_Deal.Jobs
{
    [DisallowConcurrentExecution]
    public class Workflow5Job : IJob
    {
        private QtdContext _dbContext;
        private HubspotAPIControl _hubspotAPIControl;
        private List<string>  _statuses = new List<string>() { "expired", "outstanding" };
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
                //var storedMaxQuoteNumber = GetExpiredMaxQuoteNumberFromDB();
                //lastQuoteNo = storedMaxQuoteNumber > lastQuoteNo ? storedMaxQuoteNumber : lastQuoteNo;
                //lastQuoteNo = lastQuoteNo;

                var tempLastQuote = SettingsHelpers.ReadCacheSetting<string>(Path.Combine(JobPathHelper.BasePath, quotePath), "LastQuote");
                var currLastQuote = JsonConvert.DeserializeObject<NewQuote>(tempLastQuote);

                WriteLog(PrepareLogMessage($"LAST QUOTE:{currLastQuote.quote} , REVISION: {currLastQuote.revision}"));

                var newQuotes = PaperLessAPIControl.GetNewQuotes(currLastQuote.quote, currLastQuote.revision);

                newQuotes.AddRange(storedQuotes);

                if (newQuotes == null)
                {
                    return;
                }

                foreach (var newQuote in newQuotes) 
                {
                    var quote = PaperLessAPIControl.GetQuoteInformation(newQuote.quote, newQuote.revision);
                    if(quote == null)
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote {newQuote.quote} details not fetched from the Paperless portal."));

                        continue;
                    }

                    if (!IsQuoteEligible(quote))
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote:{newQuote.quote}, status:{quote.status}: IsExpired:{quote.expired}, ExpiredDate:{quote.expired_date}"));

                        continue;
                    }
                    WriteLog(PrepareLogMessage($"Executing Quote:{newQuote.quote}"));

                    if (!IsQuoteExists(newQuote.quote))
                    {
                        var hsContactId = _hubspotAPIControl.GetContactIdByEmail(quote.customer.email);

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

                        //var data = new DealHubSpotModel
                        //{
                        //    Name = $"{quote.customer.first_name ?? "Unknown"} {quote.customer.last_name ?? "Unknown"}-{quote.number}",
                        //    Stage = GetStageId(quote.status),
                        //    CloseDate = quote.expired_date ?? DateTime.Now,
                        //    Amount = GetTotalPrice(quote.quote_items),
                        //    DealType = "newbusiness",
                        //    Pipeline = "default",
                        //    DateCreated = quote.created ?? DateTime.Now,
                        //};

                        //var hsDealId = _hubspotAPIControl.CreateDeal(data, hsContactId);
                        //WriteLog(PrepareLogMessage($"New Hubspot deal created successfully with Id:{hsDealId}"));
                        var hsDealId = 0;
                        SaveQuote(quote, hsContactId, hsDealId);

                        WriteLog(PrepareLogMessage($"Quote saved in DB succefully."));
                    }
                    else
                    {
                        UpdateQuote(quote);

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

                    TaskHubSpotModel data = new TaskHubSpotModel()
                    {
                        Notes=$"Quote {quote.number} is expired or about to expired. Please contact the customer at {quote.customer.email} on priority basis.",
                        DueDate= DateTime.Now.AddDays(1),
                        Priority="High",
                        Status="Waiting",
                        Subject=$"Quote {quote.number}- Follow-up Required",
                        Type="To-do",
                        OwnerId= hsSalesPersonId
                    };

                    _hubspotAPIControl.CreateTask(data, hsSalesPersonId, _hsAmeritexCompanyId, 
                        $"The quote {quote.number} is near to expire. Following is customer's information\r\nName:{quote.customer.first_name } {quote.customer.last_name}\r\nPhone:{quote.customer.phone}\r\nEmail:{quote.customer.email} ",
                        $"Follow up quote {quote.number}");
                    Utils.SendEmail(_emailSetting, new List<PaperLess.Quote> { quote }, "Workflow5", new List<string> { quote.salesperson.email });

                }
                //if (newQuotes.Any())
                //{
                //    SettingsHelpers.AddOrUpdateAppSetting($"{Path.Combine(JobPathHelper.BasePath, quotePath)}", $"LastQuote", newQuotes.Max(x => x.quote).ToString());
                //    WriteLog(PrepareLogMessage($"Last Quote number {newQuotes.Max(x => x.quote)} saved."));
                //}
                //else
                {
                    WriteLog(PrepareLogMessage($"No last quote fetched!"));
                }
            }
            catch (Exception ex)
            {
                WriteLog(PrepareLogMessage($"ERROR: {ex.Message}{Environment.NewLine}{ex.InnerException?.Message}{Environment.NewLine}{ex.StackTrace}"));
            }
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

        private void UpdateQuote(PaperLess.Quote quote)
        {
            var existingQuote = _dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quote.number && x.Revision == quote.revision_number);
            if (existingQuote != null)
            {
                existingQuote.Status = quote.status;
                existingQuote.IsWorkflow5 = true;

                _dbContext.SaveChanges();
            }
        }

        private bool IsQuoteEligible(PaperLess.Quote quote)
        {
            var result1 = quote.status == "expired" || quote.status == "outstanding" || quote.expired;
            var result2 = //(GetDateDiff(quote.expired_date.GetValueOrDefault(), DateTime.Now) <= 24) || 
                                    (_statuses.Contains(quote.status) &&
                                       GetDateDiff(quote.expired_date.GetValueOrDefault(), DateTime.Now).IsBetween(0,24));
            var result3 = IsWorkflow5Executed(quote.number);
            return result1 && result2 && !result3;
        }

        private bool IsWorkflow5Executed(long? quoteNumber)
        {
            if (quoteNumber == null) { return false; }  

            return _dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quoteNumber)?.IsWorkflow5 ?? false;
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

        private string GetStageId(string quoteStatus)
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
            }

            return stageId;
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
                    HsContactId = hubspotContactId
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
                IsWorkflow5 = true,
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

        private long GetExpiredMaxQuoteNumberFromDB()
        {
            var aQuery = from q in _dbContext.Quotes
                         where (q.ExpireDate.HasValue && (EF.Functions.DateDiffHour(DateTime.Now, q.ExpireDate.Value) > 0
                                    && EF.Functions.DateDiffHour(DateTime.Now, q.ExpireDate.Value) <= 24))
                                    || (_statuses.Contains(q.Status)) && (EF.Functions.DateDiffHour(q.ExpireDate.Value, DateTime.Now) > 0
                                    && EF.Functions.DateDiffHour(q.ExpireDate.Value, DateTime.Now) <= 24)
                         select new { q.QuoteNumber, q.CreatedDate };
            var aa = aQuery.ToList();

            //select SqlFunctions.DateDiff("hour", q.ExpireDate.GetValueOrDefault().ToString("mm/dd/yyyy"), DateTime.Now.ToString("mm/dd/yyyy"));
            //var bQuery = from q in _dbContext.Quotes
            //    where _statuses.Contains(q.Status)
            //    && q.ExpireDate.HasValue && (EF.Functions.DateDiffHour(q.ExpireDate.Value, DateTime.Now) > 24)
            //             select q;
            ////select SqlFunctions.DateDiff("hour", q.ExpireDate.GetValueOrDefault().ToString("mm/dd/yyyy"), DateTime.Now.ToString("mm/dd/yyyy"));
            //var bb = bQuery.ToList();
            //aa.AddRange(bb);

            return aa.OrderByDescending(x => x.CreatedDate).FirstOrDefault()?.QuoteNumber ?? 0;
        }

        private double GetDateDiff(DateTime date1, DateTime date2)
        {
            var diff = date1 - date2;
            return diff.TotalHours;
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

        private IEnumerable<NewQuote> GetStoredOutstandingQuotes()
        {
            var quotes = _dbContext.Quotes.Where(x => x.Status == "outstanding" && (x.IsWorkflow5 ?? false) == false).ToList();
            foreach (var storedQuote in quotes)
            {
                yield return new NewQuote
                {
                    quote = storedQuote.QuoteNumber ?? 0
                };
            }
        }

        private void WriteLog(string message)
        {
            StreamWriter outputFile = new StreamWriter($"{JobPathHelper.BasePath}QTDLogs//workflow5_{DateTime.Now:MMddyyyy}.txt", append: true);
            outputFile.WriteLine(message);
            outputFile.Close();
            outputFile.Dispose();
        }
    }
}