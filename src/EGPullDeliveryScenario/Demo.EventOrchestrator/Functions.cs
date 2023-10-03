using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using static APSEvent.APSEvents;
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
using APSEvent;

namespace Demo.EventOrchestrator
{
    public class Functions
    {
        [FunctionName("EventOrchestrator")]
        public async Task EventOrchestrator([TimerTrigger("*/10 * * * * *")] TimerInfo myTimer,
            [Blob("%BLOB_CONTAINER_NAME%", Connection = "BLOB_CONNSTR")] BlobContainerClient blobContainerClient,
            ILogger log)
        {
            log.LogInformation($"Executing function to fetch events at: {DateTime.Now}");

            string serviceApiEndPoint = Environment.GetEnvironmentVariable("SERVICE_API_ENDPOINT");
            string topicEndpoint = Environment.GetEnvironmentVariable("AEG_TOPIC_ENDPOINT");
            string topicKey = Environment.GetEnvironmentVariable("AEG_TOPIC_KEY");
            string topicName = Environment.GetEnvironmentVariable("AEG_TOPIC_NAME");
            int maxEvents = int.Parse(Environment.GetEnvironmentVariable("MAX_EVENTS"));

            using var channel = GrpcChannel.ForAddress(serviceApiEndPoint);
            var client = new APSEventsClient(channel);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));


            //create blob container if does not exists
            await blobContainerClient.CreateIfNotExistsAsync();

            //create evetn grid client
            EventGridClient egClient = new EventGridClient(new Uri(topicEndpoint), new AzureKeyCredential(topicKey));

            //create event request setting
            APSEvent.EventRequestSetting requestSetting = new APSEvent.EventRequestSetting
            {
                MaxEvents = maxEvents
            };

            log.LogInformation($"Requesting to fetch max {requestSetting.MaxEvents} events from service endpoint");

            using var streamingCall = client.GetTransactionStream(requestSetting, cancellationToken: cts.Token);

            try
            {
                List<EventData> events = new List<EventData>(); //variable to hold events for display purpose

                await foreach (var eventData in streamingCall.ResponseStream.ReadAllAsync(cancellationToken: cts.Token))
                {
                    log.LogInformation($"Fetching event:Event ID: {eventData.Eventid}");

                    string jsonPayload = JsonConvert.SerializeObject(eventData);

                    string filename = $"event-{eventData.Eventid}.txt";
                    var blobClient = blobContainerClient.GetBlobClient(filename);

                    await blobClient.UploadAsync(BinaryData.FromString(jsonPayload), overwrite: true);

                    //publish event in event grid
                    EventInfo info = new EventInfo();

                    info.FileName = filename;
                    info.EventId = eventData.Eventid;

                    CloudEvent cloudEvent = new CloudEvent(eventData.EventSource, "APS.Event", info);
                    await egClient.PublishCloudEventAsync(topicName, cloudEvent);

                    events.Add(eventData);
                }

                if (events.Count > 0)
                {
                    log.LogInformation("=============================");
                    foreach (var eventData in events)
                    {
                        
                        log.LogInformation($"\nFetched event:\nEvent ID: {eventData.Eventid}" +
                            $"\nClient ID: {eventData.Clientid}" +
                            $"\nClient name: {eventData.Clientname}" +
                            $"\nEvent Source: {eventData.EventSource}" +
                            $"\nEvent Source: {eventData.EventType}" +
                            $"\nEvent Datetime: {eventData.EventDateTime.ToDateTime():s}");
                        
                    }
                    log.LogInformation("=============================");
                }
                else
                {
                    log.LogInformation("===No events found.===");
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                log.LogError("Stream cancelled.");
            }

            log.LogInformation($"Fetching events complete.");
        }
    }
}
