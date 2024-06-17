using System;
using System.ComponentModel.DataAnnotations;

namespace Quote_To_Deal.Data.Entities
{
    public record OnceADayEmail
    {
        [Key]
        public int Id { get; set; }
        public int? RevisionNumber { get; set; }
        public string Email { get; set; }
        public long QuoteNumber { get; set; }
        public bool? IsSend { get; set;}
        public DateTime? Dated { get; set; }
        public DateTime? SentDate { get; set; }
    }
}
