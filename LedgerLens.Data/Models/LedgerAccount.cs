using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerLens.Data.Models
{
    public class LedgerAccount
    {
        public int AccountId { get; set; }

        public string Account { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string SubledgerFlag { get; set; } = string.Empty;
        public string DashboardGroup { get; set; } = string.Empty;
    }
}
