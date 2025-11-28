using System;
using System.Threading.Tasks;

namespace LedgerLens.Services.Abstractions
{
    public interface IShareTradeServiceExtra
    {
        /// Posts: Dr Shares A/c, Cr Broker/Bank A/c, then SharePurchases row (with debit GL id + unix).
        /// Returns: (debitGlId, creditGlId, sharePurchaseId, unix)
        Task<(int debitId, int creditId, int purchaseId, int unix)> MakeSharePurchaseAsync(
            int shareId,
            int debitSharesAccountId,
            int creditBrokerAccountId,
            int yearId,
            DateTime purchaseDate,
            decimal qty,
            decimal rate,
            string reference,
            string? narrationOverride = null,
            string? sharesAccountName = null,   // optional. If null we’ll use “Shares A/c”
            string? brokerAccountName = null,   // optional. If null we’ll use “Broker A/c”
            string? companyName = null          // optional. If null we’ll fetch from SharesChart
        );
    }
}
