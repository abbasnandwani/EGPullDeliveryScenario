using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace Demo.BusinessEventsService.Models
{
    public class BusinessEventsContext : DbContext
    {
        public BusinessEventsContext(DbContextOptions<BusinessEventsContext> options)
            : base(options)
        { }

        public DbSet<Client> Clients { get; set; }
        public DbSet<PaymentInstructionEvent> PaymentInstructionEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PaymentInstructionEvent>()
                .HasKey(c => c.EventId);
        }
    }


    public class Client
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public List<PaymentInstructionEvent> PaymentInstructionEvents { get; } = new();
    }

    public class PaymentInstructionEvent
    {
        public string EventId { get; set; }
        public int ClientId { get; set; }
        public double Amount { get; set; }
        public DateTime EventDateTime { get; set; }

        public bool EventDispatched { get; set; }

        public Client Client { get; set; }
    }
}
