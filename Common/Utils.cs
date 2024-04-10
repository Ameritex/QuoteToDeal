using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;
using Quartz.Impl.AdoJobStore;
using Quote_To_Deal.Data.Entities;
using Quote_To_Deal.Models;
using Quote_To_Deal.PaperLess;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Reflection;

namespace Quote_To_Deal.Common
{
    public class Utils
    {
        public static void SendEmail(EmailSetting setting, List<PaperLess.Quote> quotes, string templateName, 
            IReadOnlyList<string> toEmails, string dated = "", string customerId = "", string quoteId = "")
        {
            if (!toEmails.Any())
            {
                return;
            }
            try
            {
                String userEmail = setting.UserEmail;
                String password = setting.Password;
                MailMessage msg = new MailMessage();
                //foreach (string email in toEmails)
                //{
                //    msg.To.Add(new MailAddress(email));
                //}
                msg.To.Add(new MailAddress("zco@ameritexllc.com"));
                msg.To.Add(new MailAddress("damaya@ameritexllc.com"));
                msg.From = new MailAddress(userEmail);
                var (subject, body) = ReadTemplateContent(templateName, quotes, dated, customerId, quoteId);
                msg.Subject = subject;
                msg.Body = body;
                msg.IsBodyHtml = true;
                
                using (SmtpClient client = new SmtpClient
                {
                    Host = setting.Host,
                    Credentials = new System.Net.NetworkCredential(userEmail, password),
                    Port = setting.Port,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    EnableSsl = setting.EnableSsl,
                   
                    UseDefaultCredentials = false
                })
                {
                    client.Send(msg);
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
                throw ex;
                //Console.ReadKey();
            }
        }

        public static (string, string) ReadTemplateContent(string templateName, List<PaperLess.Quote> quotes, string dated, string customerId, string quoteId)
        {
            string emailContent = "";
            string emailSubject = "";
            //string headerImage = Path.Combine(Directory.GetCurrentDirectory(), "images/email-header.png");
            string emailPath = Path.Combine(Directory.GetCurrentDirectory(), $"EmailTemplates/{templateName}.htm");
            using (StreamReader SourceReader = System.IO.File.OpenText(emailPath))
            {
                emailContent = SourceReader.ReadToEnd();
                switch (templateName)
                {
                    case "Workflow1":
                        {
                            emailContent = emailContent.Replace("{customerName}", $"{quotes.FirstOrDefault().customer.first_name} {quotes.FirstOrDefault().customer.last_name}")
                                .Replace("{quoteNumber}", quotes.FirstOrDefault()?.number?.ToString() ?? "")
                                .Replace("{unsubscribeEmail}", GetEncrypted(quotes.FirstOrDefault()?.customer.email ?? ""));
                            emailSubject = $"Review Request - Ameritex Quote {quotes.FirstOrDefault()?.number} Awaiting Your Attention";
                            break;
                        }
                    case "Workflow1Once":
                        {
                            var quoteNumbers = string.Join(",", quotes.Select(x => x.number));
                            if (quoteNumbers.Contains(","))
                            { quoteNumbers = quoteNumbers.Insert(quoteNumbers.LastIndexOf(",") + 1, " and "); }
                            
                            emailContent = emailContent.Replace("{customerName}", $"{quotes.FirstOrDefault().customer.first_name} {quotes.FirstOrDefault().customer.last_name}")
                                .Replace("{quoteNumber}", quoteNumbers ?? "")
                                .Replace("{unsubscribeEmail}", GetEncrypted(quotes.FirstOrDefault()?.customer.email ?? ""));
                            emailSubject = $"Review Request - Ameritex Quote {quoteNumbers} Awaiting Your Attention";
                            break;
                        }
                    case "Workflow2":
                        {
                            var maxleadTime = GetMaxLeadTime(quotes.FirstOrDefault().quote_items);

                            emailContent = emailContent.Replace("{customerName}", $"{quotes.FirstOrDefault().customer.first_name} {quotes.FirstOrDefault().customer.last_name}")
                                .Replace("{quoteNumber}", quotes.FirstOrDefault().number?.ToString() ?? "")
                                .Replace("{quoteETA}", DateTime.Now.AddDays(maxleadTime).ToShortDateString());
                            emailSubject = $"Commencing Fabrication - Confirmation of Quote {quotes.FirstOrDefault().number}";
                            break;
                        }
                    case "Workflow4":
                        {
                            emailContent = emailContent.Replace("{customerName}", $"{quotes.FirstOrDefault().customer.first_name} {quotes.FirstOrDefault().customer.last_name}")
                                .Replace("{quoteNumber}", quotes.FirstOrDefault().number?.ToString() ?? "")
                                .Replace("{surveyLink}", $"{JobPathHelper.BaseUrl}/?quoteId={quoteId}&customerId={customerId}")
                                .Replace("{shipDate}", dated);
                            emailSubject = $"Great News! Ameritex Quote {quotes.FirstOrDefault().number} is ready to ship.";
                            break;
                        }
                    case "Workflow5":
                        {
                            emailContent = emailContent.Replace("{salesPersonName}", $"{quotes.FirstOrDefault().salesperson.first_name} {quotes.FirstOrDefault().salesperson.last_name}")
                                .Replace("{customerEmail}", quotes.FirstOrDefault().customer?.email ?? "No customer email")
                                .Replace("{quoteNumber}", quotes.FirstOrDefault().number?.ToString() ?? "")
                                .Replace("{expiredDate}", quotes.FirstOrDefault().expired_date.GetValueOrDefault().ToString("G"));
                            emailSubject = "Quote expired";
                            break;
                        }
                        //case "InitiateProject_ProdTeam":
                        //    {
                        //        var maxleadTime = GetMaxLeadTime(quote.quote_items);

                        //        emailContent = emailContent.Replace("{customerName}", $"{quote.customer.first_name} {quote.customer.last_name}")
                        //            .Replace("{quoteNumber}", quote.number?.ToString() ?? "")
                        //            .Replace("{quoteETA}", DateTime.Now.AddDays(maxleadTime).ToShortDateString());
                        //        emailSubject = $"Initiate the Project for Quote {quote.number}";
                        //        break;
                        //    }


                }

                
            }
            return (emailSubject, emailContent);
        }

        public static void SendEmail(EmailSetting setting, Data.Entities.IEmail emailPerson, Data.Entities.IEmail infoPerson,string dated,
            long quoteNumber, string templateName, IReadOnlyList<string> toEmails)
        {
            if (!toEmails.Any())
            {
                return;
            }
            try
            {
                String userEmail = setting.UserEmail;
                String password = setting.Password;
                MailMessage msg = new MailMessage();
                //foreach (string email in toEmails)
                //{
                //    msg.To.Add(new MailAddress(email));
                //}
                msg.To.Add(new MailAddress("zco@ameritexllc.com"));
                msg.To.Add(new MailAddress("damaya@ameritexllc.com"));
                msg.From = new MailAddress(userEmail);
                var (subject, body) = ReadTemplateContent(templateName, quoteNumber, emailPerson,infoPerson, dated);
                msg.Subject = subject;
                msg.Body = body;
                msg.IsBodyHtml = true;

                using (SmtpClient client = new SmtpClient
                {
                    Host = setting.Host,
                    Credentials = new System.Net.NetworkCredential(userEmail, password),
                    Port = setting.Port,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    EnableSsl = setting.EnableSsl,
                    UseDefaultCredentials = false
                })
                {
                    client.Send(msg);
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }
        }

        public static (string, string) ReadTemplateContent(string templateName, long quoteNumber, Data.Entities.IEmail emailPerson, Data.Entities.IEmail infoPerson, string dated)
        {
            string emailContent = "";
            string emailSubject = "";
            //string headerImage = Path.Combine(Directory.GetCurrentDirectory(), "images/email-header.png");
            string emailPath = Path.Combine(Directory.GetCurrentDirectory(), $"EmailTemplates/{templateName}.htm");
            using (StreamReader SourceReader = System.IO.File.OpenText(emailPath))
            {
                emailContent = SourceReader.ReadToEnd();
                switch (templateName)
                {
                    case "Workflow3_Customer":
                        {
                            emailContent = emailContent.Replace("{salesPersonName}", $"{infoPerson.FirstName} {infoPerson.LastName}")
                               .Replace("{salesPersonEmail}", infoPerson.Email)
                                .Replace("{customerName}", $"{emailPerson.FirstName} {emailPerson.LastName}")
                                .Replace("{quoteNumber}", quoteNumber.ToString())
                                .Replace("{quoteDate}", dated );
                            emailSubject = $"Accelerating Precision - Follow-up on Quote {quoteNumber}";
                            break;
                        }
                    case "Workflow3_Sales":
                        {
                            emailContent = emailContent.Replace("{salesPersonName}", $"{emailPerson.FirstName} {emailPerson.LastName}")
                               .Replace("{customerName}", $"{infoPerson.FirstName} {infoPerson.LastName}")
                               .Replace("{customerEmail}", infoPerson.Email)
                               .Replace("{customerPhone}", infoPerson.Phone)
                               .Replace("{quoteNumber}", quoteNumber.ToString())
                               .Replace("{quoteDate}", dated);
                            emailSubject = $"{quoteNumber} has has not been approved in 7 days. Please follow-up!";
                            break;
                        }
                }

            }
            return (emailSubject, emailContent);
        }


        private static int GetMaxLeadTime(IReadOnlyList<QuoteItem> quoteItems)
        {
            int maxLeadTime = 0;
            var leadDays = new List<int>();
            foreach (var quoteItem in quoteItems)
            {
                foreach (var component in quoteItem.components)
                {
                    leadDays.Add(component.quantities.LastOrDefault()?.lead_time ?? 0);
                }
            }
            maxLeadTime = leadDays.Max();
            return maxLeadTime;
        }

        private static string GetEncrypted(string value)
        {
            return EncryptDecrypt.Encrypt(value);
        }

        public static double GetBusinessDays(DateTime startD, DateTime endD)
        {
            double calcBusinessDays =
                1 + ((endD - startD).TotalDays * 5 -
                (startD.DayOfWeek - endD.DayOfWeek) * 2) / 7;

            if (endD.DayOfWeek == DayOfWeek.Saturday) calcBusinessDays--;
            if (startD.DayOfWeek == DayOfWeek.Sunday) calcBusinessDays--;

            return calcBusinessDays;
        }
    }
}
