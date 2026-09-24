using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerLens.Data.Models
{
    public class Company
    {
        public int ShareId { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        public int AccountId { get; set; }
    }
}
