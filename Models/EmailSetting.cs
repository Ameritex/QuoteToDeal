namespace Quote_To_Deal.Models
{
    public class EmailSetting
    {
        public string UserEmail { get; set; }
        public string Password { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
        public string EmailSetupBaseUrl { get; set; }
        public string SetupEndpoint { get; set; }
        public string UnsubscribeEndpoint { get; set; }
    }
}
