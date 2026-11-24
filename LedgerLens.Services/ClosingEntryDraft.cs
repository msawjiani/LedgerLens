using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace LedgerLens.Services
{
    public enum ClosingEntryPeriod
    {
        ClosingYear,
        OpeningYear
    }

    public sealed class ClosingEntryDraft
    {
        public int AccountId { get; init; }

        public ClosingEntryPeriod Period { get; init; }

        public DateTime Tdate { get; init; }

        public decimal Amount { get; init; }

        public string TransactionType { get; init; } = "";

        public string Ref { get; init; } = "";

        public string Particulars { get; init; } = "";

        public string Narration { get; init; } = "";
    }
}
