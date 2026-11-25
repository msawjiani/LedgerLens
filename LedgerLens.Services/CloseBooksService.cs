using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Repositories;
using System.Data.OleDb;

using LedgerLens.Data.Models;


using System.Collections.Generic;
using System.Linq;


namespace LedgerLens.Services
{
    public sealed class CloseBooksService
    {
        private readonly AccountYearRepository _yearRepository;
        
        private readonly IConnectionFactory _connectionFactory;

        public CloseBooksService(IConnectionFactory factory)
        {
            _yearRepository = new AccountYearRepository(factory);
            _connectionFactory = factory;
        }

        public sealed record ClosingValidationSnapshot(
            decimal TrialBalanceDifference,
            int UnbalancedUnixCount,
            decimal RetainedEarningsBalance,
            int ExistingClosingEntries,
            string Status);

        // STEP 7A.2 - Read-only accounting year validation

        public string GetClosingDiagnostic(
            int selectedYearId,
            int retainedEarningsAccountId,
            Action<ClosingValidationSnapshot>? onSnapshot = null)
        {
            var years = _yearRepository.GetAccountingYears();

            if (years.Count == 0)
                throw new InvalidOperationException(
                    "No accounting years found.");

            var selectedYear = years.FirstOrDefault(
                y => y.YearId == selectedYearId);

            if (selectedYear == null)
                throw new InvalidOperationException(
                    "Selected accounting year not found.");

            var latestYear = years.Last();

            bool isLatestYear =
                selectedYear.YearId == latestYear.YearId;

            bool yearHasEnded =
                DateTime.Today.Date > selectedYear.EndDate.Date;

            string status;

            if (!isLatestYear)
            {
                status =
                    "NOT ELIGIBLE - Selected year is not the latest.";
            }
            else if (!yearHasEnded)
            {
                status =
                    "NOT ELIGIBLE - Financial year has not ended.";
            }
            else if (retainedEarningsAccountId <= 0)
            {
                status =
                    "NOT ELIGIBLE - Retained Earnings account is not configured.";
            }
            else
            {
                status =
                    "BASIC CHECKS PASSED - Further validation required.";
            }
            // STEP 7B.1 - Cumulative Trial Balance
            // READ ONLY - Uses existing Access query

            int accountCount;
            decimal trialBalanceDifference;

            using (var connection = _connectionFactory.CreateOpen())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT COUNT(*), SUM([Total]) " +
                    "FROM qryTrialBalance";

                using var reader = command.ExecuteReader();

                if (reader == null || !reader.Read())
                    throw new InvalidOperationException(
                        "Unable to read cumulative Trial Balance.");

                accountCount = Convert.ToInt32(reader.GetValue(0));

                trialBalanceDifference = reader.IsDBNull(1)
                    ? 0m
                    : Convert.ToDecimal(reader.GetValue(1));
            }

            if (accountCount == 0)
            {
                status = "NOT ELIGIBLE - Trial Balance contains no accounts.";
            }
            else if (trialBalanceDifference != 0m)
            {
                status = "NOT ELIGIBLE - Cumulative Trial Balance is not balanced.";
            }


            // STEP 7B.2 - Verify Retained Earnings Account
            // READ ONLY

            string reAccountCheck = "NOT CONFIGURED";

            if (retainedEarningsAccountId > 0)
            {
                using var connection = _connectionFactory.CreateOpen();
                using var command = connection.CreateCommand();

                command.CommandText =
                    "SELECT Category FROM Chart WHERE AccountId = ?";

                command.Parameters.AddWithValue(
                    "@p1",
                    retainedEarningsAccountId);

                object? result = command.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                {
                    reAccountCheck = "ACCOUNT NOT FOUND";

                    status =
                        "NOT ELIGIBLE - Retained Earnings account not found.";
                }
                else if (Convert.ToString(result)?.Trim() != "RE")
                {
                    reAccountCheck = "INVALID CATEGORY";

                    status =
                        "NOT ELIGIBLE - Retained Earnings category must be RE.";
                }
                else
                {
                    reAccountCheck = "VERIFIED (RE)";
                }
            }
            // STEP 7B.3 - Validate every UNIX
            // READ ONLY - Across ALL accounting years

            int unbalancedUnixCount = 0;
            string firstUnbalancedUnix = "";

            using (var connection = _connectionFactory.CreateOpen())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT [Unix], SUM([Amount]) AS UnixBalance " +
                    "FROM GeneralLedger " +
                    "GROUP BY [Unix]";

                using var reader = command.ExecuteReader();

                if (reader == null)
                    throw new InvalidOperationException(
                        "Unable to validate UNIX balances.");

                while (reader.Read())
                {
                    decimal balance = reader.IsDBNull(1)
                        ? 0m
                        : Convert.ToDecimal(reader.GetValue(1));

                    bool missingUnix = reader.IsDBNull(0);

                    if (!missingUnix && Math.Abs(balance) <= 0.005m)
                        continue;

                    unbalancedUnixCount++;

                    if (firstUnbalancedUnix == "")
                    {
                        firstUnbalancedUnix = missingUnix
                            ? "NULL"
                            : Convert.ToString(reader.GetValue(0)) ?? "";
                    }
                }
            }

            if (unbalancedUnixCount > 0)
            {
                status =
                    "NOT ELIGIBLE - Unbalanced or missing UNIX detected.";
            }
            // STEP 7B.4 - Check existing Year-End entries
            // READ ONLY

            int existingClosingEntries;

            using (var connection = _connectionFactory.CreateOpen())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT COUNT(*) FROM GeneralLedger " +
                    "WHERE YearId = ? " +
                    "AND [Tdate] >= ? " +
                    "AND [Tdate] < ? " +
                    "AND [TransactionType] IN ('R*', 'C*', 'E*')";

                command.Parameters.AddWithValue(
                    "@p1", selectedYear.YearId);

                command.Parameters.AddWithValue(
                    "@p2", selectedYear.EndDate.Date);

                command.Parameters.AddWithValue(
                    "@p3", selectedYear.EndDate.Date.AddDays(1));

                existingClosingEntries =
                    Convert.ToInt32(command.ExecuteScalar());
            }

            if (existingClosingEntries > 0)
            {
                status =
                    "NOT ELIGIBLE - Existing Year-End entries detected.";
            }

            // STEP 7C.1 - Closing Books Category Summary
            // READ ONLY - Uses cumulative qryTrialBalance

            string categorySummary = "";
            decimal netPLBalance = 0m;

            using (var connection = _connectionFactory.CreateOpen())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT [Category], COUNT(*), SUM([Total]) " +
                    "FROM qryTrialBalance " +
                    "GROUP BY [Category] " +
                    "ORDER BY [Category]";

                using var reader = command.ExecuteReader();

                while (reader != null && reader.Read())
                {
                    string category = reader.IsDBNull(0)
                        ? "(NULL)"
                        : Convert.ToString(reader.GetValue(0))!.Trim();

                    int count = Convert.ToInt32(reader.GetValue(1));

                    decimal balance = reader.IsDBNull(2)
                        ? 0m
                        : Convert.ToDecimal(reader.GetValue(2));
                    if (category == "PL")
                    {
                        netPLBalance += balance;
                    }
                    categorySummary +=
                        $"{category}: {count} Accounts, Rs.{balance:N2}\n";
                }
            }
            // STEP 7C.5 - Verify existing Retained Earnings balance
            // READ ONLY - Cumulative across all years

            decimal retainedEarningsBalance;

            using (var connection = _connectionFactory.CreateOpen())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT SUM([Amount]) " +
                    "FROM GeneralLedger " +
                    "WHERE AccountId = ?";

                command.Parameters.AddWithValue(
                    "@p1",
                    retainedEarningsAccountId);

                object? result = command.ExecuteScalar();

                retainedEarningsBalance =
                    result == null || result == DBNull.Value
                        ? 0m
                        : Convert.ToDecimal(result);
            }

            if (retainedEarningsBalance != 0m)
            {
                status =
                    "NOT ELIGIBLE - Retained Earnings balance must be ZERO.";
            }
            // ============================================
            // KANAK GUARDRAIL
            // VERIFY REGULAR TRANSACTIONS EXIST
            // READ ONLY
            // ============================================

            int regularTransactionRows;

            using (var connection = _connectionFactory.CreateOpen())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM GeneralLedger
                    WHERE YearId = ?
                      AND [Tdate] >= ?
                      AND [Tdate] < ?
                      AND (
                          [TransactionType] IS NULL
                          OR [TransactionType] NOT IN ('R*', 'C*', 'E*')
                      )
                    """;

                command.Parameters.AddWithValue(
                    "@p1", selectedYear.YearId);

                command.Parameters.AddWithValue(
                    "@p2", selectedYear.StartDate.Date);

                command.Parameters.AddWithValue(
                    "@p3", selectedYear.EndDate.Date.AddDays(1));

                regularTransactionRows =
                    Convert.ToInt32(command.ExecuteScalar());
            }

            if (regularTransactionRows == 0)
            {
                status =
                    "NOT ELIGIBLE - No regular transactions exist " +
                    "in the selected financial year.";
            }
            onSnapshot?.Invoke(new ClosingValidationSnapshot(
                trialBalanceDifference,
                unbalancedUnixCount,
                retainedEarningsBalance,
                existingClosingEntries,
                status));
            return
                $"Selected YearId: {selectedYear.YearId}\n" +
                $"Financial Year: " +
                $"{selectedYear.StartDate:dd-MMM-yyyy} to " +
                $"{selectedYear.EndDate:dd-MMM-yyyy}\n\n" +

                $"Latest YearId: {latestYear.YearId}\n" +
                $"Latest Year Ends: {latestYear.EndDate:dd-MMM-yyyy}\n\n" +

                $"Retained Earnings AccountId: " +
                $"{retainedEarningsAccountId}\n" +
                $"Retained Earnings Account: {reAccountCheck}\n\n" +

                $"TRIAL BALANCE VALIDATION\n" +
                $"Accounts: {accountCount}\n" +
                $"Cumulative Difference: Rs.{trialBalanceDifference:N2}\n\n" +
               $"UNIX VALIDATION\n" +
                $"Unbalanced UNIX Count: {unbalancedUnixCount}\n" +
                $"First Problem UNIX: " +
                $"{(unbalancedUnixCount == 0 ? "NONE" : firstUnbalancedUnix)}\n\n" +

                $"YEAR-END VALIDATION\n" +
                $"Existing Closing Entries: {existingClosingEntries}\n\n" +

                $"CLOSING BOOKS CATEGORY SUMMARY\n" +
                $"{categorySummary}\n" +

                $"NET PROFIT / LOSS\n" +
                $"Net PL Balance: Rs.{netPLBalance:N2}\n" +
                $"Expected RE Opening Movement: Rs.{netPLBalance:N2}\n\n" +
                $"RETAINED EARNINGS VALIDATION\n" +
                $"Existing RE Balance: Rs.{retainedEarningsBalance:N2}\n\n" +

                $"CLOSING STATUS:\n{status}";
        }

        public decimal GetTrialBalanceDifference(
    int selectedYearId)
        {
            using var connection = _connectionFactory.CreateOpen();

            using var command = connection.CreateCommand();

            command.CommandText =
                "SELECT SUM(Amount) " +
                "FROM GeneralLedger " +
                "WHERE YearId = ?";

            command.Parameters.AddWithValue(
                "@p1",
                selectedYearId);

            object? result = command.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return 0m;

            return Convert.ToDecimal(result);
        }
        // STEP 7C.3 - Read-only Closing Entries Preview
        // No database modifications

        public string GetClosingEntriesPreview(
            int selectedYearId,
            int retainedEarningsAccountId)
        {
            var years = _yearRepository.GetAccountingYears();

            var year = years.FirstOrDefault(
                x => x.YearId == selectedYearId);

            if (year == null)
                throw new InvalidOperationException(
                    "Selected accounting year not found.");

            DateTime closingDate = year.EndDate.Date;
            DateTime openingDate = closingDate.AddDays(1);

            decimal netPL = 0m;

            var preview = new System.Text.StringBuilder();

            preview.AppendLine("CLOSING BOOKS - ENTRY PREVIEW");
            preview.AppendLine();

            using var connection = _connectionFactory.CreateOpen();
            using var command = connection.CreateCommand();

            command.CommandText =
                "SELECT AccountId, Account, Category, Total " +
                "FROM qryTrialBalance " +
                "ORDER BY AccountId";

            using var reader = command.ExecuteReader();

            while (reader != null && reader.Read())
            {
                int accountId = Convert.ToInt32(reader.GetValue(0));

                string account = Convert.ToString(
                    reader.GetValue(1)) ?? "";

                string category = Convert.ToString(
                    reader.GetValue(2))?.Trim() ?? "";

                decimal balance = reader.IsDBNull(3)
                    ? 0m
                    : Convert.ToDecimal(reader.GetValue(3));

                if (balance == 0m)
                    continue;

                if (category == "PL")
                {
                    netPL += balance;

                    preview.AppendLine(
                        $"{accountId} - {account}");

                    preview.AppendLine(
                        $"  CLOSE PL: {-balance:N2}");

                    preview.AppendLine(
                        $"  RE: {balance:N2}");

                    preview.AppendLine();
                }
                else if (category == "BANK" ||
                         category == "BS")
                {
                    preview.AppendLine(
                        $"{accountId} - {account}");

                    preview.AppendLine(
                        $"  CLOSE: {-balance:N2}");

                    preview.AppendLine(
                        $"  OPEN: {balance:N2}");

                    preview.AppendLine();
                }
            }

            preview.AppendLine("RETAINED EARNINGS TRANSFER");

            preview.AppendLine(
                $"AccountId: {retainedEarningsAccountId}");

            preview.AppendLine(
                $"Closing Date: {closingDate:dd-MMM-yyyy}");

            preview.AppendLine(
                $"Opening Date: {openingDate:dd-MMM-yyyy}");

            preview.AppendLine(
                $"Old Year RE Transfer: {-netPL:N2}");

            preview.AppendLine(
                $"New Year RE Opening: {netPL:N2}");

            preview.AppendLine();

            preview.AppendLine(
                "PREVIEW ONLY - NO DATABASE POSTING");

            return preview.ToString();
        }
        // STEP 7D.2 - Build Closing Books Entries
        // READ ONLY - No database modifications

        public List<ClosingEntryDraft> BuildClosingEntriesDraft(
            int selectedYearId,
            int retainedEarningsAccountId,
            OleDbTransaction? tx = null)
        {
            DateTime closingDate;

            if (tx == null)
            {
                var years = _yearRepository.GetAccountingYears();

                var year = years.FirstOrDefault(
                    y => y.YearId == selectedYearId);

                if (year == null)
                    throw new InvalidOperationException(
                        "Selected accounting year not found.");

                closingDate = year.EndDate.Date;
            }
            else
            {
                if (tx.Connection == null)
                    throw new InvalidOperationException(
                        "An active transaction is required.");

                using var yearCmd = tx.Connection.CreateCommand();

                yearCmd.Transaction = tx;

                yearCmd.CommandText =
                    "SELECT [EndDate] FROM AccountYears " +
                    "WHERE [YearId] = ?";

                yearCmd.Parameters.AddWithValue(
                    "@p1", selectedYearId);

                object? result = yearCmd.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                    throw new InvalidOperationException(
                        "Selected accounting year not found.");

                closingDate = Convert.ToDateTime(result).Date;
            }

            if (retainedEarningsAccountId <= 0)
                throw new InvalidOperationException(
                    "Retained Earnings account is not configured.");

            DateTime openingDate = closingDate.AddDays(1);
            var entries = new List<ClosingEntryDraft>();

            decimal netPL = 0m;

            // Local helper: construct an entry in memory only.

            void AddEntry(
                int accountId,
                ClosingEntryPeriod period,
                DateTime date,
                decimal amount,
                string transactionType,
                string reference,
                string particulars,
                string narration)
            {
                entries.Add(new ClosingEntryDraft
                {
                    AccountId = accountId,
                    Period = period,
                    Tdate = date,
                    Amount = amount,
                    TransactionType = transactionType,
                    Ref = reference,
                    Particulars = particulars,
                    Narration = narration
                });
            }

            using var ownConnection =
                tx == null ? _connectionFactory.CreateOpen() : null;

            using var command =
                (tx?.Connection ?? ownConnection
                    ?? throw new InvalidOperationException(
                        "Database connection is unavailable."))
                .CreateCommand();

            if (tx != null)
                command.Transaction = tx;

            command.CommandText =
                "SELECT AccountId, Account, Category, Total " +
                "FROM qryTrialBalance ORDER BY AccountId";

            using var reader = command.ExecuteReader();

            if (reader == null)
                throw new InvalidOperationException(
                    "Unable to read Trial Balance.");

            while (reader.Read())
            {
                int accountId = Convert.ToInt32(reader.GetValue(0));

                string account =
                    Convert.ToString(reader.GetValue(1)) ?? "";

                string category =
                    Convert.ToString(reader.GetValue(2))?.Trim() ?? "";

                decimal balance = reader.IsDBNull(3)
                    ? 0m
                    : Convert.ToDecimal(reader.GetValue(3));

                if (balance == 0m)
                    continue;

                if (category == "PL")
                {
                    netPL += balance;

                    // Reverse PL balance in closing year.

                    AddEntry(
                        accountId,
                        ClosingEntryPeriod.ClosingYear,
                        closingDate,
                        -balance,
                        "R*",
                        "-Closing Entry-",
                        "Closing PL to Retained Earnings",
                        $"Closing {account}");

                    // Transfer corresponding amount to RE.

                    AddEntry(
                        retainedEarningsAccountId,
                        ClosingEntryPeriod.ClosingYear,
                        closingDate,
                        balance,
                        "R*",
                        "-Closing Entry-",
                        $"Retained Earnings - {account}",
                        $"Transfer from {account}");
                }
                else if (category == "BANK" || category == "BS")
                {
                    // Close existing balance.

                    AddEntry(
                        accountId,
                        ClosingEntryPeriod.ClosingYear,
                        closingDate,
                        -balance,
                        "C*",
                        "-Closing Entry-",
                        "Balance c/d",
                        $"Closing balance - {account}");

                    // Restore balance in new financial year.

                    AddEntry(
                        accountId,
                        ClosingEntryPeriod.OpeningYear,
                        openingDate,
                        balance,
                        "C*",
                        "-Opening Entry-",
                        "Balance b/d",
                        $"Opening balance - {account}");
                }
                else if (category == "RE")
                {
                    throw new InvalidOperationException(
                        "Retained Earnings must be ZERO before closing.");
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unsupported account category: {category} " +
                        $"for AccountId {accountId}.");
                }
            }

            // Transfer net PL from old year into new year.

            if (netPL != 0m)
            {
                AddEntry(
                    retainedEarningsAccountId,
                    ClosingEntryPeriod.ClosingYear,
                    closingDate,
                    -netPL,
                    "E*",
                    "-Ret.Earns Entry-",
                    "Retained Earnings c/d",
                    "Transfer Retained Earnings to next year");

                AddEntry(
                    retainedEarningsAccountId,
                    ClosingEntryPeriod.OpeningYear,
                    openingDate,
                    netPL,
                    "E*",
                    "-Ret.Earns Entry-",
                    "Retained Earnings b/d",
                    "Opening Retained Earnings");
            }

            // Verify closing and opening entries independently.

            decimal closingTotal = entries
                .Where(x =>
                    x.Period == ClosingEntryPeriod.ClosingYear)
                .Sum(x => x.Amount);

            decimal openingTotal = entries
                .Where(x =>
                    x.Period == ClosingEntryPeriod.OpeningYear)
                .Sum(x => x.Amount);

            if (closingTotal != 0m || openingTotal != 0m)
            {
                throw new InvalidOperationException(
                    "Closing Books draft is NOT balanced.\n\n" +
                    $"Closing Difference: {closingTotal:N2}\n" +
                    $"Opening Difference: {openingTotal:N2}");
            }

            return entries;
        }
        // STEP 7D.8 - ATOMIC CLOSING BOOKS
// Entire operation uses ONE OleDb transaction.

public (
    int NewYearId,
    long Unix,
    int EntriesPosted,
    int HistoricalRows)
PostClosingBooks(
    int selectedYearId,
    int retainedEarningsAccountId)
{
            using var con = _connectionFactory.CreateOpen();
            using var tx = con.BeginTransaction();

            string stage = "Validating Closing Books";

            try
            {
        // ============================================
        // 1. VERIFY LATEST ACCOUNTING YEAR
        // ============================================

        DateTime startDate;
        DateTime endDate;

        using (var cmd = con.CreateCommand())
        {
            cmd.Transaction = tx;

            cmd.CommandText =
                """
                SELECT TOP 1 YearId, StartDate, EndDate
                FROM AccountYears
                ORDER BY EndDate DESC, YearId DESC
                """;

            using var reader = cmd.ExecuteReader();

            if (reader == null || !reader.Read())
                throw new InvalidOperationException(
                    "No accounting year found.");

            int latestYearId =
                Convert.ToInt32(reader["YearId"]);

            if (latestYearId != selectedYearId)
                throw new InvalidOperationException(
                    "Only the latest accounting year can be closed.");

            startDate =
                Convert.ToDateTime(reader["StartDate"]).Date;

            endDate =
                Convert.ToDateTime(reader["EndDate"]).Date;
        }

        if (DateTime.Today.Date <= endDate)
            throw new InvalidOperationException(
                "The financial year has not ended.");

        DateTime nextStart = endDate.AddDays(1);
        DateTime nextEnd = nextStart.AddYears(1).AddDays(-1);

        // ============================================
        // 2. PREVENT DUPLICATE CLOSING
        // ============================================

        using (var cmd = con.CreateCommand())
        {
            cmd.Transaction = tx;

            cmd.CommandText =
                """
                SELECT COUNT(*)
                FROM GeneralLedger
                WHERE YearId = ?
                  AND TDate >= ?
                  AND TDate < ?
                  AND TransactionType IN ('R*','C*','E*')
                """;

            cmd.Parameters.AddWithValue("@p1", selectedYearId);
            cmd.Parameters.AddWithValue("@p2", endDate);
            cmd.Parameters.AddWithValue(
                "@p3", endDate.AddDays(1));

            if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                throw new InvalidOperationException(
                    "Closing entries already exist for this year.");
        }
                // ============================================
                // KANAK GUARDRAIL
                // REQUIRE ACTUAL TRANSACTIONS IN CLOSING YEAR
                // ============================================

                using (var cmd = con.CreateCommand())
                {
                    cmd.Transaction = tx;

                    cmd.CommandText =
                        """
                        SELECT COUNT(*)
                        FROM GeneralLedger
                        WHERE YearId = ?
                          AND [Tdate] >= ?
                          AND [Tdate] < ?
                          AND (
                              [TransactionType] IS NULL
                              OR [TransactionType] NOT IN ('R*', 'C*', 'E*')
                          )
                        """;

                    cmd.Parameters.AddWithValue("@p1", selectedYearId);
                    cmd.Parameters.AddWithValue("@p2", startDate);
                    cmd.Parameters.AddWithValue("@p3", endDate.AddDays(1));

                    int actualTransactionRows =
                        Convert.ToInt32(cmd.ExecuteScalar());

                    if (actualTransactionRows == 0)
                    {
                        throw new InvalidOperationException(
                            "Closing Books is not permitted.\n\n" +
                            "No regular accounting transactions exist " +
                            "in the selected financial year.\n\n" +
                            "Opening entries alone do not qualify.");
                    }
                }

                // ============================================
                // 3. VERIFY RETAINED EARNINGS
                // ============================================

                using (var cmd = con.CreateCommand())
        {
            cmd.Transaction = tx;

            cmd.CommandText =
                "SELECT Category FROM Chart WHERE AccountId = ?";

            cmd.Parameters.AddWithValue(
                "@p1", retainedEarningsAccountId);

            string category =
                Convert.ToString(cmd.ExecuteScalar())?.Trim() ?? "";

            if (!category.Equals(
                    "RE", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Invalid Retained Earnings account.");
        }

        using (var cmd = con.CreateCommand())
        {
            cmd.Transaction = tx;

            cmd.CommandText =
                "SELECT SUM(Amount) FROM GeneralLedger WHERE AccountId = ?";

            cmd.Parameters.AddWithValue(
                "@p1", retainedEarningsAccountId);

            object? result = cmd.ExecuteScalar();

            decimal balance =
                result == null || result == DBNull.Value
                    ? 0m
                    : Convert.ToDecimal(result);

            if (balance != 0m)
                throw new InvalidOperationException(
                    "Retained Earnings must be ZERO before closing.");
        }

        // ============================================
        // 4. VERIFY CUMULATIVE TRIAL BALANCE
        // ============================================

        using (var cmd = con.CreateCommand())
        {
            cmd.Transaction = tx;

            cmd.CommandText =
                "SELECT COUNT(*), SUM([Total]) FROM qryTrialBalance";

            using var reader = cmd.ExecuteReader();

            if (reader == null || !reader.Read())
                throw new InvalidOperationException(
                    "Unable to read Trial Balance.");

            int count = Convert.ToInt32(reader.GetValue(0));

            decimal difference = reader.IsDBNull(1)
                ? 0m
                : Convert.ToDecimal(reader.GetValue(1));

            if (count == 0 || difference != 0m)
                throw new InvalidOperationException(
                    "Cumulative Trial Balance is not balanced.");
        }

        // ============================================
        // 5. VERIFY ALL UNIX BALANCES
        // ============================================

        using (var cmd = con.CreateCommand())
        {
            cmd.Transaction = tx;

            cmd.CommandText =
                """
                SELECT [Unix], SUM([Amount])
                FROM GeneralLedger
                GROUP BY [Unix]
                """;

            using var reader = cmd.ExecuteReader();

            if (reader == null)
                throw new InvalidOperationException(
                    "Unable to verify UNIX balances.");

            while (reader.Read())
            {
                if (reader.IsDBNull(0))
                    throw new InvalidOperationException(
                        "GeneralLedger contains a NULL UNIX.");

                decimal balance = reader.IsDBNull(1)
                    ? 0m
                    : Convert.ToDecimal(reader.GetValue(1));

                if (balance != 0m)
                    throw new InvalidOperationException(
                        $"UNIX {reader.GetValue(0)} is not balanced.");
            }
        }

        // ============================================
        // 6. CAPTURE HISTORICAL BALANCES
        // BEFORE CLOSING ENTRIES
        // ============================================

        var historicalBalances =
            new List<(int AccountId, decimal Balance)>();

        using (var cmd = con.CreateCommand())
        {
            cmd.Transaction = tx;

            cmd.CommandText =
                "SELECT AccountId, [Total] FROM qryTrialBalance";

            using var reader = cmd.ExecuteReader();

            if (reader == null)
                throw new InvalidOperationException(
                    "Unable to capture historical balances.");

            while (reader.Read())
            {
                historicalBalances.Add((
                    Convert.ToInt32(reader.GetValue(0)),
                    reader.IsDBNull(1)
                        ? 0m
                        : Convert.ToDecimal(reader.GetValue(1))
                ));
            }
        }

        if (historicalBalances.Count == 0)
            throw new InvalidOperationException(
                "No balances available for HistoricalData.");

        // ============================================
        // 7. BUILD CLOSING ENTRIES
        // USING THE SAME TRANSACTION
        // ============================================

        var entries = BuildClosingEntriesDraft(
            selectedYearId,
            retainedEarningsAccountId,
            tx);

        if (entries.Count == 0)
            throw new InvalidOperationException(
                "No Closing Books entries generated.");

        decimal closingTotal = entries
            .Where(x =>
                x.Period == ClosingEntryPeriod.ClosingYear)
            .Sum(x => x.Amount);

        decimal openingTotal = entries
            .Where(x =>
                x.Period == ClosingEntryPeriod.OpeningYear)
            .Sum(x => x.Amount);

        if (closingTotal != 0m || openingTotal != 0m)
            throw new InvalidOperationException(
                "Closing or Opening entries are not balanced.");

                // ============================================
                // 8. CREATE NEXT ACCOUNTING YEAR
                // ============================================
                stage = "Creating Next Accounting Year";
                int newYearId = _yearRepository.InsertAccountingYear(
                        nextStart,
                        nextEnd,
                        tx);

        // ============================================
        // 9. GENERATE ONE UNIX
        // ============================================

        var transactionRepository =
            new TransactionRepository(_connectionFactory);

        long unix = transactionRepository.GetNextUnix(tx);

        // ============================================
        // 10. POST ALL GENERAL LEDGER ENTRIES
        // ============================================

        foreach (var entry in entries)
        {
            var gl = new GeneralLedger
            {
                AccountId = entry.AccountId,

                YearId =
                    entry.Period == ClosingEntryPeriod.ClosingYear
                        ? selectedYearId
                        : newYearId,

                Unix = checked((int)unix),
                Tdate = entry.Tdate,
                Ref = entry.Ref,
                Particulars = entry.Particulars,
                Amount = entry.Amount,
                TransactionType = entry.TransactionType,
                Narration = entry.Narration
            };

            transactionRepository.InsertGeneralLedger(gl, tx);
        }

        // ============================================
        // 11. SAVE HISTORICAL DATA
        // ============================================

        var historicalRepository =
            new HistoricalDataRepository();
        stage = "Inserting HistoricalData";
                historicalRepository.InsertSnapshot(
            selectedYearId,
            startDate,
            endDate,
            historicalBalances,
            tx);

        // ============================================
        // 12. VERIFY POSTED UNIX
        // ============================================

        using (var cmd = con.CreateCommand())
        {
            cmd.Transaction = tx;

            cmd.CommandText =
                """
                SELECT COUNT(*), SUM(Amount)
                FROM GeneralLedger
                WHERE [Unix] = ?
                """;

                    cmd.Parameters.Add(
                        new OleDbParameter
                        {
                            OleDbType = OleDbType.Integer,
                            Value = checked((int)unix)
                        });

                    using var reader = cmd.ExecuteReader();

            if (reader == null || !reader.Read())
                throw new InvalidOperationException(
                    "Unable to verify posted closing entries.");

            int postedCount =
                Convert.ToInt32(reader.GetValue(0));

            decimal postedDifference = reader.IsDBNull(1)
                ? 0m
                : Convert.ToDecimal(reader.GetValue(1));

            if (postedCount != entries.Count ||
                postedDifference != 0m)
                throw new InvalidOperationException(
                    "Posted Closing Books entries failed verification.");
        }

        // ============================================
        // 13. VERIFY HISTORICAL RECORD COUNT
        // ============================================

        using (var cmd = con.CreateCommand())
        {
            cmd.Transaction = tx;

            cmd.CommandText =
                "SELECT COUNT(*) FROM HistoricalData WHERE YearId = ?";

            cmd.Parameters.AddWithValue("@p1", selectedYearId);

            int historicalCount =
                Convert.ToInt32(cmd.ExecuteScalar());

            if (historicalCount != historicalBalances.Count)
                throw new InvalidOperationException(
                    "HistoricalData record count mismatch.");
        }

        // ============================================
        // 14. EVERYTHING PASSED - COMMIT
        // ============================================

        tx.Commit();

        return (
            newYearId,
            unix,
            entries.Count,
            historicalBalances.Count
        );
    }
            catch (Exception ex)
            {
                try
                {
                    tx.Rollback();
                }
                catch
                {
                    // Preserve original exception.
                }

                throw new InvalidOperationException(
                    $"Closing Books failed during:\n{stage}\n\n" +
                    $"Original Error:\n{ex.Message}",
                    ex);
            }
        }
    }
}