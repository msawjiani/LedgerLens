using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;
using LedgerLens.Services.Abstractions;
using System;
using System.Data.OleDb;
using System.Globalization;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LedgerLens.Services
{
    public sealed class ShareTradeService : IShareTradeService
    {
        private readonly IConnectionFactory _factory;
        private readonly ITransactionRepository _repo;

        public ShareTradeService(IConnectionFactory factory, ITransactionRepository repo)
        {
            _factory = factory;
            _repo = repo;
        }

        public Task<(int debitId, int creditId, int purchaseId, int unix)> MakeSharePurchaseAsync(
            int shareId,
            int debitSharesAccountId,
            int creditBrokerAccountId,
            int yearId,
            DateTime purchaseDate,
            DateTime glDate,
            decimal qty,
            decimal rate,
            string reference,
            string? narrationOverride = null,
            string? sharesAccountName = null,
            string? brokerAccountName = null,
            string? companyName = null)
        {
            using var con = _factory.CreateOpen();
            using var tx = con.BeginTransaction();

            try
            {
                // 1) Common unix
                var unixLng = _repo.GetNextUnix(tx);
                int unix = (int)unixLng;

                // 2) Optional company lookup for narration
                companyName ??= _repo.GetShareCompanyName(shareId, tx) ?? "Company";

                // 3) Amount = qty * rate (rounded to 2)
                var amount = Math.Round(qty * rate, 2, MidpointRounding.AwayFromZero);

                // 4) Particulars + Narration
                var debitParticulars = $"To {brokerAccountName ?? "Broker A/c"}";
                var creditParticulars = $"By {sharesAccountName ?? "Shares A/c"}";

                var narration = narrationOverride ??
                    $"Being shares purchased for {companyName}, Qty {qty.ToString("N2", CultureInfo.InvariantCulture)} " +
                    $"@ {rate.ToString("N2", CultureInfo.InvariantCulture)} = {amount.ToString("N2", CultureInfo.InvariantCulture)}";

                // 5) GL rows
                var debit = new GeneralLedger
                {
                    AccountId = debitSharesAccountId,      // Shares account (DEBIT)
                    YearId = yearId,
                    Unix = unix,
                    Tdate = glDate,
                   
                    Ref = reference,
                    Particulars = debitParticulars,
                    Amount = amount,                    // +ve
                    TransactionType = "BE",
                    Narration = narration
                };

                var credit = new GeneralLedger
                {
                    AccountId = creditBrokerAccountId,     // Broker/Bank (CREDIT)
                    YearId = yearId,
                    Unix = unix,
                    Tdate = glDate,
                    Ref = reference,
                    Particulars = creditParticulars,
                    Amount = -amount,                   // -ve
                    TransactionType = "BE",
                    Narration = narration
                };

                var debitId = _repo.InsertGeneralLedger(debit, tx);
                var creditId = _repo.InsertGeneralLedger(credit, tx);

                // 6) SharePurchases row uses the *debit* GL id
                var sp = new SharePurchase
                {
                    ShareId = shareId,
                    PurchaseDate = purchaseDate,
                    TransactionId = debitId,       // IMPORTANT: link to DEBIT GL row
                    Unix = unix,
                    QtyPurchased = qty,
                    PurchaseRate = rate
                };
                var purchaseId = _repo.InsertSharePurchase(sp, tx);

                tx.Commit();
                return Task.FromResult((debitId, creditId, purchaseId, unix));
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }
    }
}
