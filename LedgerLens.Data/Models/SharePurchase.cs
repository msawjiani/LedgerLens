namespace LedgerLens.Data.Models
{
    public sealed class SharePurchase
    {
        public int PurchaseTransId { get; set; }       // AutoNumber (returned by insert if you want)
        public int ShareId { get; set; }               // REQUIRED
        public DateTime PurchaseDate { get; set; }     // REQUIRED
        public int TransactionId { get; set; }         // REQUIRED -> the GL *debit* row id
        public int Unix { get; set; }                  // REQUIRED -> shared unix used for both GL rows
        public decimal QtyPurchased { get; set; }      // REQUIRED
        public decimal PurchaseRate { get; set; }      // REQUIRED
    }
}
