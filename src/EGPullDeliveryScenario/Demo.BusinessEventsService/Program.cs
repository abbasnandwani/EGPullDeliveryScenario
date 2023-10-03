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

            app.MapGrpcService<APSEventService>(); //map grpc service

            app.Run();
        }


        private static void InitializeDummyData(WebApplication app)
        {
            var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BusinessEventsContext>();

            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            List<Client> clients = new List<Client>();

            clients.Add(new Client { Id = 1, Name = "Acme Corporation" });
            clients.Add(new Client { Id = 2, Name = "Sparks Ltd." });
            clients.Add(new Client { Id = 3, Name = "Contoso Ltd." });
            clients.Add(new Client { Id = 4, Name = "Globex Corporation" });

            db.Clients.AddRange(clients);

            db.SaveChanges();
        }
    }
}