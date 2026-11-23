using LedgerLens.Data.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.OleDb;


using LedgerLens.Data.Models;
using System;
using System.Collections.Generic;

using System.Linq;

namespace LedgerLens.Services
{
    public sealed class ShareSalePostingService
    {
        private readonly ShareSaleService _shareSaleService;

        private readonly IConnectionFactory _factory;
        private readonly ITransactionRepository _repo;

        // STEP 5D.2 - Resolve Capital Gain / Loss Account

        private static int GetCapitalAccountId(
            string classification,
            int ltcgAccountId,
            int stcgAccountId,
            int ltclAccountId,
            int stclAccountId)
        {
            int accountId = classification switch
            {
                "LTCG" => ltcgAccountId,
                "STCG" => stcgAccountId,
                "LTCL" => ltclAccountId,
                "STCL" => stclAccountId,

                _ => throw new InvalidOperationException(
                    $"Unknown capital gain classification: {classification}")
            };

            if (accountId <= 0)
            {
                throw new InvalidOperationException(
                    $"Capital account is not configured for {classification}.");
            }

            return accountId;
        }
        // STEP 5D.3A - Validate Accounting Year Before Posting

        private static void ValidateAccountingYear(
            OleDbConnection con,
            OleDbTransaction tx,
            int yearId,
            DateTime saleDate)
        {
            if (yearId <= 0)
                throw new InvalidOperationException(
                    "Invalid accounting year.");

            // 1. Posting permitted only in latest accounting year

            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;

                cmd.CommandText =
                    "SELECT MAX(YearID) FROM AccountYears";

                object? result = cmd.ExecuteScalar();

                if (result == null ||
                    result == DBNull.Value ||
                    Convert.ToInt32(result) != yearId)
                {
                    throw new InvalidOperationException(
                        "Share Sales posting is permitted only " +
                        "in the latest accounting year.");
                }
            }

            // 2. Validate sale date against financial year

            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;

                cmd.CommandText =
                    """
            SELECT StartDate, EndDate
            FROM AccountYears
            WHERE YearID = ?
            """;

                cmd.Parameters.AddWithValue("@p1", yearId);

                using var reader = cmd.ExecuteReader();

                if (reader == null || !reader.Read())
                    throw new InvalidOperationException(
                        "Accounting year not found.");

                DateTime startDate =
                    Convert.ToDateTime(reader["StartDate"]).Date;

                DateTime endDate =
                    Convert.ToDateTime(reader["EndDate"]).Date;

                if (saleDate.Date < startDate ||
                    saleDate.Date > endDate)
                {
                    throw new InvalidOperationException(
                        $"Share Sale date must be between " +
                        $"{startDate:dd-MMM-yyyy} and " +
                        $"{endDate:dd-MMM-yyyy}.");
                }
            }
        }
        public ShareSalePostingService(
            IConnectionFactory factory,
            ITransactionRepository repo)
        {
            _factory = factory;
            _repo = repo;

            _shareSaleService = new ShareSaleService(factory);
        }

        // STEP 5C.1 - Prepare Share Sale Posting

        public List<(int PurchaseTransId,
                     int SharesAtCostAccountId,
                     decimal QtySold,
                     decimal PurchaseCost,
                     decimal SaleProceeds,
                     decimal GainLoss,
                     string Classification)> PrepareSale(
            int shareId,
            DateTime saleDate,
            decimal quantity,
            decimal sellingPrice)
        {
            var details =
                _shareSaleService.CalculateSaleDetails(
                    shareId,
                    saleDate,
                    quantity,
                    sellingPrice);

            var result = details.Select(x =>
            {
                if (x.Lot.SharesAtCostAccountId <= 0)
                    throw new InvalidOperationException(
                        "Invalid Shares-at-Cost AccountId.");

                string classification =
                    _shareSaleService.ClassifyCapitalGain(
                        x.Lot.PurchaseDate,
                        saleDate,
                        x.GainLoss);

                return (
                    PurchaseTransId: x.Lot.PurchaseTransId,
                    SharesAtCostAccountId:
                        x.Lot.SharesAtCostAccountId,
                    QtySold: x.QtySold,
                    PurchaseCost: x.PurchaseCost,
                    SaleProceeds: x.SaleProceeds,
                    GainLoss: x.GainLoss,
                    Classification: classification
                );
            }).ToList();



            // Verify accounting balance
            decimal totalProceeds =
                result.Sum(x => x.SaleProceeds);

            decimal totalCost =
                result.Sum(x => x.PurchaseCost);

            decimal totalGainLoss =
                result.Sum(x => x.GainLoss);

            if (totalProceeds != totalCost + totalGainLoss)
                throw new InvalidOperationException(
                    "Share Sale accounting does not balance.");

            return result;
        }

        // STEP 5D.3B - Allocate FIFO Within Posting Transaction

        private List<(ShareSaleLot Lot, decimal QtySold)>
            AllocateFIFOForPosting(
                int shareId,
                DateTime saleDate,
                decimal quantityToSell,
                OleDbTransaction tx)
        {
            if (shareId <= 0)
                throw new InvalidOperationException(
                    "Invalid ShareId.");

            if (saleDate == default)
                throw new InvalidOperationException(
                    "Invalid sale date.");

            if (quantityToSell <= 0)
                throw new InvalidOperationException(
                    "Sale quantity must be greater than zero.");

            // Retrieve lots within the existing transaction.
            var lots = _shareSaleService
                .GetAvailableLots(shareId, tx)
                .Where(x => x.PurchaseDate.Date <= saleDate.Date)
                .OrderBy(x => x.PurchaseDate)
                .ThenBy(x => x.PurchaseTransId)
                .ToList();

            // Validate purchase-lot information.
            foreach (var lot in lots)
            {
                if (lot.QtyPurchased <= 0 ||
                    lot.PurchaseRate < 0 ||
                    lot.Balance <= 0 ||
                    lot.SharesAtCostAccountId <= 0)
                {
                    throw new InvalidOperationException(
                        $"Invalid purchase lot: {lot.PurchaseTransId}.");
                }
            }

            decimal totalAvailable = lots.Sum(x => x.Balance);

            if (quantityToSell > totalAvailable)
            {
                throw new InvalidOperationException(
                    "Insufficient shares available.\n\n" +
                    $"Requested: {quantityToSell}\n" +
                    $"Available: {totalAvailable}");
            }

            var allocations =
                new List<(ShareSaleLot Lot, decimal QtySold)>();

            decimal remaining = quantityToSell;

            foreach (var lot in lots)
            {
                if (remaining == 0)
                    break;

                decimal allocated =
                    Math.Min(lot.Balance, remaining);

                allocations.Add((lot, allocated));

                remaining -= allocated;
            }

            if (remaining != 0)
            {
                throw new InvalidOperationException(
                    "FIFO allocation did not consume the requested quantity.");
            }

            return allocations;
        }
        // STEP 5D.3C - Atomic Share Sales Posting

        public long PostShareSale(
            int shareId,
            int yearId,
            DateTime saleDate,
            decimal quantity,
            decimal sellingPrice,
            int brokerAccountId,
            string reference,
            string narration,
            int ltcgAccountId,
            int stcgAccountId,
            int ltclAccountId,
            int stclAccountId)
        {
            // 1. Basic validation

            if (shareId <= 0 || brokerAccountId <= 0)
                throw new InvalidOperationException(
                    "Invalid Share or Broker Account.");

            if (quantity <= 0 || sellingPrice <= 0)
                throw new InvalidOperationException(
                    "Quantity and Selling Price must be positive.");

            if (string.IsNullOrWhiteSpace(reference) ||
                string.IsNullOrWhiteSpace(narration))
            {
                throw new InvalidOperationException(
                    "Reference and Narration are required.");
            }

            using var con = _factory.CreateOpen();
            using var tx = con.BeginTransaction();

            try
            {
                // 2. Validate Accounting Year

                ValidateAccountingYear(
                    con, tx, yearId, saleDate);

                // 3. Verify Broker Account exists

                using (var cmd = con.CreateCommand())
                {
                    cmd.Transaction = tx;

                    cmd.CommandText =
                        "SELECT COUNT(*) FROM Chart WHERE AccountId = ?";

                    cmd.Parameters.AddWithValue(
                        "@p1", brokerAccountId);

                    if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                        throw new InvalidOperationException(
                            "Broker Account does not exist.");
                }

                // 4. Calculate FIFO within transaction

                var allocations = AllocateFIFOForPosting(
                    shareId,
                    saleDate,
                    quantity,
                    tx);

                // 5. Generate ONE UNIX for entire sale

                int unix = checked((int)_repo.GetNextUnix(tx));

                decimal totalDebit = 0m;
                decimal totalCredit = 0m;

                // 6. Process each FIFO allocation
                // STEP 5H.7 - Correct total proceeds for fractional sales

                decimal expectedSaleProceeds = decimal.Round(
                    quantity * sellingPrice,
                    2,
                    MidpointRounding.AwayFromZero);

                decimal allocatedSaleProceeds = 0m;
                for (int i = 0; i < allocations.Count; i++)
                {
                    var allocation = allocations[i];

                    var lot = allocation.Lot;

                    decimal qtySold = allocation.QtySold;

                    decimal purchaseCost = decimal.Round(
                        qtySold * lot.PurchaseRate,
                        2,
                        MidpointRounding.AwayFromZero);

                    // Calculate sale proceeds for this FIFO allocation

                    decimal saleProceeds = decimal.Round(
                        qtySold * sellingPrice,
                        2,
                        MidpointRounding.AwayFromZero);

                    // Last FIFO allocation absorbs the rounding difference

                    if (qtySold == lot.Balance)
                    {
                        purchaseCost = GetRemainingRecordedCost(
                            shareId,
                            lot,
                            tx);
                    }

                    if (i == allocations.Count - 1)
                    {
                        saleProceeds =
                            expectedSaleProceeds - allocatedSaleProceeds;
                    }

                    // Safety check

                    if (saleProceeds < 0m)
                    {
                        throw new InvalidOperationException(
                            "Invalid FIFO rounding adjustment. " +
                            "Final allocation proceeds cannot be negative.");
                    }

                    allocatedSaleProceeds += saleProceeds;

                    // IMPORTANT: Calculate gain/loss AFTER rounding adjustment

                    decimal gainLoss = saleProceeds - purchaseCost;

                    // Identify Capital Gain/Loss classification

                    string classification =
                        _shareSaleService.ClassifyCapitalGain(
                            lot.PurchaseDate,
                            saleDate,
                            gainLoss);
                    // STEP 5F - Detailed Share Sale Narration

                    // Detailed narration for each FIFO purchase lot

                    string detailedNarration =
                        $"{narration.Trim()} | " +
                        $"Purchase Lot: {lot.PurchaseTransId} | " +
                        $"Purchase Date: {lot.PurchaseDate:dd-MMM-yyyy} | " +
                        $"Qty: {qtySold:0.######} | " +
                        $"Purchase Rate: {lot.PurchaseRate:N2} | " +
                        $"Selling Rate: {sellingPrice:N2} | " +
                        $"Cost: {purchaseCost:N2} | " +
                        $"Proceeds: {saleProceeds:N2} | " +
                        $"{classification}: {gainLoss:N2}";

                    if (lot.SharesAtCostAccountId == brokerAccountId)
                    {
                        throw new InvalidOperationException(
                            "Broker Account cannot be the " +
                            "Shares-at-Cost Account.");
                    }

                    // 7. DEBIT Broker - Sale Proceeds

                    var brokerDebit = new GeneralLedger
                    {
                        AccountId = brokerAccountId,
                        YearId = yearId,
                        Unix = unix,
                        Tdate = saleDate.Date,
                        Ref = reference.Trim(),
                        Particulars = "Share Sale Proceeds",
                        Amount = saleProceeds,
                        TransactionType = "SS",
                        Narration = detailedNarration
                    };

                    _repo.InsertGeneralLedger(
                        brokerDebit, tx);

                    totalDebit += saleProceeds;

                    // 8. CREDIT Shares-at-Cost

                    var sharesCredit = new GeneralLedger
                    {
                        AccountId = lot.SharesAtCostAccountId,
                        YearId = yearId,
                        Unix = unix,
                        Tdate = saleDate.Date,
                        Ref = reference.Trim(),
                        Particulars = "Shares Sold at Original Cost",
                        Amount = -purchaseCost,
                        TransactionType = "SS",
                        Narration = detailedNarration
                    };

                    int sharesCreditId =
                        _repo.InsertGeneralLedger(
                            sharesCredit, tx);

                    totalCredit += purchaseCost;

                    // 9. Post Capital Gain or Loss

                    if (gainLoss != 0m)
                    {
                        int capitalAccountId =
                            GetCapitalAccountId(
                                classification,
                                ltcgAccountId,
                                stcgAccountId,
                                ltclAccountId,
                                stclAccountId);

                        if (capitalAccountId == brokerAccountId ||
                            capitalAccountId == lot.SharesAtCostAccountId)
                        {
                            throw new InvalidOperationException(
                                "Capital Gain/Loss account conflicts " +
                                "with Broker or Shares-at-Cost account.");
                        }

                        var capitalEntry = new GeneralLedger
                        {
                            AccountId = capitalAccountId,
                            YearId = yearId,
                            Unix = unix,
                            Tdate = saleDate.Date,
                            Ref = reference.Trim(),

                            Particulars =
                                $"Share Sale {classification}",

                            // Gain = Credit
                            // Loss = Debit

                            Amount = -gainLoss,

                            TransactionType = "SS",
                            Narration = detailedNarration
                        };

                        _repo.InsertGeneralLedger(
                            capitalEntry, tx);

                        if (gainLoss > 0)
                            totalCredit += gainLoss;
                        else
                            totalDebit += -gainLoss;
                    }

                    // 10. Record FIFO sale allocation

                    var shareSale = new SharesSales
                    {
                        ShareId = shareId,
                        PurchaseTransId = lot.PurchaseTransId,
                        QtySold = qtySold,
                        SellingPrice = sellingPrice,

                        // Link to Shares-at-Cost CREDIT entry

                        TransactionId = sharesCreditId,
                        Unix = unix
                    };

                    _repo.InsertShareSale(
                        shareSale, tx);
                }
                // Verify sale proceeds match the complete transaction

                if (allocatedSaleProceeds != expectedSaleProceeds)
                {
                    throw new InvalidOperationException(
                        "FIFO sale proceeds do not match total sale value.\n\n" +
                        $"Expected: {expectedSaleProceeds:N2}\n" +
                        $"Allocated: {allocatedSaleProceeds:N2}");
                }

                // 11. Final accounting balance check

                if (totalDebit != totalCredit)
                {
                    throw new InvalidOperationException(
                        "Share Sale accounting does not balance.\n\n" +
                        $"Debit: {totalDebit:N2}\n" +
                        $"Credit: {totalCredit:N2}");
                }

                // 12. Commit complete transaction

                tx.Commit();

                return unix;
            }
            catch
            {
                try
                {
                    tx.Rollback();
                }
                catch
                {
                    // Preserve original exception.
                }

                throw;
            }
        }
        // STEP 5H.10D
        // Retrieve the remaining recorded cost of a purchase lot.

        private static decimal GetRemainingRecordedCost(
            int shareId,
            ShareSaleLot lot,
            OleDbTransaction tx)
        {
            var con = tx.Connection
                ?? throw new InvalidOperationException(
                    "Database transaction is not active.");

            decimal originalCost;

            // 1. Retrieve original purchase GL amount.

            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;

                cmd.CommandText =
                    """
            SELECT gl.Amount, gl.AccountId
            FROM SharePurchases AS sp
            INNER JOIN GeneralLedger AS gl
                ON sp.TransactionId = gl.TransactionId
            WHERE sp.PurchaseTransId = ?
              AND sp.ShareId = ?
            """;

                cmd.Parameters.AddWithValue(
                    "@p1", lot.PurchaseTransId);

                cmd.Parameters.AddWithValue(
                    "@p2", shareId);

                using var reader = cmd.ExecuteReader();

                if (reader == null || !reader.Read())
                    throw new InvalidOperationException(
                        "Original purchase transaction not found.");

                int accountId =
                    Convert.ToInt32(reader["AccountId"]);

                if (accountId != lot.SharesAtCostAccountId)
                    throw new InvalidOperationException(
                        "Original purchase GL account mismatch.");

                originalCost = decimal.Round(
                    Convert.ToDecimal(reader["Amount"]),
                    2,
                    MidpointRounding.AwayFromZero);

                if (originalCost < 0m)
                    throw new InvalidOperationException(
                        "Invalid original purchase cost.");
            }

            // 2. Retrieve the cost removed by previous sales.

            decimal costAlreadyRemoved = 0m;

            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;

                cmd.CommandText =
                    """
            SELECT gl.Amount, gl.AccountId
            FROM ShareSales AS ss
            INNER JOIN GeneralLedger AS gl
                ON ss.TransactionId = gl.TransactionId
            WHERE ss.PurchaseTransId = ?
              AND ss.ShareId = ?
            """;

                cmd.Parameters.AddWithValue(
                    "@p1", lot.PurchaseTransId);

                cmd.Parameters.AddWithValue(
                    "@p2", shareId);

                using var reader = cmd.ExecuteReader();

                while (reader != null && reader.Read())
                {
                    int accountId =
                        Convert.ToInt32(reader["AccountId"]);

                    decimal amount = decimal.Round(
                        Convert.ToDecimal(reader["Amount"]),
                        2,
                        MidpointRounding.AwayFromZero);

                    if (accountId != lot.SharesAtCostAccountId ||
                        amount > 0m)
                    {
                        throw new InvalidOperationException(
                            "Invalid previous share sale cost entry.");
                    }

                    // Sales are credits (negative GL amounts).
                    costAlreadyRemoved -= amount;
                }
            }

            // 3. Calculate exact remaining recorded cost.

            decimal remainingCost =
                originalCost - costAlreadyRemoved;

            if (remainingCost < 0m)
                throw new InvalidOperationException(
                    "Remaining purchase cost cannot be negative.");

            return remainingCost;
        }
    }
}