using System;
using System.Collections.Generic;
using System.Text;

namespace Quote_To_Deal.Models
{
    public class EmailSetting
    {
        public string UserEmail { get; set; }

        public string Password { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
    }
}
