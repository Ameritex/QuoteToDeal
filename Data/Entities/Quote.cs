using System;
using System.ComponentModel.DataAnnotations;

namespace Quote_To_Deal.Data.Entities
{
    public record Quote
    {
        [Key]
        public int Id { get; set; }
        public long? QuoteNumber { get; set; }
        public string? Status { get; set; }
        public string? OrderStatus { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? SentDate { get; set; }
        public DateTime? ExpireDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? Phone{ get; set; }
        public bool? IsExpired{ get; set; }
        public double TotalPrice { get; set; }
        public long HsDealId { get; set; }
        public bool? IsWorkflow1 { get; set; }
        public bool? IsWorkflow2 { get; set; }
        public bool? IsWorkflow3 { get; set; }
        public bool? IsWorkflow4 { get; set; }
        public bool? IsWorkflow5 { get; set; }
        public bool? IsWorkflow6 { get; set; }
        public bool? IsWorkflow7 { get; set; }
        public bool? IsWorkflow8 { get; set; }
        public bool? IsWorkflow9 { get; set; }
        public bool? IsWorkflow10 { get; set; }
        public bool? IsWorkflow11 { get; set; }
        public int? Revision { get; set; }
        public DateTime? ShipOn { get; set; }
        public bool? IsWorkflowCancellation { get; set; }
        public long? OrderNumber { get; set; }
    }
}
