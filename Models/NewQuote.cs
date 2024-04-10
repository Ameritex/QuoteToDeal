using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Quote_To_Deal.Models
{
    public record NewQuote
    {
        //[JsonPropertyName("quote")]   
        public long quote { get; set; }

        //[JsonPropertyName("revision")]   
        public int? revision { get; set; }
    }
}
