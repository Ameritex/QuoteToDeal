using System.ComponentModel.DataAnnotations;

namespace Quote_To_Deal.Data.Entities
{
    public record CustomerCompany
    {
        [Key]
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int CompanyId { get; set; }
    }
}
