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
