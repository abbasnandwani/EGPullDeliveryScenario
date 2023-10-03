using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel;

namespace Demo.BusinessEventsService.Models
{
    public class IndexViewModel
    {
        [DisplayName("Customer")]
        public int ClientId { get; set; }
        public double Amount { get; set; }

        [DisplayName("Generate Random")] 
        public bool GenerateRandom { get; set; }

        [DisplayName("Number of Instructions")]
        public int NumberOfTransactions { get; set; }
    }
}