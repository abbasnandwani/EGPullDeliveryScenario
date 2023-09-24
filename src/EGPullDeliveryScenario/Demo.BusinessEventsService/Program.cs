using Demo.BusinessEventsService.Models;
using Demo.BusinessEventsService.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Demo.BusinessEventsService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddGrpc(); //add grpc services

            // Add Sqlite database
            var sqliteConn = new SqliteConnection("Filename=:memory:");
            sqliteConn.Open();
            builder.Services.AddDbContext<BusinessEventsContext>
                (options => options.UseSqlite(sqliteConn));

            var app = builder.Build();

            InitializeDummyData(app);

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapGrpcService<TransactionService>(); //map grpc service

            app.Run();
        }


        private static void InitializeDummyData(WebApplication app)
        {
            var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BusinessEventsContext>();

            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            List<Client> clients = new List<Client>();

            clients.Add(new Client { Id = 1, Name = "Client 1" });
            clients.Add(new Client { Id = 2, Name = "Client 2" });
            clients.Add(new Client { Id = 3, Name = "Client 3" });
            clients.Add(new Client { Id = 4, Name = "Client 4" });

            db.Clients.AddRange(clients);

            List<ClientTransaction> clientTransactions = new List<ClientTransaction>();

            for (int i = 0; i < 10; i++)
            {
                db.ClientTransactions.Add(new ClientTransaction
                {
                    ClientId = new Random().Next(1,4),
                    Amount = new Random().NextDouble() * 1000,
                    TransactionDateTime = DateTime.Now,
                    TransactionId = Guid.NewGuid().ToString()
                });
            }


            db.SaveChanges();
        }
    }
}