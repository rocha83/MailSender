using System;
using System.Net.Mail;

namespace Rochas.Net.Connectivity
{
    internal class ResilienceSet
    {
        public ResilienceSet()
        {
        }
        public ResilienceSet(MailMessage message, short retries, int retriesDelay)
        {
            Message = message;
            CallRetries = retries;
            RetriesDelay = retriesDelay;

            FirstCall = true;
        }
        
        public MailMessage Message { get; set; }

        public short CallRetries { get; set; }

        public int RetriesDelay { get; set; }

        public string LastError { get; set; }

        public bool FirstCall { get; set; }
    }
}
