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


        public Task<int> MakeShareSaleAsync(
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
            string? companyName = null)
                {
                    using var con = _factory.CreateOpen();
                    using var tx = con.BeginTransaction();

                    try
                    {
                        if (qtyToSell <= 0m)
                            throw new ArgumentException("qtyToSell must be > 0", nameof(qtyToSell));

                        // 1) common unix
                        var unixLng = _repo.GetNextUnix(tx);
                        int unix = (int)unixLng;

                        // 2) company for narration
                        companyName ??= _repo.GetShareCompanyName(shareId, tx) ?? "Company";

                        // 3) open lots FIFO
                        var lots = _repo.GetShareBalanceRows(shareId, tx)
                                        .Where(l => l.Balance > 0m)
                                        .OrderBy(l => l.PurchaseDate)
                                        .ToList();

                        decimal remaining = qtyToSell;

                        foreach (var lot in lots)
                        {
                            if (remaining <= 0m)
                                break;

                            var lotQty = Math.Min(lot.Balance, remaining);
                            remaining -= lotQty;

                            // ---- amounts ----
                            var saleAmount = Math.Round(lotQty * sellingPrice, 2, MidpointRounding.AwayFromZero);
                            var purchaseCost = Math.Round(lotQty * lot.PurchaseRate, 2, MidpointRounding.AwayFromZero);
                            var diff = saleAmount - purchaseCost;   // + = gain, - = loss

                            int months = GetMonthDifference(lot.PurchaseDate, saleDate);
                            bool isLongTerm = months >= 12;

                            bool isGain = diff >= 0m;

                            int capAccountId =
                                isGain
                                    ? (isLongTerm ? ltCgAccountId : stCgAccountId)
                                    : (isLongTerm ? ltClAccountId : stClAccountId);

                            decimal capAmount = isGain ? -diff : -diff; // credit for gain, debit for loss
                            // (diff positive -> capAmount negative; diff negative -> capAmount positive)

                            string narration = narrationOverride ??
                                $"Being {companyName} Sold: {companyName} Qty " +
                                $"{lotQty.ToString("N0", CultureInfo.InvariantCulture)}@" +
                                $"{sellingPrice.ToString("N2", CultureInfo.InvariantCulture)}";

                            // ---- GL line 1: Broker / Bank Dr ----
                            var gl1 = new GeneralLedger
                            {
                                AccountId = brokerAccountId,
                                YearId = yearId,
                                Unix = unix,
                                Tdate = saleDate,
                                Ref = reference,
                                Particulars = "Consolidated Entry. 1 Share Broker",
                                Amount = saleAmount,           // Dr +
                                TransactionType = transactionType,
                                Narration = narration
                            };

                            // ---- GL line 2: Shares Cr at cost ----
                            var gl2 = new GeneralLedger
                            {
                                AccountId = sharesAccountId,
                                YearId = yearId,
                                Unix = unix,
                                Tdate = saleDate,
                                Ref = reference,
                                Particulars = "Consolidated Entry. 2 Shares Account",
                                Amount = -purchaseCost,        // Cr -
                                TransactionType = transactionType,
                                Narration = narration
                            };

                            // ---- GL line 3: Gain / Loss ----
                            var gl3 = new GeneralLedger
                            {
                                AccountId = capAccountId,
                                YearId = yearId,
                                Unix = unix,
                                Tdate = saleDate,
                                Ref = reference,
                                Particulars = isGain
                                    ? "Consolidated Entry. 3 Gain Entry"
                                    : "Consolidated Entry. 3 Loss Entry",
                                Amount = capAmount,            // gain credit(-) / loss debit(+)
                                TransactionType = transactionType,
                                Narration = narration
                            };

                            int gl1Id = _repo.InsertGeneralLedger(gl1, tx);
                            int gl2Id = _repo.InsertGeneralLedger(gl2, tx);
                            int gl3Id = _repo.InsertGeneralLedger(gl3, tx); // gl3Id not used; just for completeness

                            // ---- ShareSales row for this lot ----
                            var saleRow = new ShareSale
                            {
                                ShareId = shareId,
                                PurchaseTransId = lot.PurchaseTransId,
                                QtySold = lotQty,
                                SellingPrice = sellingPrice,
                                TransactionId = gl2Id,    // Shares GL id
                                Unix = unix
                            };

                            _repo.InsertShareSale(saleRow, tx);
                        }

                        if (remaining > 0m)
                        {
                            throw new InvalidOperationException(
                                $"Not enough shares available to sell {qtyToSell}. Short by {remaining}.");
                        }

                        tx.Commit();
                        return Task.FromResult(unix);
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
        private static int GetMonthDifference(DateTime startDate, DateTime endDate)
        {
            if (endDate < startDate)
                throw new ArgumentException("endDate must be after startDate");

            int yearDiff = endDate.Year - startDate.Year;
            int monthDiff = endDate.Month - startDate.Month;

            if (monthDiff < 0)
            {
                yearDiff--;
                monthDiff += 12;
            }

            return yearDiff * 12 + monthDiff;
        }
    }
}
