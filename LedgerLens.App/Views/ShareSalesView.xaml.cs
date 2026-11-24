using LedgerLens.App.Models;
using LedgerLens.App.Session;
using LedgerLens.Data.Models;
using LedgerLens.Data.Repositories;
using LedgerLens.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
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

namespace LedgerLens.App.Views
{
    /// <summary>
    /// Interaction logic for ShareSalesView.xaml
    /// </summary>
    public partial class ShareSalesView : UserControl
    {
        public LedgerAccountRepository? LedgerAccountRepository
        {
            get;
            set;
        }
        public ShareSalePostingService? ShareSalePostingService
        {
            get;
            set;
        }

        public CompanyRepository? CompanyRepository { get; set; }

        public ShareSaleService? ShareSaleService { get; set; }
        public ShareSalesView()
        {
            InitializeComponent();
        }
        // STEP 6C.1 - Populate Shares Account Dropdown

        public void LoadAccounts()
        {
            if (LedgerAccountRepository == null)
                return;

            var accounts = LedgerAccountRepository.GetAll();

            var shareAccounts = accounts
                .Where(x => x.SubledgerFlag?.Trim()
                    .Equals("SH", StringComparison.OrdinalIgnoreCase) == true)
                .OrderBy(x => x.Account)
                .ToList();

            SharesAccountCombo.ItemsSource = shareAccounts;

            SharesAccountCombo.SelectedIndex = -1;

            // STEP 6C.2 - Populate Broker Account Dropdown

            var brokerAccounts = accounts
                .Where(x => string.Equals(
                    x.Category?.Trim(),
                    "BS",
                    StringComparison.OrdinalIgnoreCase))
                .Where(x => string.Equals(
                    x.SubledgerFlag?.Trim(),
                    "NO",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Account)
                .ToList();

            BrokerAccountCombo.ItemsSource = brokerAccounts;

            BrokerAccountCombo.SelectedIndex = -1;
            DateTextBox.Text =
                SessionContext.SelectedStartDate.ToString("dd-MMM-yyyy");
        }
        // STEP 6D.2 - Load companies and available quantities

        private void LoadCompanyHoldings()
        {
            CompanyGrid.ItemsSource = null;

            if (SharesAccountCombo.SelectedItem is not LedgerAccount account)
                return;

            if (CompanyRepository == null || ShareSaleService == null)
                return;

            try
            {
                var companies =
                    CompanyRepository.GetByAccountId(account.AccountId);

                var holdings = new List<ShareSaleHolding>();

                foreach (var company in companies)
                {
                    var lots =
                        ShareSaleService.GetAvailableLots(company.ShareId);

                    decimal availableQty = lots
                        .Where(x =>
                            x.SharesAtCostAccountId == account.AccountId)
                        .Sum(x => x.Balance);

                    if (availableQty > 0m)
                    {
                        holdings.Add(new ShareSaleHolding
                        {
                            ShareId = company.ShareId,
                            Company = company.CompanyName,
                            AvailableQty = availableQty
                        });
                    }
                }

                CompanyGrid.ItemsSource = holdings;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to load share holdings.\n\n{ex.Message}",
                    "Share Sales",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void SharesAccountCombo_SelectionChanged(
                    object sender,
                    SelectionChangedEventArgs e)
        {
            LoadCompanyHoldings();
        }
        // STEP 6E - Company Selection

        private void CompanyGrid_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (CompanyGrid.SelectedItem is not ShareSaleHolding holding)
                return;

            MessageBox.Show(
                $"Company: {holding.Company}\n" +
                $"ShareId: {holding.ShareId}\n" +
                $"Available Quantity: {holding.AvailableQty}",
                "LedgerLens - Selected Company",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // STEP 6F.2 - Temporary Date Validation Test

        // STEP 6M - FINAL SHARE SALES POSTING

        private void SaveShareSale_Click(
            object sender,
            RoutedEventArgs e)
        {
            // 1. Validate Date

            if (!ValidateSaleDate(out DateTime saleDate))
                return;

            // 2. Validate GL Accounts

            if (!ValidateGLAccounts())
                return;

            // 3. Validate Reference and Narration

            if (!ValidateJournalDetails())
                return;

            // 4. Validate Quantity and Selling Rate

            if (!ValidateSaleValues(
                out decimal quantity,
                out decimal sellingRate))
                return;

            // 5. Validate Company Selection

            if (!ValidateSelectedCompany(
                quantity,
                out var holding))
                return;

            if (holding == null)
                return;

            // 6. Retrieve Selected Accounts

            if (BrokerAccountCombo.SelectedItem
                is not LedgerAccount broker)
                return;

            if (SharesAccountCombo.SelectedItem
                is not LedgerAccount sharesAccount)
                return;

            if (ShareSalePostingService == null)
            {
                MessageBox.Show(
                    "Share Sale Posting Service is not initialized.",
                    "Share Sales",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            // 7. Display FIFO Preview
            // Stop immediately if preview fails.

            if (!ShowSalePreview(
                holding,
                quantity,
                sellingRate))
                return;

            // 8. Calculate Expected Sale Proceeds

            decimal expectedProceeds = decimal.Round(
                quantity * sellingRate,
                2,
                MidpointRounding.AwayFromZero);

            string reference = ReferenceTextBox.Text.Trim();
            string narration = NarrationTextBox.Text.Trim();

            // 9. FINAL CONFIRMATION

            var confirmation = MessageBox.Show(

                $"DATABASE:\n{SessionContext.DatabaseFileName}\n\n" +

                $"Company: {holding.Company}\n" +
                $"ShareId: {holding.ShareId}\n\n" +

                $"Broker Account:\n{broker.Account}\n\n" +

                $"Shares Account:\n{sharesAccount.Account}\n\n" +

                $"Sale Date: {saleDate:dd-MMM-yyyy}\n" +
                $"Reference: {reference}\n\n" +

                $"Quantity: {quantity:0.######}\n" +
                $"Selling Rate: Rs.{sellingRate:N2}\n" +

                $"SALE PROCEEDS: Rs.{expectedProceeds:N2}\n\n" +

                "POST THIS SHARE SALE?\n\n" +
                "This operation will update GeneralLedger and ShareSales.",

                "LedgerLens - Confirm Share Sale Posting",

                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;

            // 10. Disable SAVE to prevent repeated clicks

            SaveShareSaleButton.IsEnabled = false;

            try
            {
                // 11. ATOMIC DATABASE POSTING

                long unix = ShareSalePostingService.PostShareSale(

                    holding.ShareId,
                    SessionContext.SelectedYearId,
                    saleDate,
                    quantity,
                    sellingRate,
                    broker.AccountId,
                    reference,
                    narration,

                    SessionContext.LTCGAccountCode,
                    SessionContext.STCGAccountCode,
                    SessionContext.LTCLAccountCode,
                    SessionContext.STCLAccountCode);

                // 12. SUCCESS

                MessageBox.Show(

                    "SHARE SALE POSTED SUCCESSFULLY!\n\n" +

                    $"UNIX: {unix}\n" +
                    $"Company: {holding.Company}\n" +
                    $"Quantity Sold: {quantity:0.######}\n" +
                    $"Sale Proceeds: Rs.{expectedProceeds:N2}\n\n" +

                    "GeneralLedger and ShareSales updated.\n\n" +
                    "Please verify Trial Balance.",

                    "LedgerLens - Share Sale Saved",

                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Leave SAVE disabled after successful posting.
                // Reopen Share Sales for the next transaction.
            }
            catch (Exception ex)
            {
                SaveShareSaleButton.IsEnabled = true;

                MessageBox.Show(

                    "SHARE SALE POSTING FAILED.\n\n" +
                    ex.Message,

                    "LedgerLens - Posting Error",

                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        // STEP 6F.1 - Validate Share Sale Date

        private bool ValidateSaleDate(out DateTime saleDate)
        {
            saleDate = default;

            string dateText = DateTextBox.Text.Trim();

            if (!DateTime.TryParseExact(
                dateText,
                "dd-MMM-yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out saleDate))
            {
                MessageBox.Show(
                    "Please enter a valid date in dd-MMM-yyyy format.",
                    "Invalid Date",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DateTextBox.Focus();
                return false;
            }

            if (saleDate.Date < SessionContext.SelectedStartDate.Date ||
                saleDate.Date > SessionContext.SelectedEndDate.Date)
            {
                MessageBox.Show(
                    $"Share Sale date must be between " +
                    $"{SessionContext.SelectedStartDate:dd-MMM-yyyy} and " +
                    $"{SessionContext.SelectedEndDate:dd-MMM-yyyy}.",
                    "Invalid Financial Year",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DateTextBox.Focus();
                return false;
            }

            return true;
        }
        // STEP 6G - Validate Reference and Narration

        private bool ValidateJournalDetails()
        {
            if (string.IsNullOrWhiteSpace(ReferenceTextBox.Text))
            {
                MessageBox.Show(
                    "Reference cannot be blank.",
                    "Invalid Reference",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                ReferenceTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(NarrationTextBox.Text))
            {
                MessageBox.Show(
                    "Narration cannot be blank.",
                    "Invalid Narration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                NarrationTextBox.Focus();
                return false;
            }

            return true;
        }

        // STEP 6H.1 - Validate Quantity and Selling Rate

        private bool ValidateSaleValues(
            out decimal quantity,
            out decimal sellingRate)
        {
            quantity = 0m;
            sellingRate = 0m;

            if (!decimal.TryParse(
                QuantityTextBox.Text.Trim(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out quantity) ||
                quantity <= 0m ||
                quantity > 999999999m ||
                decimal.Round(quantity, 6) != quantity)
            {
                MessageBox.Show(
                    "Enter a valid positive quantity with a maximum of six decimal places.",
                    "Invalid Quantity",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                QuantityTextBox.Focus();
                return false;
            }

            if (!decimal.TryParse(
                SellingRateTextBox.Text.Trim(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out sellingRate) ||
                sellingRate <= 0m ||
                decimal.Round(sellingRate, 6) != sellingRate)
            {
                MessageBox.Show(
                    "Enter a valid positive selling rate with a maximum of six decimal places.",
                    "Invalid Selling Rate",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                SellingRateTextBox.Focus();
                return false;
            }

            return true;
        }

        // STEP 6I.1 - Validate selected company

        private bool ValidateSelectedCompany(
            decimal quantity,
            out ShareSaleHolding? holding)
        {
            holding = CompanyGrid.SelectedItem as ShareSaleHolding;

            if (holding == null)
            {
                MessageBox.Show(
                    "Please select a company from the grid.",
                    "Company Not Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                CompanyGrid.Focus();
                return false;
            }

            if (quantity > holding.AvailableQty)
            {
                MessageBox.Show(
                    $"Insufficient shares available.\n\n" +
                    $"Company: {holding.Company}\n" +
                    $"Available: {holding.AvailableQty:0.######}\n" +
                    $"Requested: {quantity:0.######}",
                    "Insufficient Shares",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                QuantityTextBox.Focus();
                return false;
            }

            return true;
        }

        // STEP 6I.2 - Validate GL Account Selections

        private bool ValidateGLAccounts()
        {
            if (BrokerAccountCombo.SelectedItem is not LedgerAccount broker)
            {
                MessageBox.Show(
                    "Please select a Broker Account.",
                    "Broker Account Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                BrokerAccountCombo.Focus();
                return false;
            }

            if (SharesAccountCombo.SelectedItem is not LedgerAccount shares)
            {
                MessageBox.Show(
                    "Please select a Shares Account.",
                    "Shares Account Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                SharesAccountCombo.Focus();
                return false;
            }

            if (broker.AccountId == shares.AccountId)
            {
                MessageBox.Show(
                    "Broker Account and Shares Account cannot be the same.",
                    "Invalid GL Selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            return true;
        }
        // STEP 6J - Display FIFO Preview

        private bool ShowSalePreview(
            ShareSaleHolding holding,
            decimal quantity,
            decimal sellingRate)
        {
            if (ShareSalePostingService == null)
            {
                MessageBox.Show(
                    "Share Sale Posting Service is not initialized.");
                return false; ;
            }

            if (!ValidateSaleDate(out DateTime saleDate))
                return false; ;

            try
            {
                var entries = ShareSalePostingService.PrepareSale(
                    holding.ShareId,
                    saleDate,
                    quantity,
                    sellingRate);
                // STEP 6K.4 - Verify Shares-at-Cost Account

                if (SharesAccountCombo.SelectedItem is not LedgerAccount sharesAccount)
                {
                    throw new InvalidOperationException(
                        "Please select a Shares Account.");
                }

                if (entries.Any(x =>
                    x.SharesAtCostAccountId != sharesAccount.AccountId))
                {
                    throw new InvalidOperationException(
                        "FIFO allocations contain purchases from a different " +
                        "Shares-at-Cost account.\n\n" +
                        "The sale cannot proceed with this account selection.");
                }
                string result =
                    $"COMPANY: {holding.Company}\n" +
                    $"QUANTITY: {quantity:0.######}\n" +
                    $"SELLING RATE: {sellingRate:N2}\n\n";

                foreach (var entry in entries)
                {
                    result +=
                        $"Purchase Lot: {entry.PurchaseTransId}\n" +
                        $"Qty Sold: {entry.QtySold:0.######}\n" +
                        $"Purchase Cost: {entry.PurchaseCost:N2}\n" +
                        $"Sale Proceeds: {entry.SaleProceeds:N2}\n" +
                        $"Gain/Loss: {entry.GainLoss:N2} " +
                        $"({entry.Classification})\n\n";
                }
                

                result +=
                    $"TOTAL COST: {entries.Sum(x => x.PurchaseCost):N2}\n" +
                    $"TOTAL PROCEEDS: {entries.Sum(x => x.SaleProceeds):N2}\n" +
                    $"NET GAIN/LOSS: {entries.Sum(x => x.GainLoss):N2}\n\n" +
                    "PREVIEW ONLY — NO DATABASE POSTING.";

                MessageBox.Show(
                    result,
                    "LedgerLens - Share Sales FIFO Preview",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Share Sales Preview Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                 return false;
            }
            return true;
        }
    }
}
