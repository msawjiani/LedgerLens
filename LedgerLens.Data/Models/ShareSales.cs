namespace LedgerLens.Data.Models
{
    public class SharesSales
    {
        public int ShareSoldId { get; set; }

        public int ShareId { get; set; }

        public int PurchaseTransId { get; set; }

        public decimal QtySold { get; set; }

        public decimal SellingPrice { get; set; }

        public int TransactionId { get; set; }

        public int Unix { get; set; }
    }
}