using Google.Protobuf.WellKnownTypes;
using System;

namespace Demo.Consumer.EventOrchestrator
{
    public class EventInfo
    {
        public string TransactionId { get; set; }
        public string FileName { get; set; }
    }

    public class TransactionInfo
    {
        public string TransactionId { get; set; }
        public int ClientId { get; set; }
        public double Amount { get; set; }
        public Timestamp TransactionDateTime { get; set; }
    }
}
