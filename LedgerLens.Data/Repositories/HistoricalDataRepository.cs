using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace LedgerLens.Data.Repositories
{
    public sealed class HistoricalDataRepository
    {
        // STEP 7D.5
        // Insert HistoricalData using the caller's transaction.
        // The balances must be captured BEFORE closing entries.

        public void InsertSnapshot(
            int yearId,
            DateTime startDate,
            DateTime endDate,
            IReadOnlyList<(int AccountId, decimal Balance)> balances,
            OleDbTransaction tx)
        {
            if (tx?.Connection == null)
                throw new InvalidOperationException(
                    "An active transaction is required.");

            if (yearId <= 0)
                throw new InvalidOperationException(
                    "Invalid accounting year.");

            if (balances == null || balances.Count == 0)
                throw new InvalidOperationException(
                    "No historical balances to save.");

            if (balances.Any(x => x.AccountId <= 0))
                throw new InvalidOperationException(
                    "Invalid historical account.");

            if (balances.Select(x => x.AccountId).Distinct().Count()
                != balances.Count)
                throw new InvalidOperationException(
                    "Duplicate accounts in historical snapshot.");

            // Prevent duplicate historical snapshots.

            using (var check = tx.Connection.CreateCommand())
            {
                check.Transaction = tx;

                check.CommandText =
                    "SELECT COUNT(*) FROM HistoricalData " +
                    "WHERE YearId = ?";

                check.Parameters.AddWithValue("@p1", yearId);

                int existing =
                    Convert.ToInt32(check.ExecuteScalar());

                if (existing > 0)
                    throw new InvalidOperationException(
                        "HistoricalData already exists for this year.");
            }

            // Insert all balances within the same transaction.

            using var cmd = tx.Connection.CreateCommand();

            cmd.Transaction = tx;

            cmd.CommandText =
                """
                INSERT INTO HistoricalData
                    ([YearId], [StartDate], [EndDate],
                     [AccountId], [Balance])
                VALUES (?, ?, ?, ?, ?)
                """;

            cmd.Parameters.Add(
                new OleDbParameter { OleDbType = OleDbType.Integer });

            cmd.Parameters.Add(
                new OleDbParameter { OleDbType = OleDbType.Date });

            cmd.Parameters.Add(
                new OleDbParameter { OleDbType = OleDbType.Date });

            cmd.Parameters.Add(
                new OleDbParameter { OleDbType = OleDbType.Integer });

            cmd.Parameters.Add(
                new OleDbParameter
                {
                    OleDbType = OleDbType.Decimal,
                    Precision = 18,
                    Scale = 2
                });

            foreach (var row in balances)
            {
                cmd.Parameters[0].Value = yearId;
                cmd.Parameters[1].Value = startDate.Date;
                cmd.Parameters[2].Value = endDate.Date;
                cmd.Parameters[3].Value = row.AccountId;
                cmd.Parameters[4].Value = row.Balance;

                cmd.ExecuteNonQuery();
            }
        }
    }
}