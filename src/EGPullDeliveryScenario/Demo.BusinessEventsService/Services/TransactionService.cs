using Demo.BusinessEventsService.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using TransactionEvent;

namespace Demo.BusinessEventsService.Services
{
    public class TransactionService : TransactionEvents.TransactionEventsBase
    {
        private readonly ILogger<TransactionService> _logger;
        private readonly BusinessEventsContext _dbContext;
        public TransactionService(BusinessEventsContext dbContext, ILogger<TransactionService> logger)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public override async Task GetTransactionStream(EventRequestSetting requestSetting, IServerStreamWriter<TransactionEventData> responseStream,
            ServerCallContext context)
        {
            //var rng = new Random();
            //var now = DateTime.UtcNow;

            //var clientTransactions = _dbContext.ClientTransactions
            //    .Where(t => t.EventDispatched == false)
            //    .OrderBy(t => t.TransactionDateTime)
            //    .Take(requestSetting.MaxEvents);

            //var i = 0;
            //while (!context.CancellationToken.IsCancellationRequested && i < requestSetting.MaxEvents)
            //{
            //    await Task.Delay(500); // Gotta look busy

            //    var clientTransaction = clientTransactions.Take(1).FirstOrDefault();

            //    var transaction = new TransactionEventData
            //    {
            //        TransactionDateTime = Timestamp.FromDateTime(now.AddDays(i++)),
            //        Transactionid = Guid.NewGuid().ToString(),
            //        Clientid = Guid.NewGuid().ToString(),
            //        Amount = rng.Next(20, 1550)
            //    };

            //    _logger.LogInformation("Sending TransactionEventData response");

            //    await responseStream.WriteAsync(transaction);
            //}

            var transactions = _dbContext.ClientTransactions.ToList();

            var clientTransactions = _dbContext.ClientTransactions
             .Where(t => t.EventDispatched == false)
             .OrderBy(t => t.TransactionDateTime)
             .Take(requestSetting.MaxEvents);

            var i = 0;
            foreach (var clientTransaction in clientTransactions)
            {
                if (context.CancellationToken.IsCancellationRequested && i >= requestSetting.MaxEvents)
                {
                    break;
                }

                var transaction = new TransactionEventData
                {
                    TransactionDateTime = Timestamp.FromDateTime(DateTime.SpecifyKind(clientTransaction.TransactionDateTime, DateTimeKind.Utc)),
                    Transactionid = clientTransaction.TransactionId,
                    Clientid = clientTransaction.ClientId.ToString(),
                    Amount = clientTransaction.Amount
                };

                clientTransaction.EventDispatched = true;
                _dbContext.SaveChanges();
                i++;

                await responseStream.WriteAsync(transaction);
            }
        }
    }
}
