// LedgerLens.Data/Models/ShareBalanceRow.cs
namespace LedgerLens.Data.Models
{
    public sealed class ShareBalanceRow
    {
        public int PurchaseTransId { get; set; }
        public int ShareId { get; set; }
        public System.DateTime PurchaseDate { get; set; }
        public decimal Balance { get; set; }       // remaining qty from this lot
        public decimal PurchaseRate { get; set; }
    }
}
