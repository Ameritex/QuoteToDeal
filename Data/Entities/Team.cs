using System.ComponentModel.DataAnnotations;

namespace Quote_To_Deal.Data.Entities
{
    public record Team
    {
        [Key]
        public int Id { get; set; }
        public string? TeamName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Email { get; set; }
        public bool? IsActive { get; set; }
    }
}
