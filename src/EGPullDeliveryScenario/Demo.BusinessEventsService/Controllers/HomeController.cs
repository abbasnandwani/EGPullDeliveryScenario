using Demo.BusinessEventsService.Models;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Diagnostics;
using TransactionEvent;

namespace Demo.BusinessEventsService.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly BusinessEventsContext _dbContext;

        public HomeController(BusinessEventsContext dbContext, ILogger<HomeController> logger)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new IndexViewModel();

            ViewBag.ListClients = _dbContext.Clients.Select(c => new SelectListItem { Text = c.Name, Value = c.Id.ToString() }).ToList();

            return View(model);
        }

        [HttpPost]
        public IActionResult Index(IndexViewModel model)
        {
            ViewBag.ListClients = _dbContext.Clients.Select(c => new SelectListItem { Text = c.Name, Value = c.Id.ToString() }).ToList();


            if (model.GenerateRandom)
            {
                for(int i=0; i<model.NumberOfTransactions; i++)
                {
                    _dbContext.ClientTransactions.Add(new ClientTransaction
                    {
                        ClientId = model.ClientId,
                        Amount = new Random().NextDouble() * 1000,
                        TransactionDateTime = DateTime.Now,
                        TransactionId = Guid.NewGuid().ToString()
                    });
                }
            }
            else
            {
                _dbContext.ClientTransactions.Add(new ClientTransaction
                {
                    ClientId = model.ClientId,
                    Amount = model.Amount,
                    TransactionDateTime = DateTime.Now,
                    TransactionId = Guid.NewGuid().ToString()
                });
            }

            _dbContext.SaveChanges();

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult ViewClients()
        {
            var clients = _dbContext.Clients.ToList();
            return View(clients);
        }

        public IActionResult ViewTransactions()
        {
            var transactions = _dbContext.ClientTransactions.ToList();
            return View(transactions);
        }

        public IActionResult TestEventDispatch(int numevents)
        {
            var transactions = _dbContext.ClientTransactions.ToList();

            var model = new List<TransactionEventData>();

            var clientTransactions = _dbContext.ClientTransactions
             .Where(t => t.EventDispatched == false)
             .OrderBy(t => t.TransactionDateTime)
             .Take(numevents);

            var i = 0;
            foreach (var clientTransaction in clientTransactions)
            {
                if (i >= numevents)
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

                model.Add(transaction);

                clientTransaction.EventDispatched = true;
                _dbContext.SaveChanges();
                i++;
            }   
                       

            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}