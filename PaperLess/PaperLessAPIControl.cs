using Newtonsoft.Json;
using Quote_To_Deal.Common;
using Quote_To_Deal.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Quote_To_Deal.PaperLess
{
    public class PaperLessAPIControl
    {
        private static string API_URL = "https://api.paperlessparts.com";
        public static string API_KEY = "";
        public static List<NewQuote> GetNewQuotes(long lastQuote, int? revision)
        {
            var revisionQuery = revision.HasValue ? $"&revision={revision}" : "";
            var result = ApiHelper.Get<List<NewQuote>>(API_URL, $"/quotes/public/new?last_quote={lastQuote}{revisionQuery}", API_KEY);
            return result.Result;
        }

        public static OrderModel GetOrderInformation(long orderId)
        {
            try
            {
                var result = ApiHelper.Get<OrderModel>(API_URL, $"/orders/public/{orderId}", API_KEY);
                return result.Result;
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        public static Quote GetQuoteInformation(long quoteNumber, int? revision)
        {
            try
            {
                var revisionQuery = revision != null ? $"?revision={revision}" : "";
                var result = ApiHelper.Get<Quote>(API_URL, $"/quotes/public/{quoteNumber}{revisionQuery}", API_KEY);
                return result.Result;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static List<long> GetNewOrders(long lastOrder)
        {
            var result = ApiHelper.Get<List<long>>(API_URL, $"/orders/public/new?last_order={lastOrder}", API_KEY);
            return result.Result;
        }
    }
}
