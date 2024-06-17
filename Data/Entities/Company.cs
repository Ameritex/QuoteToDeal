using System.ComponentModel.DataAnnotations;

namespace Quote_To_Deal.Data.Entities
{
    public record Company
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ErpName { get; set; }
    }
}
