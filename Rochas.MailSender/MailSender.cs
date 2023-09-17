using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Rochas.Net.Connectivity.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Rochas.Net.Connectivity
{
    public class MailSender : IDisposable
    {
        #region Declarations

        private readonly string _defaultSender = string.Empty;
        private readonly SmtpClient? _mailClient = null;

        private readonly short _callRetries;
        private readonly int _retriesDelay;
        private readonly ResilienceManager _resilienceManager;

        #endregion

        #region Constructors

        public MailSender(string smtpHost, int smtpPort, bool useSSL,
                          string smtpUser, string smtpPwd, string defaultSender)
        {
            _defaultSender = defaultSender;
            
            if (!UseResilience)
                _mailClient = GetMailClient(smtpHost, smtpPort, useSSL, smtpUser, smtpPwd);
        }

        public MailSender(SmtpConfiguration smtpConfig) : this(smtpConfig.SmtpHost, smtpConfig.SmtpPort, smtpConfig.UseSSL,
                                                               smtpConfig.SmtpUser, smtpConfig.SmtpPwd, smtpConfig.DefaultSender)
        { }

        public MailSender(ILogger<MailSender> logger, SmtpConfiguration smtpConfig, short callRetries, int retriesDelay = 0) : this(smtpConfig)
        {
            _callRetries = callRetries;
            _retriesDelay = retriesDelay;
            _mailClient = GetMailClient(smtpConfig.SmtpHost, smtpConfig.SmtpPort, smtpConfig.UseSSL, 
                                        smtpConfig.SmtpUser, smtpConfig.SmtpPwd);
            _resilienceManager = new ResilienceManager(logger, _mailClient, smtpConfig);
            UseResilience = true;
        }

        #endregion

        #region Public Properties

        public bool UseResilience { get; set; }

        #endregion

        #region Public Methods

        public static bool CheckEmailSyntax(string email)
        {
            return Regex.IsMatch(email, @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase);
        }

        public async Task<MailSendResult> SendMessage(string destination, string subject, string body, string replyTo = "",
                                                      string companyName = "", bool htmlContent = false, Dictionary<byte[], string> attachs = null)
        {
            var message = ComposeMailMessage(destination, subject, body, replyTo, companyName, htmlContent, attachs);

            MailSendResult result = null;

            if (_mailClient != null)
            {
                try
                {
                    if (!UseResilience)
                    {
                        await _mailClient.SendMailAsync(message);
                        result = new MailSendResult(true, "OK");
                    }
                    else if (attachs == null)
                    {
                        var resilienceSet = new ResilienceSet(message, _callRetries, _retriesDelay);
                        return await _resilienceManager.TrySend(_mailClient, resilienceSet);
                    }
                    else
                        throw new ArgumentException("Use of mail attachments within resilience mode is not permitted.");
                }
                catch (Exception ex)
                {
                    result = new MailSendResult(false, ex.Message);
                }
            }

            return result;
        }

        public MailSendResult SendMessageSync(string destination, string subject, string body, string replyTo = "",
                                              string companyName = "", bool htmlContent = false, Dictionary<byte[], string> attachs = null)
        {
            return SendMessage(destination, subject, body, replyTo, companyName, htmlContent, attachs).Result;
        }

        #endregion

        #region Helper Methods

        private SmtpClient GetMailClient(string smtpHost, int smtpPort, bool useSSL, string smtpUser, string smtpPwd)
        {
            return new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPwd),
                EnableSsl = useSSL
            };
        }

        private MailMessage ComposeMailMessage(string destination, string subject, string body, string replyTo = "",
                                               string companyName = "", bool htmlContent = false, Dictionary<byte[], string> attachs = null)
        {
            MailMessage message;
            if (!(destination.Contains(",") || destination.Contains(";")))
            {
                var fromDisplayName = !string.IsNullOrEmpty(companyName) ? string.Format(companyName) : "Rochas PostMaster";
                var messageFrom = new MailAddress(_defaultSender, fromDisplayName);

                message = new MailMessage(_defaultSender, destination, subject, body)
                {
                    IsBodyHtml = htmlContent,
                    From = messageFrom
                };

                if (!string.IsNullOrEmpty(replyTo))
                    message.ReplyToList?.Add(new MailAddress(replyTo));
            }
            else
                message = ComposMultiDestinationMessage(destination, subject, body, htmlContent);

            if ((attachs != null) && attachs.Any())
                foreach (var atc in attachs)
                    message.Attachments?.Add(
                        new Attachment(
                            new MemoryStream(atc.Key), atc.Value
                            )
                        );

            return message;
        }

        private MailMessage ComposMultiDestinationMessage(string destination, string subject, string body, bool htmlContent)
        {
            var message = new MailMessage();

            message.From = new MailAddress(_defaultSender);
            message.Subject = subject;
            message.Body = body;

            message.IsBodyHtml = htmlContent;

            char destinBreakToken;
            if (destination.Contains(","))
                destinBreakToken = ',';
            else
                destinBreakToken = ';';

            var destinations = destination.Split(destinBreakToken);

            foreach (var destin in destinations)
                message.To.Add(new MailAddress(destin));

            return message;
        }

        public void Dispose()
        {
            GC.ReRegisterForFinalize(this);
        }

        #endregion
    }
}
