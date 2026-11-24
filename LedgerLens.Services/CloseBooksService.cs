using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Repositories;


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

        // STEP 7A.2 - Read-only accounting year validation

        public string GetClosingDiagnostic(
            int selectedYearId,
            int retainedEarningsAccountId)
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
            int retainedEarningsAccountId)
        {
            var years = _yearRepository.GetAccountingYears();

            var year = years.FirstOrDefault(
                y => y.YearId == selectedYearId);

            if (year == null)
                throw new InvalidOperationException(
                    "Selected accounting year not found.");

            if (retainedEarningsAccountId <= 0)
                throw new InvalidOperationException(
                    "Retained Earnings account is not configured.");

            DateTime closingDate = year.EndDate.Date;
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

            using var connection = _connectionFactory.CreateOpen();
            using var command = connection.CreateCommand();

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
    }
}