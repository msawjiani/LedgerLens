using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerLens.App.Session
{
    public static class SessionContext
    {
        public static string DatabaseFileName { get; set; } = "";
        public static string ConnectionString { get; set; } = "";
        public static string PAN { get; set; } = "";
        public static string IndividualName { get; set; } = "";

        public static int MaxYearId { get; set; }
        public static int SelectedYearId { get; set; }
        public static DateTime SelectedStartDate { get; set; }
        public static DateTime SelectedEndDate { get; set; }

        public static int InterestAccountCode { get; set; }
        public static string InterestAccountDesc { get; set; } = "";

        public static int LTCGAccountCode { get; set; }
        public static string LTCGAccountDesc { get; set; } = "";

        public static int STCGAccountCode { get; set; }
        public static string STCGAccountDesc { get; set; } = "";

        public static int LTCLAccountCode { get; set; }
        public static string LTCLAccountDesc { get; set; } = "";

        public static int STCLAccountCode { get; set; }
        public static string STCLAccountDesc { get; set; } = "";

        public static int RetainedEarningsId { get; set; }
    }
}