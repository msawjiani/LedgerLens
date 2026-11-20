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
                    SELECT PurchaseTransId,
                           TransactionId,
                           PurchaseDate,
                           QtyPurchased,
                           PurchaseRate
                    FROM SharePurchases
                    WHERE ShareId = ?
                    ORDER BY PurchaseDate, PurchaseTransId
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
    }
}