using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerLens.App.Models
{
    public class TurboSharePurchaseRow
    {
        public int ExcelRowNumber { get; set; }

        public string GLAccountForCredit { get; set; } = "";
        public string GLSharesAccountForDebit { get; set; } = "";
        public string Company { get; set; } = "";

        public DateTime? TradeDate { get; set; }

        public decimal Qty { get; set; }
        public decimal Rate { get; set; }
        public decimal Amt { get; set; }

        public string? ErrorMessage { get; set; }
        public int? CreditAccountId { get; set; }

        public int? SharesAccountId { get; set; }

        public int? ShareId { get; set; }
    }
}