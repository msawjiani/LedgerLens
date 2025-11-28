using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerLens.Data.Models
{
    public sealed class GeneralLedger
    {
        public int AccountId { get; set; }
        public int YearId { get; set; }
        public int Unix { get; set; }                // Access table is Integer → keep this int
        public DateTime Tdate { get; set; }
        public string Ref { get; set; } = "";
        public string Particulars { get; set; } = "";
        public decimal Amount { get; set; }          // + = Dr, - = Cr
        public string TransactionType { get; set; } = "BE";
        public string Narration { get; set; } = "";
    }
}
