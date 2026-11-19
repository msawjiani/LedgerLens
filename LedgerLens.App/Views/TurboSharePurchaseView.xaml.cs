using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LedgerLens.App.Session;
using LedgerLens.Services.Abstractions;
using ClosedXML.Excel;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using LedgerLens.App.Models;
using LedgerLens.Data.Models;
using LedgerLens.Data.Repositories;
using System.IO;


namespace LedgerLens.App.Views
{
    /// <summary>
    /// Interaction logic for TurboSharePurchaseView.xaml
    /// </summary>
    public partial class TurboSharePurchaseView : UserControl
    {
        public TurboSharePurchaseView()
        {
            InitializeComponent();
        }
        private bool _batchPostingAttempted = false;
        public IShareTradeService? ShareTradeService { get; set; }
        public LedgerAccountRepository? LedgerAccountRepository { get; set; }

        public CompanyRepository? CompanyRepository { get; set; }
        private readonly ObservableCollection<TurboSharePurchaseRow>
            _excelRows = new();
        private void SelectExcelButton_Click(
                object sender,
                RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Select Share Purchase Excel File"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var importedRows = new List<TurboSharePurchaseRow>();

                using var workbook = new XLWorkbook(dialog.FileName);

                var worksheet = workbook.Worksheet(1);

                // Skip the Excel header row.
                foreach (var excelRow in worksheet.RowsUsed().Skip(1))
                {
                    var row = new TurboSharePurchaseRow
                    {
                        ExcelRowNumber = excelRow.RowNumber(),

                        GLAccountForCredit =
                            excelRow.Cell(1).GetString().Trim(),

                        GLSharesAccountForDebit =
                            excelRow.Cell(2).GetString().Trim(),

                        Company =
                            excelRow.Cell(3).GetString().Trim()
                    };

                    var errors = new List<string>();

                    // TRADE DATE
                    if (excelRow.Cell(4).TryGetValue<DateTime>(
                            out DateTime tradeDate))
                    {
                        row.TradeDate = tradeDate.Date;
                    }
                    else
                    {
                        errors.Add("Invalid Trade Date");
                    }

                    // QUANTITY
                    if (excelRow.Cell(5).TryGetValue<decimal>(
                            out decimal qty) && qty > 0)
                    {
                        row.Qty = qty;
                    }
                    else
                    {
                        errors.Add("Invalid Quantity");
                    }

                    // RATE
                    if (excelRow.Cell(6).TryGetValue<decimal>(
                            out decimal rate) && rate > 0)
                    {
                        row.Rate = rate;
                    }
                    else
                    {
                        errors.Add("Invalid Rate");
                    }

                    // AMOUNT
                    if (row.Qty > 0 && row.Rate > 0)
                    {
                        try
                        {
                            row.Amt = Math.Round(
                                row.Qty * row.Rate,
                                2,
                                MidpointRounding.AwayFromZero);
                        }
                        catch (OverflowException)
                        {
                            errors.Add("Amount too large");
                        }
                    }

                    row.ErrorMessage = errors.Count == 0
                        ? null
                        : string.Join("; ", errors);

                    importedRows.Add(row);
                }

                // VALIDATE GL ACCOUNTS AGAINST ACCDB

                if (LedgerAccountRepository == null)
                {
                    MessageBox.Show("Ledger Account Repository is not initialized.");
                    return;
                }

                var accounts = LedgerAccountRepository.GetAll();

                foreach (var row in importedRows)
                {
                    var errors = new List<string>();

                    // Preserve existing Excel parsing errors
                    if (!string.IsNullOrWhiteSpace(row.ErrorMessage))
                        errors.Add(row.ErrorMessage);

                    // CREDIT ACCOUNT
                    bool creditExists = accounts.Any(a =>
                        string.Equals(
                            a.Account?.Trim(),
                            row.GLAccountForCredit,
                            StringComparison.OrdinalIgnoreCase));

                    if (!creditExists)
                        errors.Add("Credit GL Account not found.");

                    // DEBIT ACCOUNT
                    bool debitExists = accounts.Any(a =>
                        string.Equals(
                            a.Account?.Trim(),
                            row.GLSharesAccountForDebit,
                            StringComparison.OrdinalIgnoreCase));

                    if (!debitExists)
                        errors.Add("Debit GL Account not found.");

                    row.ErrorMessage = errors.Count == 0
                        ? null
                        : string.Join("; ", errors);
                }
                // VALIDATE COMPANIES AGAINST SHARESCHART

                if (CompanyRepository == null)
                {
                    MessageBox.Show("Company Repository is not initialized.");
                    return;
                }

                // Cache companies by Shares Account to avoid
                // repeatedly querying the database.

                var companyCache = new Dictionary<int, HashSet<string>>();

                foreach (var row in importedRows)
                {
                    // Find the Shares GL Account
                    var debitAccount = accounts.FirstOrDefault(a =>
                        string.Equals(
                            a.Account?.Trim(),
                            row.GLSharesAccountForDebit?.Trim(),
                            StringComparison.OrdinalIgnoreCase));

                    // Already flagged by GL validation
                    if (debitAccount == null)
                        continue;

                    int accountId = debitAccount.AccountId;

                    // Load companies only once per Shares Account
                    if (!companyCache.TryGetValue(accountId, out var companyNames))
                    {
                        companyNames = CompanyRepository
                            .GetByAccountId(accountId)
                            .Select(c => c.CompanyName.Trim())
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                        companyCache[accountId] = companyNames;
                    }

                    // Validate company
                    if (!companyNames.Contains(row.Company.Trim()))
                    {
                        string error =
                            "Company not found under selected Shares GL.";

                        row.ErrorMessage =
                            string.IsNullOrWhiteSpace(row.ErrorMessage)
                                ? error
                                : row.ErrorMessage + "; " + error;
                    }
                }

                // RESOLVE DATABASE IDs FOR VALID ROWS

                var postingCompanyCache = new Dictionary<int, List<Company>>();

                foreach (var row in importedRows)
                {
                    // Do not process rows already containing errors.
                    if (!string.IsNullOrWhiteSpace(row.ErrorMessage))
                        continue;

                    var creditAccount = accounts.FirstOrDefault(a =>
                        string.Equals(
                            a.Account?.Trim(),
                            row.GLAccountForCredit,
                            StringComparison.OrdinalIgnoreCase));

                    var sharesAccount = accounts.FirstOrDefault(a =>
                        string.Equals(
                            a.Account?.Trim(),
                            row.GLSharesAccountForDebit,
                            StringComparison.OrdinalIgnoreCase));

                    if (creditAccount == null || sharesAccount == null)
                    {
                        row.ErrorMessage = "Unable to resolve GL Account IDs.";
                        continue;
                    }

                    if (!postingCompanyCache.TryGetValue(
                        sharesAccount.AccountId, out var companies))
                    {
                        companies = CompanyRepository.GetByAccountId(
                            sharesAccount.AccountId);

                        postingCompanyCache[sharesAccount.AccountId] = companies;
                    }

                    var company = companies.FirstOrDefault(c =>
                        string.Equals(
                            c.CompanyName.Trim(),
                            row.Company.Trim(),
                            StringComparison.OrdinalIgnoreCase));

                    if (company == null)
                    {
                        row.ErrorMessage = "Unable to resolve Share ID.";
                        continue;
                    }

                    row.CreditAccountId = creditAccount.AccountId;
                    row.SharesAccountId = sharesAccount.AccountId;
                    row.ShareId = company.ShareId;
                }

                _excelRows.Clear();

                // VALIDATE TRADE DATE AGAINST SELECTED FINANCIAL YEAR

                foreach (var row in importedRows)
                {
                    // Invalid or missing dates have already been
                    // flagged during Excel reading.
                    if (!row.TradeDate.HasValue)
                        continue;

                    DateTime tradeDate = row.TradeDate.Value.Date;

                    if (tradeDate < SessionContext.SelectedStartDate.Date ||
                        tradeDate > SessionContext.SelectedEndDate.Date)
                    {
                        string error = "Trade Date outside selected financial year.";

                        row.ErrorMessage =
                            string.IsNullOrWhiteSpace(row.ErrorMessage)
                                ? error
                                : row.ErrorMessage + "; " + error;
                    }
                }

                foreach (var row in importedRows)
                    _excelRows.Add(row);

                TurboGrid.ItemsSource = _excelRows;

                // SUMMARY CALCULATION
                UpdateImportSummary();

                // DATABASE IMPORT IS NOT ENABLED YET
               
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to read Excel file.\n\n{ex.Message}",
                    "TURBO Share Purchase",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.DataContext is not TurboSharePurchaseRow row)
                return;

            _excelRows.Remove(row);

            UpdateImportSummary();

            // Posting is not implemented yet.
            ImportButton.IsEnabled = false;
        }
        private void UpdateImportSummary()
        {
            int total = _excelRows.Count;

            int errorsCount = _excelRows.Count(
                x => !string.IsNullOrWhiteSpace(x.ErrorMessage));

            int valid = total - errorsCount;

            decimal totalAmount = _excelRows
                .Where(x => string.IsNullOrWhiteSpace(x.ErrorMessage))
                .Sum(x => x.Amt);

            TotalRowsText.Text = $"Rows: {total}";

            ValidRowsText.Text = $"Valid: {valid}";

            ErrorRowsText.Text = $"Errors: {errorsCount}";

            TotalAmountText.Text =
                $"Total Amount: ₹{totalAmount:N2}";

            // Enable IMPORT only when every row is valid
            // and the latest financial year is selected.

            ImportButton.IsEnabled =
                total > 0 &&
                errorsCount == 0 &&
                SessionContext.SelectedYearId == SessionContext.MaxYearId;
        }

        private async void ImportButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_batchPostingAttempted)
                return;

            if (ShareTradeService == null)
            {
                MessageBox.Show("Share Trade Service is not initialized.");
                return;
            }

            // FIRST TEST: SCRAP DATABASE ONLY
            string databaseName =
                System.IO.Path.GetFileName(SessionContext.DatabaseFileName);



            if (SessionContext.SelectedYearId != SessionContext.MaxYearId)
            {
                MessageBox.Show("Please select the latest financial year.");
                return;
            }

            if (_excelRows.Count == 0 ||
                _excelRows.Any(r =>
                    !string.IsNullOrWhiteSpace(r.ErrorMessage) ||
                    !r.CreditAccountId.HasValue ||
                    !r.SharesAccountId.HasValue ||
                    !r.ShareId.HasValue ||
                    !r.TradeDate.HasValue))
            {
                MessageBox.Show("All rows must be valid before posting.");
                return;
            }

            string reference = BatchReferenceTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(reference))
            {
                MessageBox.Show("Please enter a Reference.");
                return;
            }

            string narrationPrefix = BatchNarrationTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(narrationPrefix))
                narrationPrefix = "Being";
            else if (!narrationPrefix.StartsWith(
                "Being",
                StringComparison.OrdinalIgnoreCase))
                narrationPrefix = "Being " + narrationPrefix;

            decimal totalAmount = _excelRows.Sum(r => r.Amt);

            var confirmation = MessageBox.Show(
                $"Database: {databaseName}\n\n" +
                $"Transactions: {_excelRows.Count}\n" +
                $"Total Amount: ₹{totalAmount:N2}\n" +
                $"Reference: {reference}\n\n" +
                "POST THESE SHARE PURCHASES?",
                "TURBO SHARE PURCHASE - CONFIRM POSTING",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            _batchPostingAttempted = true;

            ImportButton.IsEnabled = false;
            SelectExcelButton.IsEnabled = false;
            TurboGrid.IsEnabled = false;

            int posted = 0;
            string failure = "";
            var postingDetails = new List<string>();

            foreach (var row in _excelRows.ToList())
            {
                try
                {
                    string narration =
                        $"{narrationPrefix} " +
                        $"Company: {row.Company} " +
                        $"via {row.GLAccountForCredit} " +
                        $"- Qty: {row.Qty:0.######} " +
                        $"Rate: {row.Rate:0.######}";

                    var result =
                        await ShareTradeService.MakeSharePurchaseAsync(
                            shareId: row.ShareId!.Value,
                            debitSharesAccountId: row.SharesAccountId!.Value,
                            creditBrokerAccountId: row.CreditAccountId!.Value,
                            yearId: SessionContext.SelectedYearId,
                            purchaseDate: row.TradeDate!.Value,
                            glDate: row.TradeDate.Value,
                            qty: row.Qty,
                            rate: row.Rate,
                            reference: reference,
                            narrationOverride: narration,
                            sharesAccountName: row.GLSharesAccountForDebit,
                            brokerAccountName: row.GLAccountForCredit,
                            companyName: row.Company);

                    posted++;

                    postingDetails.Add(
                        $"Excel Row {row.ExcelRowNumber}: " +
                        $"UNIX {result.unix}, " +
                        $"GL {result.debitId}/{result.creditId}, " +
                        $"Purchase {result.purchaseId}");

                    // Successfully posted rows are removed from preview.
                    _excelRows.Remove(row);
                }
                catch (Exception ex)
                {
                    failure =
                        $"FAILED at Excel Row {row.ExcelRowNumber}\n" +
                        ex.Message;

                    break;
                }
            }

            UpdateImportSummary();

            // Keep IMPORT disabled after an attempt.
            ImportButton.IsEnabled = false;

            string report =
                $"Successfully Posted: {posted}\n" +
                $"Remaining Rows: {_excelRows.Count}\n\n" +
                string.Join("\n", postingDetails);

            if (!string.IsNullOrWhiteSpace(failure))
            {
                report +=
                    $"\n\n{failure}\n\n" +
                    "STOP: Verify the posted transactions in Access " +
                    "before attempting another import.";
            }

            MessageBox.Show(
                report,
                "TURBO SHARE PURCHASE - POSTING RESULTS",
                MessageBoxButton.OK,
                string.IsNullOrWhiteSpace(failure)
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);
        }

    }

}
