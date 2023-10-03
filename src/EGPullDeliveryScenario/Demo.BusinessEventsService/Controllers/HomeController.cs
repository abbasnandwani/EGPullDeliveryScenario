using Demo.BusinessEventsService.Models;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using APSEvent;

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

            model.GenerateRandom = true;

            return View(model);
        }

        [HttpPost]
        public IActionResult Index(IndexViewModel model)
        {
            ViewBag.ListClients = _dbContext.Clients.Select(c => new SelectListItem { Text = c.Name, Value = c.Id.ToString() }).ToList();


            if (model.GenerateRandom)
            {
                for (int i = 0; i < model.NumberOfTransactions; i++)
                {
                    _dbContext.PaymentInstructionEvents.Add(new PaymentInstructionEvent
                    {
                        ClientId = new Random().Next(1, 4),
                        Amount = Math.Round(new Random().NextDouble() * 1000, 2),
                        EventDateTime = DateTime.Now,
                        EventId = Guid.NewGuid().ToString()
                    });
                }
            }
            else
            {
                _dbContext.PaymentInstructionEvents.Add(new PaymentInstructionEvent
                {
                    ClientId = model.ClientId,
                    Amount = model.Amount,
                    EventDateTime = DateTime.Now,
                    EventId = Guid.NewGuid().ToString()
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

        public IActionResult ViewEvents()
        {
            var transactions = _dbContext.PaymentInstructionEvents.Include(a => a.Client).ToList();
            return View(transactions);
        }

        public IActionResult TestEventDispatch(int numevents)
        {
            var transactions = _dbContext.PaymentInstructionEvents.Include(a => a.Client).ToList();

            var model = new List<EventData>();

            var instructions = _dbContext.PaymentInstructionEvents
             .Include(t => t.Client)
             .Where(t => t.EventDispatched == false)
             .OrderBy(t => t.EventDateTime)
             .Take(numevents);

            var i = 0;
            foreach (var instruction in instructions)
            {
                if (i >= numevents)
                {
                    break;
                }

                var transaction = new EventData
                {
                    EventDateTime = Timestamp.FromDateTime(DateTime.SpecifyKind(instruction.EventDateTime, DateTimeKind.Utc)),
                    Eventid = instruction.EventId,
                    Clientid = instruction.ClientId.ToString(),
                    Amount = instruction.Amount,
                    Clientname = instruction.Client.Name,
                    //EventSource = (clientTransaction.ClientId == 3) ? "Demo.NonBusinessEvent" : "Demo.BusinessEvent"
                    EventSource = "Contose.APS",
                    EventType = "APS.PaymentInstruction"
                };

                model.Add(transaction);

                instruction.EventDispatched = true;
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