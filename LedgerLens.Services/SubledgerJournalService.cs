using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;
using System;
using System.Data.OleDb;
using System.Globalization;

namespace LedgerLens.Services
{
    public sealed class SubledgerJournalService
    {
        private readonly IConnectionFactory _factory;
        private readonly ITransactionRepository _repo;

        public SubledgerJournalService(
            IConnectionFactory factory,
            ITransactionRepository repo)
        {
            _factory = factory;
            _repo = repo;
        }

        public long PostSubledgerJournal(
            int yearId,
            DateTime date,
            string reference,
            string narration,
            decimal amount,
            LedgerAccount debitAccount,
            LedgerAccount creditAccount,
            Subledger subledger)
        {
            // Basic validation

            ArgumentNullException.ThrowIfNull(debitAccount);
            ArgumentNullException.ThrowIfNull(creditAccount);
            ArgumentNullException.ThrowIfNull(subledger);

            if (yearId <= 0)
                throw new InvalidOperationException(
                    "Invalid accounting year.");

            if (amount <= 0)
                throw new InvalidOperationException(
                    "Journal amount must be positive.");

            if (string.IsNullOrWhiteSpace(reference))
                throw new InvalidOperationException(
                    "Reference is required.");

            if (string.IsNullOrWhiteSpace(narration))
                throw new InvalidOperationException(
                    "Narration is required.");

            if (debitAccount.AccountId == creditAccount.AccountId)
                throw new InvalidOperationException(
                    "Debit and Credit accounts cannot be identical.");

            if (subledger.AccountId != debitAccount.AccountId)
                throw new InvalidOperationException(
                    "Selected subledger does not belong to Debit GL.");

            using var con = _factory.CreateOpen();
            using var tx = con.BeginTransaction();

            try
            {
                // 1. Validate latest accounting year

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
                            "Posting is permitted only in the latest accounting year.");
                    }
                }

                // 2. Validate financial-year dates

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

                    if (date.Date < startDate ||
                        date.Date > endDate)
                    {
                        throw new InvalidOperationException(
                            "Journal date is outside the selected financial year.");
                    }
                }

                // 3. Verify Debit and Credit GL eligibility

                var debitInfo = GetAccountInfo(
                    con, tx, debitAccount.AccountId);

                var creditInfo = GetAccountInfo(
                    con, tx, creditAccount.AccountId);

                if (IsOrdinaryAccount(debitInfo.Flag))
                    throw new InvalidOperationException(
                        "Debit GL must be a subledger-controlled account.");

                if (!IsOrdinaryAccount(creditInfo.Flag) ||
                    creditInfo.Category.Equals(
                        "BANK",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Credit GL must be an ordinary non-bank account.");
                }

                // 4. Verify Subledger belongs to Debit GL

                using (var cmd = con.CreateCommand())
                {
                    cmd.Transaction = tx;

                    cmd.CommandText =
                        """
                        SELECT COUNT(*)
                        FROM SubledgerChart
                        WHERE SubledgerId = ?
                          AND AccountId = ?
                          AND Active = 'Y'
                        """;

                    cmd.Parameters.AddWithValue(
                        "@p1", subledger.SubledgerId);

                    cmd.Parameters.AddWithValue(
                        "@p2", debitAccount.AccountId);

                    int count =
                        Convert.ToInt32(cmd.ExecuteScalar());

                    if (count != 1)
                        throw new InvalidOperationException(
                            "The selected subledger is inactive or " +
                            "does not belong to the selected Debit GL.");
                }

                // 5. Generate one common UNIX

                int unix = checked((int)_repo.GetNextUnix(tx));

                string plusAmount =
                    amount.ToString("N2", CultureInfo.InvariantCulture);

                // 6. Debit GeneralLedger

                var debit = new GeneralLedger
                {
                    AccountId = debitAccount.AccountId,
                    YearId = yearId,
                    Unix = unix,
                    Tdate = date.Date,
                    Ref = reference.Trim(),
                    Particulars =
                        $"To {creditAccount.Account} (+{plusAmount})",
                    Amount = amount,
                    TransactionType = "SJ",
                    Narration = narration.Trim()
                };

                // 7. Credit GeneralLedger

                var credit = new GeneralLedger
                {
                    AccountId = creditAccount.AccountId,
                    YearId = yearId,
                    Unix = unix,
                    Tdate = date.Date,
                    Ref = reference.Trim(),
                    Particulars =
                        $"By {debitAccount.Account} (-{plusAmount})",
                    Amount = -amount,
                    TransactionType = "SJ",
                    Narration = narration.Trim()
                };

                int debitId =
                    _repo.InsertGeneralLedger(debit, tx);

                _repo.InsertGeneralLedger(credit, tx);

                // 8. Link Subledger to DEBIT TransactionId

                _repo.InsertSubledgerTrans(
                    new SubledgerTrans
                    {
                        SubledgerId = subledger.SubledgerId,
                        Unix = unix,
                        TransactionId = debitId
                    },
                    tx);

                // 9. Commit all three records

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
                    // Preserve the original exception.
                }

                throw;
            }
        }

        private static (string Flag, string Category)
            GetAccountInfo(
                OleDbConnection con,
                OleDbTransaction tx,
                int accountId)
        {
            using var cmd = con.CreateCommand();

            cmd.Transaction = tx;

            cmd.CommandText =
                """
                SELECT SubledgerFlag, Category
                FROM Chart
                WHERE AccountId = ?
                """;

            cmd.Parameters.AddWithValue("@p1", accountId);

            using var reader = cmd.ExecuteReader();

            if (reader == null || !reader.Read())
                throw new InvalidOperationException(
                    $"Ledger Account {accountId} not found.");

            return (
                Convert.ToString(reader["SubledgerFlag"])?.Trim() ?? "",
                Convert.ToString(reader["Category"])?.Trim() ?? ""
            );
        }

        private static bool IsOrdinaryAccount(string? flag)
        {
            string value = (flag ?? "")
                .Trim()
                .ToUpperInvariant();

            return value is "" or "N" or "NO" or "NONE" or "0";
        }
    }
}