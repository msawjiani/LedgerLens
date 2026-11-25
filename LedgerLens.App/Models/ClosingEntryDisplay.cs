using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace LedgerLens.App.Models
{
    public sealed class ClosingEntryDisplay
    {
        public string Period { get; set; } = "";

        public DateTime Tdate { get; set; }

        public string Ref { get; set; } = "";

        public string Particulars { get; set; } = "";

        public decimal Amount { get; set; }

        public decimal? Debit =>
            Amount > 0m ? Amount : null;

        public decimal? Credit =>
            Amount < 0m ? -Amount : null;
    }
}