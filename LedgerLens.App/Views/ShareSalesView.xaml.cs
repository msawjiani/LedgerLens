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

        private void TestSaleDate_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!ValidateSaleDate(out DateTime saleDate))
                return;

            MessageBox.Show(
                $"Share Sale Date: {saleDate:dd-MMM-yyyy}\n\n" +
                $"FY Start: {SessionContext.SelectedStartDate:dd-MMM-yyyy}\n" +
                $"FY End: {SessionContext.SelectedEndDate:dd-MMM-yyyy}\n\n" +
                "DATE VALIDATION PASSED\n\n" +
                "TEST ONLY - No database posting.",
                "LedgerLens - Share Sales Date Test",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
    }
}
