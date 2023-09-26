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
                    Amount = clientTransaction.Amount,
                    EventSource = (clientTransaction.ClientId == 3) ? "Demo.NonBusinessEvent" : "Demo.BusinessEvent"
                };

                clientTransaction.EventDispatched = true;
                _dbContext.SaveChanges();
                i++;

                await responseStream.WriteAsync(transaction);
            }
        }
    }
}
