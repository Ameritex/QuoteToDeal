using Quote_To_Deal.Models;
using Quote_To_Deal.PaperLess;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text.Encodings.Web;

namespace Quote_To_Deal.Common
{
    public class Utils
    {
        private static bool _isTestEmail;
        private static bool _isDevOnly;
        private static string _surveyLink;
        private static EmailSetting _emailSetting;
        private static List<SalespersonEmailCreds> _salesPersonEmailCreds;

        public Utils(bool isTestEmail, bool isDevOnly, string surveyLink, List<SalespersonEmailCreds> salesPersonEmailCreds)
        {
            _isTestEmail = isTestEmail;
            _isDevOnly = isDevOnly;
            _salesPersonEmailCreds = salesPersonEmailCreds;
            _surveyLink = surveyLink;
        }

        public static void SendEmail(EmailSetting setting, List<PaperLess.Quote> quotes, string templateName,
            IReadOnlyList<string> toEmails, string dated = "", string customerId = "", string quoteId = "", string salesPersonName = "")
        {
            if (!toEmails.Any())
            {
                return;
            }
            try
            {
                _emailSetting = setting;
                String userEmail = setting.UserEmail;
                String password = setting.Password;
                MailMessage msg = new MailMessage();
                if (_isTestEmail)
                {
                    msg.To.Add(new MailAddress("zco@ameritexllc.com"));
                    if (!_isDevOnly)
                    {
                        msg.To.Add(new MailAddress("damaya@ameritexllc.com"));
                    }
                }
                else
                {
                    foreach (string email in toEmails)
                    {
                        msg.To.Add(new MailAddress(email));
                    }
                }

                var emailCreds = GetSalesPersonEmailCreds(salesPersonName);//salesPersonName

                msg.From = new MailAddress(emailCreds.Email);
                var (subject, body) = ReadTemplateContent(templateName, quotes, dated, customerId, quoteId, salesPersonName);
                msg.Subject = subject;
                msg.Body = body;
                msg.IsBodyHtml = true;


                using (SmtpClient client = new SmtpClient
                {
                    Host = setting.Host,
                    Credentials = new System.Net.NetworkCredential(emailCreds.Email, emailCreds.Password),
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
       
        public static (string, string) ReadTemplateContent(string templateName, List<PaperLess.Quote> quotes, 
            string dated, string customerId, string quoteId, string salesPersonName)
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
                            //emailContent = emailContent.Replace("{customerName}", $"{quotes.FirstOrDefault().customer.first_name} {quotes.FirstOrDefault().customer.last_name}")
                            emailContent = emailContent.Replace("{customerFirstName}", quotes.FirstOrDefault()?.customer.first_name ?? "")
                                //.Replace("{quoteNumber}", quotes.FirstOrDefault()?.number?.ToString() ?? "")
                                .Replace("{unsubscribeLink}", $"{_emailSetting.EmailSetupBaseUrl}{_emailSetting.UnsubscribeEndpoint}")
                                .Replace("{emailSetupLink}", $"{_emailSetting.EmailSetupBaseUrl}{_emailSetting.SetupEndpoint}")
                                .Replace("{encryptedEmail}", GetEncrypted(quotes.FirstOrDefault()?.customer.email ?? ""))
                                .Replace("{salesPersonName}"
                                , $"{quotes.FirstOrDefault()?.salesperson.first_name ?? ""} {quotes.FirstOrDefault()?.salesperson.last_name ?? ""}");
                            emailSubject = $"Review Request - Ameritex Quote {quotes.FirstOrDefault()?.number} Awaiting Your Attention";
                            break;
                        }
                    case "Workflow1Once":
                        {
                            var quoteNumbers = quotes.Select(x => x.number);
                            var quoteNumberText = string.Join(",", quoteNumbers);
                            if (quoteNumberText.Contains(",") && quoteNumbers.Count() > 2)
                            {
                                quoteNumberText = quoteNumberText.Insert(quoteNumberText.LastIndexOf(",") + 1, " and ");
                            }

                            //emailContent = emailContent.Replace("{customerName}", $"{quotes.FirstOrDefault().customer.first_name} {quotes.FirstOrDefault().customer.last_name}")
                            emailContent = emailContent.Replace("{customerFirstName}", quotes.FirstOrDefault()?.customer.first_name ?? "")
                                //.Replace("{quoteNumber}", quoteNumberText ?? "")
                                .Replace("{unsubscribeLink}", $"{_emailSetting.EmailSetupBaseUrl}{_emailSetting.UnsubscribeEndpoint}")
                                .Replace("{emailSetupLink}", $"{_emailSetting.EmailSetupBaseUrl}{_emailSetting.SetupEndpoint}")
                                .Replace("{encryptedEmail}", GetEncrypted(quotes.FirstOrDefault()?.customer.email ?? ""))
                                .Replace("{salesPersonName}", 
                                $"{quotes.FirstOrDefault()?.salesperson.first_name ?? ""} {quotes.FirstOrDefault()?.salesperson.last_name ?? ""}");
                            emailSubject = $"Review Request - Ameritex Quote {quoteNumberText} Awaiting Your Attention";
                            break;
                        }
                    case "Workflow2":
                        {
                            var maxleadTime = GetMaxLeadTime(quotes.FirstOrDefault()?.quote_items);

                            emailContent = emailContent.Replace("{customerName}", $"{quotes.FirstOrDefault()?.customer.first_name ?? ""} {quotes.FirstOrDefault()?.customer.last_name ?? ""}")
                                .Replace("{quoteNumber}", quotes.FirstOrDefault()?.number?.ToString() ?? "")
                                .Replace("{quoteETA}", DateTime.Now.AddDays(maxleadTime).ToShortDateString());
                            emailSubject = $"Commencing Fabrication - Confirmation of Quote {quotes.FirstOrDefault()?.number ?? 0}";
                            break;
                        }
                    case "Workflow4":
                        {
                            emailContent = emailContent.Replace("{customerName}",
                                $"{quotes.FirstOrDefault()?.customer.first_name ?? ""} {quotes.FirstOrDefault()?.customer.last_name ?? ""}")
                                .Replace("{quoteNumber}", quotes.FirstOrDefault()?.number?.ToString() ?? "")
                                .Replace("{surveyLink}", _surveyLink)
                                .Replace("{shipDate}", dated);
                            emailSubject = $"Great News! Ameritex Quote {quotes.FirstOrDefault()?.number} is ready to ship.";
                            break;
                        }
                    case "Workflow5":
                        {
                            emailContent = emailContent.Replace("{salesPersonName}", 
                                $"{quotes.FirstOrDefault()?.salesperson.first_name ?? ""}   {quotes.FirstOrDefault()?.salesperson.last_name ?? ""}")
                                .Replace("{customerEmail}", quotes.FirstOrDefault()?.customer?.email ?? "No customer email")
                                .Replace("{quoteNumber}", quotes.FirstOrDefault()?.number?.ToString() ?? "")
                                .Replace("{expiredDate}", quotes.FirstOrDefault()?.expired_date.GetValueOrDefault().ToString("G"));
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
            long quoteNumber, string templateName, IReadOnlyList<string> toEmails, string salesPersonName = "")
        {
            if (!toEmails.Any())
            {
                return;
            }
            try
            {
                _emailSetting = setting;
                String userEmail = setting.UserEmail;
                String password = setting.Password;
                MailMessage msg = new MailMessage();
               
                if (_isTestEmail)
                {
                    msg.To.Add(new MailAddress("zco@ameritexllc.com"));
                    //msg.To.Add(new MailAddress("zcoouhatafe@gmail.com"));
                    if (!_isDevOnly)
                    {
                        msg.To.Add(new MailAddress("damaya@ameritexllc.com"));
                    }
                }
                else
                {
                    foreach (string email in toEmails)
                    {
                        msg.To.Add(new MailAddress(email));
                    }
                }

                var emailCreds = GetSalesPersonEmailCreds(salesPersonName);

                msg.From = new MailAddress(emailCreds.Email);
                var (subject, body) = ReadTemplateContent(templateName, quoteNumber, emailPerson, infoPerson, dated);
                msg.Subject = subject;
                msg.Body = body;
                msg.IsBodyHtml = true;

                using (SmtpClient client = new SmtpClient
                {
                    Host = setting.Host,
                    Credentials = new System.Net.NetworkCredential(emailCreds.Email, emailCreds.Password),
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
                                .Replace("{customerFirstName}", emailPerson.FirstName)
                                //.Replace("{customerName}", $"{emailPerson.FirstName} {emailPerson.LastName}")
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

        private static SalespersonEmailCreds GetSalesPersonEmailCreds(string salesPersonName)
        {
            var emailCreds = _salesPersonEmailCreds.FirstOrDefault(x => x.Name.Equals(salesPersonName, StringComparison.OrdinalIgnoreCase));
            return emailCreds ?? new SalespersonEmailCreds
                                        {
                                            Email = _emailSetting.UserEmail,
                                            Password = _emailSetting.Password,
                                        };
        }

        private static int GetMaxLeadTime(IReadOnlyList<QuoteItem>? quoteItems)
        {
            int maxLeadTime = 0;
            if (quoteItems != null)
            {
                var leadDays = new List<int>();
                foreach (var quoteItem in quoteItems)
                {
                    foreach (var component in quoteItem.components)
                    {
                        leadDays.Add(component.quantities.LastOrDefault()?.lead_time ?? 0);
                    }
                }
                maxLeadTime = leadDays.Max();
            }
            return maxLeadTime;
        }
        
        private static string GetEncrypted(string value)
        {
            var encVal = EncryptDecrypt.Encrypt(value);
            
            return Uri.EscapeDataString(encVal);
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
