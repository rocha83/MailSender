using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Rochas.Net.Connectivity.ValueObjects;
using System.Net.Mail;

namespace Rochas.Net.Connectivity
{
    internal class ResilienceManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly SmtpConfiguration _smtpConfig;
        private static readonly ConcurrentQueue<ResilienceSet> _callQueue = new ConcurrentQueue<ResilienceSet>();

        public ResilienceManager(ILogger logger, SmtpClient mailClient, SmtpConfiguration smtpConfig)
        {
            _logger = logger;
            _smtpConfig = smtpConfig;

            var resilienceThread = new Thread(async () => await ListenResilienceQueue(mailClient));
            resilienceThread.Start();
        }

        private async Task ListenResilienceQueue(SmtpClient mailClient)
        {
            _logger.LogInformation($"Monitoring resiliency queue...");

            while (true)
            {
                if (!_callQueue.IsEmpty)
                {
                    if (_callQueue.TryPeek(out var queueItem))
                    {
                        if (queueItem.CallRetries > 0)
                        {
                            _logger.LogInformation($"Trying to send message to {queueItem.Message}...");

                            queueItem.CallRetries--;
                            var result = await TrySend(mailClient, queueItem);
                            if (result.Success)
                                queueItem.CallRetries = 0;
                        }
                        else
                            _callQueue.TryDequeue(out _);
                    }
                }
            }
        }

        public async Task<MailSendResult> TrySend(SmtpClient mailClient, ResilienceSet resilienceSet)
        {
            MailSendResult result = null;

            try
            {
                await mailClient.SendMailAsync(resilienceSet.Message);
                result = new MailSendResult(true, "OK");
                
                _logger.LogInformation($"Message delivered to {resilienceSet.Message.To} successfully.");
            }
            catch (Exception ex)
            {
                result = new MailSendResult(false, ex.Message);

                resilienceSet.LastError = ex.Message;
                if (resilienceSet.FirstCall)
                {
                    _logger.LogWarning($"Problem sending message to {resilienceSet.Message.To}.");
                    SendToResilience(resilienceSet);
                }
                else if (resilienceSet.CallRetries == 0)
                    _logger.LogError($"Error sending message to {resilienceSet.Message.To}: {Environment.NewLine} {ex.Message}");
            }

            if (resilienceSet.RetriesDelay > 0)
                Thread.Sleep(resilienceSet.RetriesDelay);

            return result;
        }
        private void SendToResilience(ResilienceSet resilienceSet)
        {
            _logger.LogInformation($"Sending message for {resilienceSet.Message.To} to resilience queue...");

            resilienceSet.FirstCall = false;
            _callQueue.Enqueue(resilienceSet);
        }

        public void Dispose()
        {
            GC.ReRegisterForFinalize(this);
        }
    }
}