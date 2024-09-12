using System;
using System.Collections.Generic;
using System.Text;

namespace Rochas.Net.Connectivity.ValueObjects
{
    public class MailSendResult
    {
        public MailSendResult(bool sucess, string errorMessage) { 
            this.Success = sucess;
            this.ErrorMessage = errorMessage;
        }

        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}
