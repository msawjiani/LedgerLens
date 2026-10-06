using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerLens.Data.Models
{
    public sealed class BankEntryRow
    {
        public int TransactionId { get; set; }

        public int AccountId { get; set; }

        public int Unix { get; set; }

        public DateTime TDate { get; set; }

        public string Ref { get; set; } = string.Empty;

        public string Particulars { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Narration { get; set; } = string.Empty;

        public decimal RunningBalance { get; set; }
    }
}
