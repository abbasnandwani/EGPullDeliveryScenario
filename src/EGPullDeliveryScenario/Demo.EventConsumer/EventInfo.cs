using Google.Protobuf.WellKnownTypes;
using System;

namespace Demo.EventConsumer
{
    public class EventInfo
    {
        public string EventId { get; set; }
        public string FileName { get; set; }
    }

    public class EventPayload
    {
        public string EventId { get; set; }
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public double Amount { get; set; }
        public Timestamp EventDateTime { get; set; }
    }
}
