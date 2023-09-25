using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using static TransactionEvent.TransactionEvents;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.Namespaces;
using Azure;
using Azure.Messaging;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.VisualBasic;

namespace Demo.Consumer
{
    public class Functions
    {
        [FunctionName("EventOrchestrator")]
        public async Task EventOrchestrator([TimerTrigger("*/10 * * * * *")] TimerInfo myTimer,
            [Blob("eventfiles", Connection = "BLOB_CONNSTR")] BlobContainerClient blobContainerClient,
            ILogger log)
        {
            log.LogInformation($"Executing function to fetch events at: {DateTime.Now}");


            string serviceApiEndPoint = Environment.GetEnvironmentVariable("SERVICE_API_ENDPOINT");
            string topicEndpoint = Environment.GetEnvironmentVariable("AEG_TOPIC_ENDPOINT");
            string topicKey = Environment.GetEnvironmentVariable("AEG_TOPIC_KEY");
            string topicName = Environment.GetEnvironmentVariable("AEG_TOPIC_NAME");

            using var channel = GrpcChannel.ForAddress(serviceApiEndPoint);
            var client = new TransactionEventsClient(channel);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));


            //create blob container if does not exists
            await blobContainerClient.CreateIfNotExistsAsync();

            //create evetn grid client
            EventGridClient egClient = new EventGridClient(new Uri(topicEndpoint), new AzureKeyCredential(topicKey));

            //create event request setting
            TransactionEvent.EventRequestSetting requestSetting = new TransactionEvent.EventRequestSetting
            {
                MaxEvents = 2
            };

            log.LogInformation($"Fetching max {requestSetting.MaxEvents} events from service endpoint");

            using var streamingCall = client.GetTransactionStream(requestSetting, cancellationToken: cts.Token);

            try
            {
                await foreach (var transactionEventData in streamingCall.ResponseStream.ReadAllAsync(cancellationToken: cts.Token))
                {
                    log.LogInformation($"Tran Datetime: {transactionEventData.TransactionDateTime.ToDateTime():s} | Event Source: {transactionEventData.EventSource}");

                    string jsonPayload = JsonConvert.SerializeObject(transactionEventData);

                    string filename = $"event-{transactionEventData.Transactionid}.txt";
                    var blobClient = blobContainerClient.GetBlobClient(filename);

                    await blobClient.UploadAsync(BinaryData.FromString(jsonPayload), overwrite: true);

                    //publish event in event grid
                    EventInfo info = new EventInfo();

                    info.FileName = filename;
                    info.TransactionId = transactionEventData.Transactionid;

                    //CloudEvent cloudEvent = new CloudEvent("Demo.BusinessEvent", "Business.Event", info);
                    CloudEvent cloudEvent = new CloudEvent(transactionEventData.EventSource, "Business.Event", info);
                    await egClient.PublishCloudEventAsync(topicName, cloudEvent);
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                log.LogError("Stream cancelled.");
            }

            log.LogInformation($"Fetching events complete.");
        }

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
            log.LogInformation($"Pulling events....");
            var resultRes = await client.ReceiveCloudEventsAsync(topicName, subscription, 2, TimeSpan.FromSeconds(20)); //receive 2 events at a time
            ReceiveResult result = resultRes.Value;

            Console.WriteLine("Received Response");

            // handle received messages. Define these variables on the top.

            var toAcknowledge = new List<string>();

            // Iterate through the results and collect the lock tokens for events we want to release/acknowledge/result

            foreach (ReceiveDetails detail in result.Value)
            {
                CloudEvent @event = detail.Event;
                BrokerProperties brokerProperties = detail.BrokerProperties;
                log.LogInformation($"Event Details: {@event.Data.ToString()}");

                // The lock token is used to acknowledge, reject or release the event
                Console.WriteLine(brokerProperties.LockToken);

                //acknowledge the event
                AcknowledgeResult acknowledgeResult = await client.AcknowledgeCloudEventsAsync(topicName, subscription, new string[] { brokerProperties.LockToken });

                // Inspect the Acknowledge result
                if (acknowledgeResult.SucceededLockTokens.Count > 0)
                {
                    log.LogInformation($"Success count for Acknowledge: {acknowledgeResult.SucceededLockTokens.Count}");
                    foreach (string lockToken in acknowledgeResult.SucceededLockTokens)
                    {
                        log.LogInformation($"Lock Token: {lockToken}");
                    }

                }
                else
                {
                    log.LogInformation($"Failed count for Acknowledge: {acknowledgeResult.FailedLockTokens.Count}");
                    foreach (FailedLockToken failedLockToken in acknowledgeResult.FailedLockTokens)
                    {
                        log.LogInformation($"Lock Token: {failedLockToken.LockToken}");
                        log.LogInformation($"Error Code: {failedLockToken.ErrorCode}");
                        log.LogInformation($"Error Description: {failedLockToken.ErrorDescription}");
                    }
                }


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
                }
                else
                {
                    log.LogInformation($"Payload: {info.FileName} not found.");
                }

            }

            log.LogInformation($"Pulling events - Complete....");
        }
    }
}
