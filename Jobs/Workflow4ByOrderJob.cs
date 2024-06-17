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
using Quote_To_Deal.Data.Entities;
using HubSpot.NET.Api.Deal.Dto;
using Quote_To_Deal.Models;
using HubSpot.NET.Api.Contact.Dto;
using System.Data;

namespace Quote_To_Deal.Jobs
{
    [DisallowConcurrentExecution]
    public class Workflow4ByOrderJob : IJob
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
            long lastOrderNo = dataMap.GetLong("LastOrderNumber");
            string quotePath = dataMap.GetString("QuotePath");

            _hsAmeritexCompanyId = dataMap.GetLong("AmeritexCompanyId");

            try
            {
                SetDBContext(connectionString);
                SetHubSpotAPI(hsApiKey);
                SetEmailSetting(dataMap);

                var currLastOrderNo = SettingsHelpers.ReadCacheSetting<long?>(Path.Combine(JobPathHelper.BasePath, quotePath), "LastOrder") ?? lastOrderNo;

                var newOrders = PaperLessAPIControl.GetNewOrders(currLastOrderNo);

                //var newOrders = new List<long>()
                //{
                //    4257
                //};

                if (newOrders == null)
                {
                    return;
                }

                foreach (var newOrder in newOrders)
                {
                    //WriteLog(PrepareLogMessage($"Executing Quote:{newQuote.quote}"));
                    //if (!IsQuoteExists(newQuote.quote, newQuote.revision))
                    //{
                    //    WriteLog(PrepareLogMessage($"--SKIP-- Quote {newQuote.quote}. Quote not stored in DB."));

                    //    continue;
                    //}

                    if (IsOrderExists(newOrder) && !IsOrderEligible(newOrder))
                    {
                        continue;
                    }

                    var order = PaperLessAPIControl.GetOrderInformation(newOrder);
                    if (order == null)
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Order {newOrder} details not fetched from the Paperless portal."));

                        continue;
                    }

                    if (order.status != "completed")
                    {
                        //UpdateQuote(newQuote.quote, newQuote.revision, false, quote.status);
                        WriteLog(PrepareLogMessage($"--SKIP-- Order {newOrder}. Status:{order.status}."));
                        continue;
                    }

                    if (order.quote_number == null)
                    {
                        WriteLog(PrepareLogMessage($"--SKIP-- Order {newOrder} not found."));

                        continue;
                    }

                    var quoteRevision = order.quote_revision_number;
                    var quoteNo = order.quote_number;


                    var quote = PaperLessAPIControl.GetQuoteInformation(quoteNo ?? 0, quoteRevision);

                    if(!IsQuoteExists(quoteNo ?? 0, quoteRevision))
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
                            Stage = "152024072",
                            CloseDate = quote.expired_date ?? DateTime.Now,
                            Amount = GetTotalPrice(quote.quote_items),
                            DealType = "newbusiness",
                            Pipeline = "default",
                            DateCreated = quote.created ?? DateTime.Now,
                            OwnerId = hsOwnerId,
                        };

                        var hsDealId = _hubspotAPIControl.CreateDeal(data, hsContactId);
                        WriteLog(PrepareLogMessage($"New Hubspot deal created successfully with Id:{hsDealId}"));

                        SaveQuote(quote, hsContactId, hsDealId, newOrder);

                        WriteLog(PrepareLogMessage($"Quote saved in DB succefully."));
                    }

                    var dateDiff = Utils.GetBusinessDays(order.ships_on ?? DateTime.Now, DateTime.Now);

                    if (dateDiff < 2)
                    {
                        UpdateOrder(quoteNo, quoteRevision, false, order);
                        WriteLog(PrepareLogMessage($"--SKIP-- Quote {quoteNo}. Order:{newOrder}. OrderStatus:{order.status}. DateDiff:{dateDiff}"));
                        continue;
                    }

                    UpdateHsDeal(quoteNo ?? 0);
                    UpdateOrder(quoteNo ?? 0, quoteRevision, true, order);

                    if (!string.IsNullOrEmpty(quote.customer.email))
                    {
                        var (quoteId, customerId) = GetQuoteCustomerIds(quoteNo ?? 0);
                        try
                        {
                            Utils.SendEmail(_emailSetting, new List<PaperLess.Quote> { quote }, "Workflow4", new List<string> { quote.customer.email }
                                        , order.ships_on.GetValueOrDefault().ToShortDateString(), EncryptDecrypt.Encrypt(customerId.ToString()), 
                                        EncryptDecrypt.Encrypt(quoteId.ToString()), quote.salesperson.first_name);
                        }
                        catch (Exception ex)
                        {
                            WriteLog(PrepareLogMessage($"EMAIL ERROR: {ex.Message}{Environment.NewLine}{ex.InnerException?.Message}{Environment.NewLine}{ex.StackTrace}"));
                        }
                    }

                    SettingsHelpers.AddOrUpdateAppSetting($"{Path.Combine(JobPathHelper.BasePath, quotePath)}", $"LastOrder", newOrder.ToString());
                    WriteLog(PrepareLogMessage($"Last Order number {newOrder} saved."));
                }

                if (!newOrders.Any())
                {
                    WriteLog(PrepareLogMessage($"No last order fetched!"));
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

        private long GetStoredHsDealId(long? quoteNumber)
        {
            return _dbContext.Quotes.Where(x => x.QuoteNumber == quoteNumber).FirstOrDefault()?.HsDealId ?? 0;
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

        private bool IsOrderEligible(long orderNo)
        {
            var quote = _dbContext.Quotes
                .FirstOrDefault(x => x.OrderNumber == orderNo
                                && x.OrderStatus != "completed"
                                && (x.IsWorkflow4 ?? false) == false);
            return quote != null;
        }

        private bool IsQuoteExists(long quoteNo, int? revision)
        {
            var quote = _dbContext.Quotes.FirstOrDefault(x => x.QuoteNumber == quoteNo && x.Revision == revision);
            return quote != null;
        }

        private bool IsOrderExists(long orderNo)
        {
            var order = _dbContext.Quotes.FirstOrDefault(x => x.OrderNumber == orderNo);
            return order != null;
        }

        private void SaveQuote(PaperLess.Quote paperlessQuote, long hubspotContactId, long hubspotDealId, long orderNo)
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
                OrderNumber = orderNo,
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
            StreamWriter outputFile = new StreamWriter($"{JobPathHelper.BasePath}QTDLogs//workflow4ByOrder_{DateTime.Now:MMddyyyy}.txt", append: true);
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
