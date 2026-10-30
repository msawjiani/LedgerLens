using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerLens.Data.Models
{
    public class FDBalance
    {
        public int SubledgerId { get; set; }

        public string Subaccount { get; set; } = string.Empty;

        public decimal TotalFD { get; set; }
    }
}