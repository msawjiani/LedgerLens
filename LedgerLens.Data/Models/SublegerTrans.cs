using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerLens.Data.Models
{
    public sealed class SubledgerTrans
    {
        public int SubledgerTransId { get;  }
        public int SubledgerId { get; set; }
        public long Unix { get; set; }
        public int TransactionId { get; set; }
    }
}

