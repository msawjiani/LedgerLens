using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerLens.Data.Models
{
   
        public class Subledger
        {
            public int SubledgerId { get; set; }

            public int AccountId { get; set; }

            public string Subaccount { get; set; } = string.Empty;
        }
    
}
