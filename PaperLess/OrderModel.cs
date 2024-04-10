// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
using Quote_To_Deal.PaperLess.Contract;
using System;
using System.Collections.Generic;

namespace Quote_To_Deal.PaperLess
{
    public class BillingInfo
    {
        public long id { get; set; }
        public string attention { get; set; }
        public string address1 { get; set; }
        public string address2 { get; set; }
        public string business_name { get; set; }
        public string city { get; set; }
        public string country { get; set; }
        public string facility_name { get; set; }
        public object phone { get; set; }
        public object phone_ext { get; set; }
        public string postal_code { get; set; }
        public string state { get; set; }
    }


    public class Account
    {
        public string erp_code { get; set; }
        public long id { get; set; }
        public object notes { get; set; }
        public string name { get; set; }
        public string payment_terms { get; set; }
        public long? payment_terms_period { get; set; }
    }

    public class Contact
    {
        public Account? account { get; set; }
        public string email { get; set; }
        public string first_name { get; set; }
        public long id { get; set; }
        public string last_name { get; set; }
        public string notes { get; set; }
        public string phone { get; set; }
        public string phone_ext { get; set; }
    }

    public class ORderCompany
    {
        public string business_name { get; set; }
        public string erp_code { get; set; }
        public object id { get; set; }
        public object notes { get; set; }
        public string phone { get; set; }
        public string phone_ext { get; set; }
    }

    public class ORderCustomer
    {
        public object id { get; set; }
        public Company? company { get; set; }
        public string email { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string notes { get; set; }
        public string phone { get; set; }
        public string phone_ext { get; set; }
    }

    public class SupportingFile : ISupportingFile
    {
        public string Filename { get; set; }
        public string Url { get; set; }
    }

    public class ProductCode
    {
        public string name { get; set; }
        public string type { get; set; }
        public string value { get; set; }
    }

    public class ORderComponent
    {
        public long id { get; set; }
        public List<object> child_ids { get; set; }
        public List<object> children { get; set; }
        public string description { get; set; }
        public bool export_controlled { get; set; }
        public List<string> finishes { get; set; }
        public long? innate_quantity { get; set; }
        public bool is_root_component { get; set; }

        public List<object> parent_ids { get; set; }

        public List<ProductCode> part_custom_attrs { get; set; }

        public string part_name { get; set; }
        public string part_number { get; set; }
        public string part_url { get; set; }
        public string part_uuid { get; set; }

        public object purchased_component { get; set; }
        public string revision { get; set; }
        public List<SupportingFile> supporting_files { get; set; }
        public string thumbnail_url { get; set; }
        public string type { get; set; }
        public long? deliver_quantity { get; set; }
        public long? make_quantity { get; set; }
    }

    public class OrderOrderItem
    {
        public long id { get; set; }
        public List<Component> components { get; set; }
        public string description { get; set; }
        public object expedite_revenue { get; set; }
        public bool export_controlled { get; set; }
        public string filename { get; set; }
        public long? lead_days { get; set; }
        public string markup_1_price { get; set; }
        public string markup_1_name { get; set; }
        public string markup_2_price { get; set; }
        public string markup_2_name { get; set; }
        public object private_notes { get; set; }
        public string public_notes { get; set; }
        public int? quantity { get; set; }
        public int? quantity_outstanding { get; set; }
        public long? quote_item_id { get; set; }
        public string quote_item_type { get; set; }
        public long? root_component_id { get; set; }
        public string ships_on { get; set; }
        public string total_price { get; set; }
        public string unit_price { get; set; }
        public string base_price { get; set; }
        public object add_on_fees { get; set; }
        public List<object> ordered_add_ons { get; set; }
    }

    public class PaymentDetails
    {
        public object card_brand { get; set; }
        public object card_last4 { get; set; }
        public string net_payout { get; set; }
        public string payment_type { get; set; }
        public string purchase_order_number { get; set; }
        public object purchasing_dept_contact_email { get; set; }
        public object purchasing_dept_contact_name { get; set; }
        public string shipping_cost { get; set; }
        public string subtotal { get; set; }
        public string tax_cost { get; set; }
        public string tax_rate { get; set; }
        public string payment_terms { get; set; }
        public string total_price { get; set; }
    }

    public class OrderSalesPerson
    {
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string email { get; set; }
    }
    public class OrderModel
    {
        public BillingInfo billing_info { get; set; }
        public DateTime? created { get; set; }
        public DateTime? ships_on { get; set; }
        public Contact? contact { get; set; }
        public Customer customer { get; set; }
        public OrderSalesPerson sales_person { get; set; }
        public object deliver_by { get; set; }
        public object erp_code { get; set; }
        public long? number { get; set; }
        public List<OrderOrderItem> order_items { get; set; }
        public PaymentDetails payment_details { get; set; }
        public string private_notes { get; set; }
        public object quote_erp_code { get; set; }
        public long? quote_number { get; set; }
        public int? quote_revision_number { get; set; }
        public string status { get; set; }
        public string purchase_order_file_url { get; set; }
    }

}