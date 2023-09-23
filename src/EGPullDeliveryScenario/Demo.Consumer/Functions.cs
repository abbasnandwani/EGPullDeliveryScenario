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

namespace Demo.Consumer
{
    public class Functions
    {
        [FunctionName("EventOrchestrator")]
        public async Task EventOrchestrator([TimerTrigger("*/5 * * * * *")] TimerInfo myTimer,
            [Blob("eventfiles", Connection = "BLOB_CONNSTR")] BlobContainerClient blobContainerClient,
            ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");


            string serviceApiEndPoint = Environment.GetEnvironmentVariable("SERVICE_API_ENDPOINT");
            string topicEndpoint = Environment.GetEnvironmentVariable("AEG_TOPIC_ENDPOINT");
            string topicKey = Environment.GetEnvironmentVariable("AEG_TOPIC_KEY");
            string topicName = Environment.GetEnvironmentVariable("AEG_TOPIC_NAME");

            using var channel = GrpcChannel.ForAddress(serviceApiEndPoint);
            var client = new TransactionEventsClient(channel);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));


            //create blob container if does not exists
            await blobContainerClient.CreateIfNotExistsAsync();

            TransactionEvent.EventRequestSetting requestSetting = new TransactionEvent.EventRequestSetting
            {
                MaxEvents = 2
            };

            using var streamingCall = client.GetTransactionStream(requestSetting, cancellationToken: cts.Token);

            try
            {
                await foreach (var transactionEventData in streamingCall.ResponseStream.ReadAllAsync(cancellationToken: cts.Token))
                {
                    log.LogInformation($"Tran Datetime: {transactionEventData.TransactionDateTime.ToDateTime():s} | TranId: {transactionEventData.Transactionid} | Amount: {transactionEventData.Amount} ");

                    string jsonPayload = JsonConvert.SerializeObject(transactionEventData);

                    string filename = $"event-{transactionEventData.Transactionid}.txt";
                    var blobClient = blobContainerClient.GetBlobClient(filename);

                    await blobClient.UploadAsync(BinaryData.FromString(jsonPayload), overwrite: true);
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                log.LogError("Stream cancelled.");
            }
        }
    }
}
