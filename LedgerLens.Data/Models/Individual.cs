using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace LedgerLens.Data.Models
{
    public class Individual
    {
        public string IndividualName { get; set; } = "";
        public string PANNumber { get; set; } = "";
        public string PhotoFile { get; set; } = "";

        public int InterestAccountCode { get; set; }
        public string InterestAccountDesc { get; set; } = "";

        public int LTCGAccountCode { get; set; }
        public string LTCGAccountDesc { get; set; } = "";

        public int STCGAccountCode { get; set; }
        public string STCGAccountDesc { get; set; } = "";

        public int LTCLAccountCode { get; set; }
        public string LTCLAccountDesc { get; set; } = "";

        public int STCLAccountCode { get; set; }
        public string STCLAccountDesc { get; set; } = "";

        public int RetainedEarningsId { get; set; }
    }
}
