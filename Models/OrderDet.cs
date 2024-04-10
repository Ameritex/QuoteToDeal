using System;
using System.ComponentModel.DataAnnotations;

namespace Quote_To_Deal.Models
{
    
    public class OrderDet
    {
        [Key]
        public int OrderDet_ID { get; set; }

        public string? OrderNo { get; set; }
        public string? JobNo { get; set; }

        public string? PartNo { get; set; }
        public string? PartDesc { get; set; }
        public string Status { get; set; }
        public DateTime? DueDate { get; set; }
        public int QtyToMake { get; set; }
        public bool Processed { get; set; }
        public string MasterJobNo { get; set; }
        public string ProdCode { get; set; }
        public string WorkCode { get; set; }
        public string Revision { get; set; }

        public int QtyShipped2Cust { get; set; }
        public double? UnitPrice { get; set; }
        public string User_Text1 { get; set; }
        public string User_Text2 { get; set; }

        public int? QtyOrdered { get; set; }
        public int? QtyCanceled { get; set; }

        public string JobNotes { get; set; }

        public DateTime? DateFinished { get; set; }


    }
}
