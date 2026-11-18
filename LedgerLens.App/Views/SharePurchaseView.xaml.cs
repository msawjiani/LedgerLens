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
using System.Globalization;
using LedgerLens.App.Session;
using LedgerLens.Data.Models;
using LedgerLens.Services.Abstractions;

namespace LedgerLens.App.Views
{
    /// <summary>
    /// Interaction logic for SharePurchaseView.xaml
    /// </summary>
    public partial class SharePurchaseView : UserControl
    {
        public SharePurchaseView()
        {
            InitializeComponent();
        }
        public IShareTradeService? ShareTradeService { get; set; }
        private void PurchaseInputs_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (QuantityTextBox == null ||
                PurchaseRateTextBox == null ||
                PurchaseAmountTextBox == null)
                return;

            bool qtyValid = decimal.TryParse(
                QuantityTextBox.Text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out decimal qty);

            bool rateValid = decimal.TryParse(
                PurchaseRateTextBox.Text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out decimal rate);

            if (qtyValid && rateValid)
            {
                try
                {
                    decimal amount = Math.Round(
                        qty * rate,
                        2,
                        MidpointRounding.AwayFromZero);

                    PurchaseAmountTextBox.Text =
                        amount.ToString("0.00", CultureInfo.CurrentCulture);
                }
                catch (OverflowException)
                {
                    PurchaseAmountTextBox.Text = "0.00";
                }
            }
            else
            {
                PurchaseAmountTextBox.Text = "0.00";
            }
        }
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // CREDIT ACCOUNT
            if (CreditAccountCombo.SelectedValue is not int creditId)
            {
                ShowError("Please select a Credit Account.");
                return;
            }

            // SHARES ACCOUNT
            if (SharesAccountCombo.SelectedValue is not int sharesId)
            {
                ShowError("Please select a Shares Account.");
                return;
            }

            if (creditId == sharesId)
            {
                ShowError("Credit Account and Shares Account cannot be the same.");
                return;
            }

            // COMPANY
            if (CompaniesDataGrid.SelectedItem is not Company company ||
                company.AccountId != sharesId)
            {
                ShowError("Please select a Company.");
                return;
            }

            // GL DATE
            if (!TryParsePurchaseDate(GLDateTextBox.Text, out DateTime glDate))
            {
                ShowError("Please enter a valid GL Date.");
                return;
            }

            if (glDate.Date < SessionContext.SelectedStartDate.Date ||
                glDate.Date > SessionContext.SelectedEndDate.Date)
            {
                ShowError("GL Date must be within the selected financial year.");
                return;
            }

            // ACTUAL PURCHASE DATE

            
            if (!TryParsePurchaseDate(
                PurchaseDateTextBox.Text, out DateTime purchaseDate))
            {
                ShowError("Please enter a valid Purchase Date.");
                return;
            }

            // PURCHASE DATE MUST NOT EXCEED GL DATE
            if (purchaseDate.Date > glDate.Date)
            {
                ShowError("Purchase Date cannot be later than GL Date.");
                return;
            }

            // REFERENCE
            if (string.IsNullOrWhiteSpace(ReferenceTextBox.Text))
            {
                ShowError("Please enter a Reference.");
                return;
            }

            // QUANTITY
            if (!decimal.TryParse(
                    QuantityTextBox.Text,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out decimal qty) || qty <= 0)
            {
                ShowError("Please enter a valid Quantity.");
                return;
            }

            // PURCHASE RATE
            if (!decimal.TryParse(
                    PurchaseRateTextBox.Text,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out decimal rate) || rate <= 0)
            {
                ShowError("Please enter a valid Purchase Rate.");
                return;
            }

            try
            {
                decimal amount = Math.Round(
                    qty * rate,
                    2,
                    MidpointRounding.AwayFromZero);

                if (amount <= 0)
                {
                    ShowError("Purchase Amount must be greater than zero.");
                    return;
                }
            }
            catch (OverflowException)
            {
                ShowError("Purchase Amount is too large.");
                return;
            }

            // VALIDATION SUCCESSFUL
            // VALIDATION SUCCESSFUL - READY TO POST

            if (ShareTradeService == null)
            {
                ShowError("Share Trade Service is not initialized.");
                return;
            }

            // Do not post to a closed financial year.
            if (SessionContext.SelectedYearId != SessionContext.MaxYearId)
            {
                ShowError("Share Purchases can only be posted in the latest financial year.");
                return;
            }

            // Confirm the transaction before posting.
            var confirmation = MessageBox.Show(
                $"Database: {SessionContext.DatabaseFileName}\n\n" +
                $"Company: {company.CompanyName}\n" +
                $"GL Date: {glDate:dd-MMM-yyyy}\n" +
                $"Purchase Date: {purchaseDate:dd-MMM-yyyy}\n" +
                $"Quantity: {qty}\n" +
                $"Rate: {rate}\n" +
                $"Purchase Amount: {PurchaseAmountTextBox.Text}\n\n" +
                "Do you want to post this Share Purchase?",
                "Confirm Share Purchase",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;

            SaveButton.IsEnabled = false;

            try
            {
                // Build the complete narration

                    string narrationPrefix = NarrationTextBox.Text.Trim();

                    if (string.IsNullOrWhiteSpace(narrationPrefix))
                    {
                        narrationPrefix = "Being";
                    }
                    else if (!narrationPrefix.Equals("Being", StringComparison.OrdinalIgnoreCase)
                             && !narrationPrefix.StartsWith("Being ", StringComparison.OrdinalIgnoreCase))
                    {
                        narrationPrefix = "Being " + narrationPrefix;
                    }

                    string fullNarration =
                        $"{narrationPrefix} " +
                        $"Company: {company.CompanyName} " +
                        $"via {CreditAccountCombo.Text.Trim()} " +
                        $"- Qty: {qty} Rate: {rate}";
                var result = await ShareTradeService.MakeSharePurchaseAsync(
                    shareId: company.ShareId,
                    debitSharesAccountId: sharesId,
                    creditBrokerAccountId: creditId,
                    yearId: SessionContext.SelectedYearId,
                    purchaseDate: purchaseDate,
                    glDate: glDate,
                    qty: qty,
                    rate: rate,
                    reference: ReferenceTextBox.Text.Trim(),
                    narrationOverride: fullNarration,
                    sharesAccountName: SharesAccountCombo.Text,
                    brokerAccountName: CreditAccountCombo.Text,
                    companyName: company.CompanyName
                );

                MessageBox.Show(
                    $"Share Purchase Posted Successfully!\n\n" +
                    $"UNIX: {result.unix}\n" +
                    $"Debit Transaction ID: {result.debitId}\n" +
                    $"Credit Transaction ID: {result.creditId}\n" +
                    $"Share Purchase ID: {result.purchaseId}",
                    "LedgerLens 2.0",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Clear transaction inputs after successful posting.
                // Clear only the fields needed for the next purchase.
                    QuantityTextBox.Clear();
                    PurchaseRateTextBox.Clear();

                    NarrationTextBox.Text = "Being";

                    // Clear company selection AND the entire grid
                    CompaniesDataGrid.SelectedItem = null;
                    CompaniesDataGrid.ItemsSource = null;
            }
            catch (Exception ex)
            {
                ShowError("Share Purchase could not be posted.\n\n" + ex.Message);
            }
            finally
            {
                SaveButton.IsEnabled = true;
            }
        }
        private static bool TryParsePurchaseDate(
                        string text, out DateTime result)
        {
            string[] formats =
            {
                "dd-MMM-yyyy",
                "d-MMM-yyyy",
                "dd-MM-yyyy",
                "d-M-yyyy"
            };

            return DateTime.TryParseExact(
                text?.Trim(),
                formats,
                CultureInfo.GetCultureInfo("en-IN"),
                DateTimeStyles.None,
                out result);
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(
                message,
                "Share Purchase",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }


    }
}
