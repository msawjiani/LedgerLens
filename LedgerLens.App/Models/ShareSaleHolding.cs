using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerLens.App.Models
{
    public sealed class ShareSaleHolding
    {
        public int ShareId { get; set; }

        public string Company { get; set; } = string.Empty;

        public decimal AvailableQty { get; set; }
    }
}