using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
