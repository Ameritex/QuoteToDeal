using System.ComponentModel.DataAnnotations;

namespace Quote_To_Deal.Data.Entities
{
    public record QuoteSalesPerson
    {
        [Key]
        public int Id { get; set; }
        public int SalesPersonId { get; set; }
        public int QuoteId { get; set; }
    }
}
