using System.ComponentModel.DataAnnotations;

namespace Quote_To_Deal.Data.Entities
{
    public record QuoteCustomer
    {
        [Key]
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int QuoteId { get; set; }
    }
}
