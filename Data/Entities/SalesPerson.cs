using System.ComponentModel.DataAnnotations;

namespace Quote_To_Deal.Data.Entities
{
    public record SalesPerson : IEmail
    {
        [Key]
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; } = string.Empty;
    }
}
