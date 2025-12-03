using System;
using System.Threading.Tasks;

namespace LedgerLens.Services.Abstractions
{
    public interface IShareTradeService
    {
        /// Posts: Dr Shares A/c, Cr Broker/Bank A/c, then SharePurchases row (with debit GL id + unix).
        /// Returns: (debitGlId, creditGlId, sharePurchaseId, unix)
        Task<(int debitId, int creditId, int purchaseId, int unix)> MakeSharePurchaseAsync(
            int shareId,
            int debitSharesAccountId,
            int creditBrokerAccountId,
            int yearId,
            DateTime purchaseDate,
            DateTime glDate,     // <— NEW: actual acquisition date for SharePurchases
            decimal qty,
            decimal rate,
            string reference,
            string? narrationOverride = null,
            string? sharesAccountName = null,   // optional. If null we’ll use “Shares A/c”
            string? brokerAccountName = null,   // optional. If null we’ll use “Broker A/c”
            string? companyName = null          // optional. If null we’ll fetch from SharesChart
        );


        // NEW: sale (must exactly match your implementation’s signature)
        Task<int> MakeShareSaleAsync(
            int shareId,
            int brokerAccountId,
            int sharesAccountId,
            int ltCgAccountId,
            int stCgAccountId,
            int ltClAccountId,
            int stClAccountId,
            int yearId,
            DateTime saleDate,
            decimal qtyToSell,
            decimal sellingPrice,
            string reference,
            string transactionType,
            string? narrationOverride = null,
            string? companyName = null);
    }
}

