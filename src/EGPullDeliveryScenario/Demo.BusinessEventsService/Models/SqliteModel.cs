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
        public DbSet<ClientTransaction> ClientTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClientTransaction>()
                .HasKey(c => c.TransactionId);
        }
    }


    public class Client
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public List<ClientTransaction> Transactions { get; } = new();
    }

    public class ClientTransaction
    {
        public string TransactionId { get; set; }
        public int ClientId { get; set; }
        public double Amount { get; set; }
        public DateTime TransactionDateTime { get; set; }

        public bool EventDispatched { get; set; }

        public Client Client { get; set; }
    }
}
