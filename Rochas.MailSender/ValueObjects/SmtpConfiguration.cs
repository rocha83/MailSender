using System;

namespace Rochas.Net.Connectivity.ValueObjects
{
    public class SmtpConfiguration
    {
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public bool UseSSL { get; set; }
        public string SmtpUser { get; set; }
        public string SmtpPwd { get; set; }
        public string DefaultSender { get; set; }
    }
}
