using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
