// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
using Quote_To_Deal.PaperLess.Contract;
using System;
using System.Collections.Generic;

namespace Quote_To_Deal.PaperLess
{
    public class Company
    {
        public string business_name { get; set; }
        public string erp_code { get; set; }
    }
    public class SalesPerson
    {
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string email { get; set; }
    }

    public class Customer
    {
        public Company company { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public string phone_ext { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
    }

    public class Quantity
    {
        public int lead_time { get; set; }
        public double total_price { get; set; }
    }

    public class RootComponent
    {
        public IReadOnlyList<Component> components { get; set; }
    }

    public class Component
    {
        public IReadOnlyList<Quantity> quantities { get; set; }
    }

    public class QuoteItem
    {
        public IReadOnlyList<Component> components { get; set; }
        //public RootComponent root_component { get; set; }
    }

    public class Quote
    {
        public long? number { get; set; }
        public int? revision_number { get; set; }
        public string status { get; set; }
        public DateTime? sent_date { get; set; }
        public string first_name { get; set; }
        public string email { get; set; }
        public string last_name { get; set; }
        public string phone { get; set; }
        public bool expired { get; set; }
        public DateTime? expired_date { get; set; }
        public DateTime? created { get; set; }
        public IReadOnlyList<QuoteItem> quote_items { get; set; }
        public IReadOnlyList<long> order_numbers { get; set; }
        public SalesPerson salesperson { get; set; }
        public Customer customer { get; set; }

    }

}