using Microsoft.AspNetCore.Mvc.Rendering;

namespace Demo.BusinessEventsService.Models
{
    public class IndexViewModel
    {        
        public int ClientId { get; set; }
        public double Amount { get; set; }
        
        public bool GenerateRandom { get; set; }

        public int NumberOfTransactions { get; set; }
    }
}