using LedgerLens.App.Session;
using LedgerLens.Data.Models;
using LedgerLens.Data.Repositories;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LedgerLens.Services;
using System.Windows.Media;

namespace LedgerLens.App.Views
{
    public partial class SubledgerJournalView : UserControl
    {
        public LedgerAccountRepository? LedgerAccountRepository
        {
            get;
            set;
        }

        public SubledgerRepository? SubledgerRepository
        {
            get;
            set;
        }
        // STEP 6B - Subledger Journal Posting Service

        public SubledgerJournalService? SubledgerJournalService
        {
            get;
            set;
        }

        public SubledgerJournalView()
        {
            InitializeComponent();
        }

        // STEP 5A - Validate Journal Date

        // STEP 5A - Validate Journal Date

        private bool ValidateJournalDate(out DateTime journalDate)
        {
            journalDate = default;

            string dateText = DateTextBox.Text.Trim();

            // 1. Validate date format
            if (!DateTime.TryParseExact(
                    dateText,
                    "dd-MMM-yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out journalDate))
            {
                MessageBox.Show(
                    "Enter a valid date in dd-MMM-yyyy format.",
                    "Invalid Journal Date",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DateTextBox.Focus();
                return false;
            }

            // 2. Validate selected financial year
            if (journalDate.Date < SessionContext.SelectedStartDate.Date ||
                journalDate.Date > SessionContext.SelectedEndDate.Date)
            {
                MessageBox.Show(
                    $"Journal date must be between " +
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

        // STEP 4A - Load GL Accounts

        public void LoadAccounts()
        {
            if (LedgerAccountRepository == null)
                return;

            var accounts = LedgerAccountRepository.GetAll();

            // Credit - Ordinary GL Accounts
            var creditAccounts = accounts
                .Where(a =>
                    IsOrdinaryAccount(a.SubledgerFlag) &&
                    !string.Equals(
                        a.Category?.Trim(),
                        "BANK",
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Debit - Subledger GL Accounts
            var debitAccounts = accounts
                .Where(a =>
                    !IsOrdinaryAccount(a.SubledgerFlag))
                .ToList();

            CreditAccountCombo.ItemsSource = creditAccounts;
            CreditAccountCombo.DisplayMemberPath = "Account";

            DebitAccountCombo.ItemsSource = debitAccounts;
            DebitAccountCombo.DisplayMemberPath = "Account";
        }

        private static bool IsOrdinaryAccount(string? flag)
        {
            string value = (flag ?? "")
                .Trim()
                .ToUpperInvariant();

            return value is "" or "N" or "NO" or "NONE" or "0";
        }

        // STEP 4B - Load Subledgers

        private void DebitAccountCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            SubledgerGrid.ItemsSource = null;
            SubledgerGrid.SelectedItem = null;

            SelectedSubledgerText.Text =
                "No subledger selected";

            if (SubledgerRepository == null)
                return;

            if (DebitAccountCombo.SelectedItem
                is not LedgerAccount account)
                return;

            var subledgers =
                SubledgerRepository.GetByAccountId(account.AccountId);

            SubledgerGrid.ItemsSource = subledgers;
        }

        // STEP 4C - Display Selected Subledger

        private void SubledgerGrid_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (SubledgerGrid.SelectedItem is Subledger selected)
            {
                SelectedSubledgerText.Text =
                    $"Selected: {selected.Subaccount} " +
                    $"(ID: {selected.SubledgerId})";
            }
            else
            {
                SelectedSubledgerText.Text =
                    "No subledger selected";
            }
        }
        // STEP 5A.3 - Test Date Validation Only

        private void SaveJournalButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!ValidateJournalDate(out DateTime journalDate))
                return;
            // STEP 5B - Validate Reference

            if (string.IsNullOrWhiteSpace(ReferenceTextBox.Text))
            {
                MessageBox.Show(
                    "Reference cannot be blank.",
                    "Invalid Reference",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                ReferenceTextBox.Focus();
                return;
            }

            // STEP 5B.2 - Validate Narration

            if (string.IsNullOrWhiteSpace(NarrationTextBox.Text))
            {
                MessageBox.Show(
                    "Narration cannot be blank.",
                    "Invalid Narration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                NarrationTextBox.Focus();
                return;
            }

            // STEP 5B.3 - Validate Amount

            string amountText = AmountTextBox.Text.Trim();

            if (!decimal.TryParse(
                    amountText,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal amount) ||
                amount <= 0 ||
                (decimal.GetBits(amount)[3] >> 16 & 0xFF) > 2)
            {
                MessageBox.Show(
                    "Enter a valid positive amount with a maximum of two decimal places.",
                    "Invalid Amount",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                AmountTextBox.Focus();
                return;
            }

            // STEP 5C.1 - Validate Credit GL

            if (CreditAccountCombo.SelectedItem is not LedgerAccount)
            {
                MessageBox.Show(
                    "Please select a valid Credit GL account.",
                    "Invalid Credit Account",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                CreditAccountCombo.Focus();
                return;
            }

            // STEP 5C.1 - Validate Credit GL
            if (CreditAccountCombo.SelectedItem is not LedgerAccount)
            {
                MessageBox.Show(
                    "Please select a valid Credit GL account.",
                    "Invalid Credit Account",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                CreditAccountCombo.Focus();
                return;
            }

            // STEP 5C.2 - Validate Debit GL
            if (DebitAccountCombo.SelectedItem is not LedgerAccount)
            {
                MessageBox.Show(
                    "Please select a valid Debit GL account.",
                    "Invalid Debit Account",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DebitAccountCombo.Focus();
                return;
            }
            // STEP 5C.3 - Validate Subledger Selection

            if (SubledgerGrid.SelectedItem is not Subledger)
            {
                MessageBox.Show(
                    "Please select a subledger from the grid.",
                    "Subledger Selection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                SubledgerGrid.Focus();
                return;
            }
            // STEP 6D - POST SUBLEDGER JOURNAL

            if (SubledgerJournalService == null)
            {
                MessageBox.Show(
                    "Subledger Journal Service is not configured.",
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            // All selections have already passed validation.

            var debitAccount =
                (LedgerAccount)DebitAccountCombo.SelectedItem;

            var creditAccount =
                (LedgerAccount)CreditAccountCombo.SelectedItem;

            var selectedSubledger =
                (Subledger)SubledgerGrid.SelectedItem;

            string reference = ReferenceTextBox.Text.Trim();
            string narration = NarrationTextBox.Text.Trim();

            // CONFIRM BEFORE POSTING

            var confirmation = MessageBox.Show(
                $"DATABASE:\n{SessionContext.DatabaseFileName}\n\n" +

                $"Date: {journalDate:dd-MMM-yyyy}\n" +
                $"Reference: {reference}\n\n" +

                $"DEBIT: {debitAccount.Account}\n" +
                $"Amount: Rs.{amount:N2}\n\n" +

                $"CREDIT: {creditAccount.Account}\n" +
                $"Amount: Rs.{amount:N2}\n\n" +

                $"SUBLEDGER:\n{selectedSubledger.Subaccount}\n" +
                $"Subledger ID: {selectedSubledger.SubledgerId}\n\n" +

                $"Transaction Type: SJ\n\n" +

                "POST THIS SUBLEDGER JOURNAL?",

                "Confirm Subledger Journal Posting",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;

            // ACTUAL DATABASE POSTING

            try
            {
                long unix = SubledgerJournalService.PostSubledgerJournal(
                    SessionContext.SelectedYearId,
                    journalDate,
                    reference,
                    narration,
                    amount,
                    debitAccount,
                    creditAccount,
                    selectedSubledger);

                MessageBox.Show(
                    "SUBLEDGER JOURNAL SAVED SUCCESSFULLY!\n\n" +
                    $"UNIX: {unix}\n" +
                    $"Date: {journalDate:dd-MMM-yyyy}\n" +
                    $"Reference: {reference}\n\n" +
                    $"Debit: Rs.{amount:N2}\n" +
                    $"Credit: Rs.{amount:N2}\n\n" +
                    $"Difference: Rs.0.00",
                    "LedgerLens - Subledger Journal Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Clear saved input to prevent accidental duplicate posting.

                ReferenceTextBox.Clear();
                NarrationTextBox.Clear();
                AmountTextBox.Clear();

                SubledgerGrid.SelectedItem = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Subledger Journal could not be posted.\n\n" +
                    ex.Message,
                    "Subledger Journal Posting Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        // DISCO - Amount Magnitude Indicator
        private void Amount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox || Mag10Cr == null)
                return;

            decimal.TryParse(
                textBox.Text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal amount);

            Brush inactive = new SolidColorBrush(
                Color.FromRgb(203, 213, 225));

            Mag1K.Background = amount >= 1000
                ? Brushes.LimeGreen : inactive;

            Mag10K.Background = amount >= 10000
                ? Brushes.DodgerBlue : inactive;

            Mag1L.Background = amount >= 100000
                ? Brushes.MediumPurple : inactive;

            Mag10L.Background = amount >= 1000000
                ? Brushes.Gold : inactive;

            Mag1Cr.Background = amount >= 10000000
                ? Brushes.DarkOrange : inactive;

            Mag10Cr.Background = amount >= 100000000
                ? Brushes.Red : inactive;
        }
    }
}