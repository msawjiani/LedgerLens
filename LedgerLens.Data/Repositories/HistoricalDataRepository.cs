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

                int existing;

                try
                {
                    existing = Convert.ToInt32(check.ExecuteScalar());
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"HistoricalData duplicate check FAILED. YearId: {yearId}\n" +
                        ex.Message, ex);
                }

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
                    OleDbType = OleDbType.Currency
                });

            foreach (var row in balances)
            {
                cmd.Parameters[0].Value = yearId;
                cmd.Parameters[1].Value = startDate.Date;
                cmd.Parameters[2].Value = endDate.Date;
                cmd.Parameters[3].Value = row.AccountId;
                cmd.Parameters[4].Value = row.Balance;

                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "HistoricalData INSERT FAILED.\n\n" +
                        $"YearId: {yearId}\n" +
                        $"AccountId: {row.AccountId}\n" +
                        $"Balance: {row.Balance:N2}\n\n" +
                        $"Original Error: {ex.Message}",
                        ex);
                }
            }
        }
    }
}