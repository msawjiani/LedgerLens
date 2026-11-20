using LedgerLens.Data.Abstractions;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace LedgerLens.Services
{
    public sealed class ShareSaleLot
    {
        public int PurchaseTransId { get; set; }
        public int TransactionId { get; set; }
        public DateTime PurchaseDate { get; set; }

        public decimal QtyPurchased { get; set; }
        public decimal QtySold { get; set; }
        public decimal Balance { get; set; }

        public int SharesAtCostAccountId { get; set; }

        public decimal PurchaseRate { get; set; }
    }

    public sealed class ShareSaleService
    {
        private readonly IConnectionFactory _factory;

        public ShareSaleService(IConnectionFactory factory)
        {
            _factory = factory;
        }

        // STEP 3A - Retrieve available purchase lots
        public int SharesAtCostAccountId { get; set; }

        public List<ShareSaleLot> GetAvailableLots(int shareId)
        {
            if (shareId <= 0)
                throw new ArgumentException(
                    "Invalid ShareId.");

            var lots = new List<ShareSaleLot>();

            using var con = _factory.CreateOpen();

            // 1. Retrieve original purchases

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText =
                  """
                SELECT
                    sp.PurchaseTransId,
                    sp.TransactionId,
                    sp.PurchaseDate,
                    sp.QtyPurchased,
                    sp.PurchaseRate,
                    gl.AccountId AS SharesAtCostAccountId
                FROM
                    SharePurchases AS sp
                INNER JOIN
                    GeneralLedger AS gl
                    ON sp.TransactionId = gl.TransactionId
                WHERE
                    sp.ShareId = ?
                ORDER BY
                    sp.PurchaseDate,
                    sp.PurchaseTransId
                """;

                cmd.Parameters.AddWithValue("@p1", shareId);

                using var reader = cmd.ExecuteReader();

                while (reader != null && reader.Read())
                {
                    lots.Add(new ShareSaleLot
                    {
                        PurchaseTransId =
                            Convert.ToInt32(reader["PurchaseTransId"]),

                        TransactionId =
                            Convert.ToInt32(reader["TransactionId"]),

                        PurchaseDate =
                            Convert.ToDateTime(reader["PurchaseDate"]),

                        QtyPurchased =
                            Convert.ToDecimal(reader["QtyPurchased"]),

                        SharesAtCostAccountId =
                            Convert.ToInt32(reader["SharesAtCostAccountId"]),

                        PurchaseRate =
                            Convert.ToDecimal(reader["PurchaseRate"])
                    });
                }
            }

            // 2. Retrieve quantities already sold

            var soldQuantities = new Dictionary<int, decimal>();

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText =
                    """
                    SELECT PurchaseTransId,
                           SUM(QtySold) AS TotalSold
                    FROM ShareSales
                    WHERE ShareId = ?
                    GROUP BY PurchaseTransId
                    """;

                cmd.Parameters.AddWithValue("@p1", shareId);

                using var reader = cmd.ExecuteReader();

                while (reader != null && reader.Read())
                {
                    int purchaseTransId =
                        Convert.ToInt32(reader["PurchaseTransId"]);

                    decimal qtySold =
                        Convert.ToDecimal(reader["TotalSold"]);

                    soldQuantities[purchaseTransId] = qtySold;
                }
            }

            // 3. Calculate available balance for each lot

            foreach (var lot in lots)
            {
                lot.QtySold = soldQuantities.TryGetValue(
                    lot.PurchaseTransId,
                    out decimal sold)
                    ? sold
                    : 0m;

                lot.Balance = lot.QtyPurchased - lot.QtySold;

                if (lot.Balance < 0)
                {
                    throw new InvalidOperationException(
                        $"Negative share balance detected for " +
                        $"PurchaseTransId {lot.PurchaseTransId}.");
                }
            }

            // 4. Return available lots in FIFO order

            return lots
                .Where(x => x.Balance > 0)
                .OrderBy(x => x.PurchaseDate)
                .ThenBy(x => x.PurchaseTransId)
                .ToList();
        }
        // STEP 3C - Allocate Share Sales using FIFO


        // STEP 5D.1 - Retrieve FIFO Lots Within Existing Transaction

        public List<ShareSaleLot> GetAvailableLots(
            int shareId,
            OleDbTransaction tx)
        {
            if (shareId <= 0)
                throw new InvalidOperationException(
                    "Invalid ShareId.");

            var lots = new List<ShareSaleLot>();

            var con = tx.Connection
                ?? throw new InvalidOperationException(
                    "Database transaction is not active.");

            // 1. Retrieve purchase lots and original GL AccountId

            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;

                cmd.CommandText =
                    """
            SELECT
                sp.PurchaseTransId,
                sp.TransactionId,
                sp.PurchaseDate,
                sp.QtyPurchased,
                sp.PurchaseRate,
                gl.AccountId AS SharesAtCostAccountId
            FROM SharePurchases AS sp
            INNER JOIN GeneralLedger AS gl
                ON sp.TransactionId = gl.TransactionId
            WHERE sp.ShareId = ?
            ORDER BY sp.PurchaseDate, sp.PurchaseTransId
            """;

                cmd.Parameters.AddWithValue("@p1", shareId);

                using var reader = cmd.ExecuteReader();

                while (reader != null && reader.Read())
                {
                    lots.Add(new ShareSaleLot
                    {
                        PurchaseTransId =
                            Convert.ToInt32(reader["PurchaseTransId"]),

                        TransactionId =
                            Convert.ToInt32(reader["TransactionId"]),

                        PurchaseDate =
                            Convert.ToDateTime(reader["PurchaseDate"]),

                        QtyPurchased =
                            Convert.ToDecimal(reader["QtyPurchased"]),

                        PurchaseRate =
                            Convert.ToDecimal(reader["PurchaseRate"]),

                        SharesAtCostAccountId =
                            Convert.ToInt32(reader["SharesAtCostAccountId"])
                    });
                }
            }

            // 2. Retrieve quantities already sold

            var soldQuantities = new Dictionary<int, decimal>();

            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;

                cmd.CommandText =
                    """
            SELECT
                PurchaseTransId,
                SUM(QtySold) AS TotalSold
            FROM ShareSales
            WHERE ShareId = ?
            GROUP BY PurchaseTransId
            """;

                cmd.Parameters.AddWithValue("@p1", shareId);

                using var reader = cmd.ExecuteReader();

                while (reader != null && reader.Read())
                {
                    int purchaseTransId =
                        Convert.ToInt32(reader["PurchaseTransId"]);

                    decimal qtySold =
                        Convert.ToDecimal(reader["TotalSold"]);

                    soldQuantities[purchaseTransId] = qtySold;
                }
            }

            // 3. Calculate remaining quantities

            foreach (var lot in lots)
            {
                lot.QtySold = soldQuantities.TryGetValue(
                    lot.PurchaseTransId,
                    out decimal sold)
                    ? sold
                    : 0m;

                lot.Balance =
                    lot.QtyPurchased - lot.QtySold;

                if (lot.Balance < 0)
                {
                    throw new InvalidOperationException(
                        $"Negative share balance detected for " +
                        $"PurchaseTransId {lot.PurchaseTransId}.");
                }
            }

            // 4. Return available FIFO lots

            return lots
                .Where(x => x.Balance > 0)
                .OrderBy(x => x.PurchaseDate)
                .ThenBy(x => x.PurchaseTransId)
                .ToList();
        }

        public List<(ShareSaleLot Lot, decimal QtySold)> AllocateFIFO(
            int shareId,
            DateTime saleDate,
            decimal quantityToSell)
        {
            if (quantityToSell <= 0)
                throw new InvalidOperationException(
                    "Sale quantity must be greater than zero.");

            var lots = GetAvailableLots(shareId)
                .Where(x => x.PurchaseDate.Date <= saleDate.Date)
                .ToList();

            decimal totalAvailable = lots.Sum(x => x.Balance);

            if (quantityToSell > totalAvailable)
                throw new InvalidOperationException(
                    $"Insufficient shares available.\n\n" +
                    $"Requested: {quantityToSell}\n" +
                    $"Available: {totalAvailable}");

            var allocations =
                new List<(ShareSaleLot Lot, decimal QtySold)>();

            decimal remaining = quantityToSell;

            foreach (var lot in lots)
            {
                if (remaining == 0)
                    break;

                decimal allocated = Math.Min(lot.Balance, remaining);

                allocations.Add((lot, allocated));

                remaining -= allocated;
            }

            return allocations;
        }
        // STEP 4A - Calculate Share Sale Details

        public List<(ShareSaleLot Lot,
                     decimal QtySold,
                     decimal PurchaseCost,
                     decimal SaleProceeds,
                     decimal GainLoss)> CalculateSaleDetails(
            int shareId,
            DateTime saleDate,
            decimal quantityToSell,
            decimal sellingPrice)
        {
            if (sellingPrice <= 0)
                throw new InvalidOperationException(
                    "Selling price must be greater than zero.");

            var allocations = AllocateFIFO(
                shareId,
                saleDate,
                quantityToSell);

            return allocations.Select(x =>
            {
                decimal purchaseCost =
                    x.QtySold * x.Lot.PurchaseRate;

                decimal saleProceeds =
                    x.QtySold * sellingPrice;

                decimal gainLoss =
                    saleProceeds - purchaseCost;

                return (
                    x.Lot,
                    x.QtySold,
                    purchaseCost,
                    saleProceeds,
                    gainLoss
                );
            }).ToList();
        }
        // STEP 4D - Classify Capital Gains / Losses
        // Applicable to listed Indian equity shares.

        public string ClassifyCapitalGain(
            DateTime purchaseDate,
            DateTime saleDate,
            decimal gainLoss)
        {
            if (saleDate.Date < purchaseDate.Date)
                throw new InvalidOperationException(
                    "Sale date cannot precede purchase date.");

            // Listed equity becomes long-term
            // when held for more than 12 calendar months.

            bool isLongTerm =
                saleDate.Date > purchaseDate.Date.AddMonths(12);

            if (isLongTerm)
            {
                return gainLoss < 0 ? "LTCL" : "LTCG";
            }
            else
            {
                return gainLoss < 0 ? "STCL" : "STCG";
            }
        }
    }
}