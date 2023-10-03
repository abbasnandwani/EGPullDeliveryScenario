using Demo.BusinessEventsService.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using APSEvent;

namespace Demo.BusinessEventsService.Services
{
    public class APSEventService : APSEvents.APSEventsBase
    {
        private readonly ILogger<APSEventService> _logger;
        private readonly BusinessEventsContext _dbContext;
        public APSEventService(BusinessEventsContext dbContext, ILogger<APSEventService> logger)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public override async Task GetTransactionStream(EventRequestSetting requestSetting,
            IServerStreamWriter<EventData> responseStream,
            ServerCallContext context)
        {
            var paymentInstructions = _dbContext.PaymentInstructionEvents
             .Include(t => t.Client)
             .Where(t => t.EventDispatched == false)
             .OrderBy(t => t.EventDateTime)
             .Take(requestSetting.MaxEvents);

            var i = 0;
            foreach (var instruction in paymentInstructions)
            {
                if (context.CancellationToken.IsCancellationRequested && i >= requestSetting.MaxEvents)
                {
                    break;
                }

                var apsevent = new EventData
                {
                    EventDateTime = Timestamp.FromDateTime(DateTime.SpecifyKind(instruction.EventDateTime, DateTimeKind.Utc)),
                    Eventid = instruction.EventId,
                    Clientid = instruction.ClientId.ToString(),
                    Clientname = instruction.Client.Name,
                    Amount = instruction.Amount,
                    //EventSource = (clientTransaction.ClientId == 3) ? "Demo.NonBusinessEvent" : "Demo.BusinessEvent"
                    EventSource = "Contoso.APS",
                    EventType = "APS.PaymentInstruction"
                };

                instruction.EventDispatched = true;
                _dbContext.SaveChanges();
                i++;

                await responseStream.WriteAsync(apsevent);
            }
        }
    }
}
