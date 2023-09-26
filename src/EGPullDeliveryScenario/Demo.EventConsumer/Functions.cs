using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Azure.Messaging.EventGrid.Namespaces;
using Azure;
using Azure.Messaging;
using System.Collections.Generic;

namespace Demo.EventConsumer
{
    public class Functions
    {
        [FunctionName("EventGridPullScheduleTrigger")]
        public static async Task EventGridPullScheduleTrigger([TimerTrigger("*/20 * * * * *")] TimerInfo myTimer,
             [Blob("eventfiles", Connection = "BLOB_CONNSTR")] BlobContainerClient blobContainerClient,
             ILogger log)
        {
            log.LogInformation($"EventGridPullScheduleTrigger Triggered at {DateTime.Now}");

            string topicEndpoint = Environment.GetEnvironmentVariable("AEG_TOPIC_ENDPOINT");
            string topicKey = Environment.GetEnvironmentVariable("AEG_TOPIC_KEY");
            string topicName = Environment.GetEnvironmentVariable("AEG_TOPIC_NAME");
            string subscription = Environment.GetEnvironmentVariable("AEG_TOPIC_SUBSCRIPTION");

            // Construct the client using an Endpoint for a namespace as well as the access key
            var client = new EventGridClient(new Uri(topicEndpoint), new AzureKeyCredential(topicKey));

            // Receive the published CloudEvents
            log.LogInformation($"Pulling events (max 2 per call)....");
            var resultRes = await client.ReceiveCloudEventsAsync(topicName, subscription, 2, TimeSpan.FromSeconds(20)); //receive 2 events at a time
            ReceiveResult result = resultRes.Value;

            log.LogInformation($"Received Response. Total events {result.Value.Count}...");

            // Iterate through the results and collect the lock tokens for events we want to release/acknowledge/result
            log.LogInformation($"Looping through events.");

            foreach (ReceiveDetails detail in result.Value)
            {
                CloudEvent @event = detail.Event;
                BrokerProperties brokerProperties = detail.BrokerProperties;
                log.LogInformation($"Event Details: {@event.Data.ToString()}");

                // The lock token is used to acknowledge, reject or release the event
                //Console.WriteLine(brokerProperties.LockToken);

                //deserialize event data
                var info = JsonConvert.DeserializeObject<EventInfo>(@event.Data.ToString());

                var blob = blobContainerClient.GetBlobClient(info.FileName);

                if (blob.Exists())
                {
                    var blobResult = await blobContainerClient.GetBlobClient(info.FileName).DownloadContentAsync();

                    var transactionInfo = JsonConvert.DeserializeObject<TransactionInfo>(blobResult.Value.Content.ToString());

                    log.LogInformation($"File Contents:\n ClientId: {transactionInfo.ClientId}" +
                        $"\nTransactionId: {transactionInfo.TransactionId}" +
                        $"\nAmount: {transactionInfo.Amount}" +
                        $"\nTransactionDateTime: {transactionInfo.TransactionDateTime}");

                    //acknowledge the event
                    AcknowledgeResult acknowledgeResult = await client.AcknowledgeCloudEventsAsync(topicName, subscription, new string[] { brokerProperties.LockToken });

                    // Inspect the Acknowledge result
                    if (acknowledgeResult.SucceededLockTokens.Count > 0)
                    {
                        log.LogInformation($"Successfully acknowledged");
                    }
                    else
                    {
                        log.LogError($"Failed to acknowledge: {acknowledgeResult.FailedLockTokens.Count}");
                        foreach (FailedLockToken failedLockToken in acknowledgeResult.FailedLockTokens)
                        {
                            log.LogError($"Lock Token: {failedLockToken.LockToken}");
                            log.LogError($"Error Code: {failedLockToken.ErrorCode}");
                            log.LogError($"Error Description: {failedLockToken.ErrorDescription}");
                        }
                    }
                }
                else
                {
                    log.LogError($"Payload: {info.FileName} not found. Rejecting event.");

                    //reject event
                    RejectResult rejectResult = await client.RejectCloudEventsAsync(topicName, subscription, new string[] { brokerProperties.LockToken });

                    // Inspect the Reject result
                    if (rejectResult.SucceededLockTokens.Count > 0)
                    {
                        log.LogError($"Successfully rejected");
                    }
                    else
                    {
                        log.LogError($"Failed to reject: {rejectResult.FailedLockTokens.Count}");
                        foreach (FailedLockToken failedLockToken in rejectResult.FailedLockTokens)
                        {
                            log.LogError($"Lock Token: {failedLockToken.LockToken}");
                            log.LogError($"Error Code: {failedLockToken.ErrorCode}");
                            log.LogError($"Error Description: {failedLockToken.ErrorDescription}");
                        }
                    }
                }
            }

            log.LogInformation($"Pulling events - Complete....");
        }
    }
}
