// LedgerLens.Data/Models/ShareSale.cs
namespace LedgerLens.Data.Models
{
    public sealed class ShareSale
    {
        public int ShareSoldId { get; set; }   // AutoNumber, not usually needed
        public int ShareId { get; set; }
        public int PurchaseTransId { get; set; }
        public decimal QtySold { get; set; }
        public decimal SellingPrice { get; set; }
        public int TransactionId { get; set; } // GL id for Shares (line 2)
        public int Unix { get; set; }
    }
}
