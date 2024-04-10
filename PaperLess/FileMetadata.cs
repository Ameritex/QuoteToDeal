using Quote_To_Deal.Models;
using System;
using System.Collections.Generic;

namespace Quote_To_Deal.PaperLess
{
    public class FileMetadata
    {
        public string CustDesc { get; set; }
        public string CustContact { get; set; }
        public string CustContactLastName { get; set; }
        public DateTime CreatedDate { get; set; }  
        public string Descriptions { get; set; }
        public List<OrderDet> OrderDets { get; set; }

        public string QuoteNo { get; set; }
        public string SalesPerson { get; set; }
    }
}
