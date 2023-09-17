using System;
using System.Collections.Generic;
using System.Text;

namespace Rochas.Net.Connectivity.ValueObjects
{
    public class MailSendResult
    {
        public MailSendResult(bool sucess, string errorMessage) { 
            
        }

        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}
