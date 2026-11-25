using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using LedgerLens.App.Models;
using LedgerLens.App.Session;
using LedgerLens.Data;
using LedgerLens.Services;
using Microsoft.Extensions.Options;

using System.IO;



namespace LedgerLens.App.Views
{
    public partial class CloseBooksView : UserControl
    {
        private bool _validationPassed = false;
        private bool _previewReady = false;
        public CloseBooksView()
        {
            InitializeComponent();

            CloseBooksButton.IsEnabled = false;
            CloseBooksButton.Click += CloseBooksButton_Click;

            ConfirmYesTextBox.TextChanged += (_, _) =>
                    UpdateCloseBooksButtonState();

            ConfirmCloseTextBox.TextChanged += (_, _) =>
                UpdateCloseBooksButtonState();

            IndividualText.Text = SessionContext.IndividualName;

            ClosingYearText.Text =
                $"{SessionContext.SelectedStartDate:dd-MMM-yyyy} to " +
                $"{SessionContext.SelectedEndDate:dd-MMM-yyyy}";

            OpeningYearText.Text =
                $"{SessionContext.SelectedEndDate.AddDays(1):dd-MMM-yyyy} to " +
                $"{SessionContext.SelectedEndDate.AddYears(1):dd-MMM-yyyy}";
            try
            {
                var factory = new OleDbConnectionFactory(
                    Options.Create(new OleDbOptions
                    {
                        ConnectionString = SessionContext.ConnectionString
                    }));

                var service = new CloseBooksService(factory);
                service.GetClosingDiagnostic(
                SessionContext.SelectedYearId,
                SessionContext.RetainedEarningsId,
                snapshot =>
                {
                    TrialBalanceText.Text =
                        $"{snapshot.TrialBalanceDifference:N2}";

                    UnixValidationText.Text =
                        snapshot.UnbalancedUnixCount.ToString();

                    RetainedEarningsText.Text =
                        $"{snapshot.RetainedEarningsBalance:N2}";

                    bool checksPassed = snapshot.Status.StartsWith(
                        "BASIC CHECKS PASSED",
                        StringComparison.Ordinal);

                    _validationPassed =
                        checksPassed &&
                        snapshot.TrialBalanceDifference == 0m &&
                        snapshot.UnbalancedUnixCount == 0 &&
                        snapshot.RetainedEarningsBalance == 0m &&
                        snapshot.ExistingClosingEntries == 0;

                    ClosingStatusText.Text = checksPassed
                        ? "BASIC CHECKS PASSED"
                        : "NOT ELIGIBLE";

                    ClosingStatusText.Foreground = checksPassed
                        ? System.Windows.Media.Brushes.ForestGreen
                        : System.Windows.Media.Brushes.Firebrick;

                    ClosingStatusText.ToolTip =
                        $"{snapshot.Status}\n" +
                        $"Existing Closing Entries: " +
                        $"{snapshot.ExistingClosingEntries}";
                });
                // Do not generate closing entries
                // when pre-closing validation has failed.

                if (!_validationPassed)
                {
                    ClosingEntriesGrid.ItemsSource = null;

                    _previewReady = false;

                    UpdateCloseBooksButtonState();

                    return;
                }
                var entries = service.BuildClosingEntriesDraft(
                    SessionContext.SelectedYearId,
                    SessionContext.RetainedEarningsId);

                LoadClosingEntries(entries);

                _previewReady = entries.Count > 0;

                UpdateCloseBooksButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Closing Books Preview",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        private void UpdateCloseBooksButtonState()
        {
            bool confirmation1 =
                ConfirmYesTextBox.Text.Trim() == "YES";

            bool confirmation2 =
                ConfirmCloseTextBox.Text.Trim() == "CLOSE";

            CloseBooksButton.IsEnabled =
                _validationPassed &&
                _previewReady &&
                confirmation1 &&
                confirmation2;
        }

        public void LoadClosingEntries(
            IReadOnlyList<ClosingEntryDraft> entries)
        {
            var displayEntries = entries
                .OrderBy(entry => entry.Period == ClosingEntryPeriod.OpeningYear)
                .Select(entry =>
                new ClosingEntryDisplay
                {
                    Period = entry.Period.ToString(),
                    Tdate = entry.Tdate,
                    Ref = entry.Ref,
                    Particulars = entry.Narration,
                    Amount = entry.Amount
                }).ToList();

            ClosingEntriesGrid.ItemsSource = displayEntries;

            ClosingCountText.Text =
                $"Closing Entries: {entries.Count(e =>
                    e.Period == ClosingEntryPeriod.ClosingYear)}";

            OpeningCountText.Text =
                $"Opening Entries: {entries.Count(e =>
                    e.Period == ClosingEntryPeriod.OpeningYear)}";

            ClosingDifferenceText.Text =
                $"Closing Difference: {entries
                    .Where(e => e.Period == ClosingEntryPeriod.ClosingYear)
                    .Sum(e => e.Amount):N2}";

            OpeningDifferenceText.Text =
                $"Opening Difference: {entries
                    .Where(e => e.Period == ClosingEntryPeriod.OpeningYear)
                    .Sum(e => e.Amount):N2}";
        }
        private void CloseBooksButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateCloseBooksButtonState();

            if (!CloseBooksButton.IsEnabled)
                return;

            var result = MessageBox.Show(
                $"Individual: {SessionContext.IndividualName}\n\n" +
                $"Financial Year:\n" +
                $"{SessionContext.SelectedStartDate:dd-MMM-yyyy} to " +
                $"{SessionContext.SelectedEndDate:dd-MMM-yyyy}\n\n" +
                "Closing entries will be posted.\n" +
                "Opening entries will be created for the next year.\n" +
                "HistoricalData will be recorded.\n\n" +
                "Make sure you have backed up the ACCDB database.\n\n" +
                "I UNDERSTAND THE RISKS.\n\n" +
                "Do you wish to proceed?",
                "FINAL CONFIRMATION - CLOSING BOOKS",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
                return;

            // Prevent accidental double-click posting.
            CloseBooksButton.IsEnabled = false;
            ConfirmYesTextBox.IsEnabled = false;
            ConfirmCloseTextBox.IsEnabled = false;

            try
            {
                var factory = new OleDbConnectionFactory(
                    Options.Create(new OleDbOptions
                    {
                        ConnectionString = SessionContext.ConnectionString
                    }));

                var service = new CloseBooksService(factory);

                /*                var result = service.PostClosingBooks(
                                    SessionContext.SelectedYearId,
                                    SessionContext.RetainedEarningsId);*/

                var closingResult = service.PostClosingBooks(
                        SessionContext.SelectedYearId,
                        SessionContext.RetainedEarningsId);
                // STEP 7F.14 - Refresh selected financial year.

                DateTime newStartDate =
                    SessionContext.SelectedEndDate.Date.AddDays(1);

                DateTime newEndDate =
                    newStartDate.AddYears(1).AddDays(-1);

                SessionContext.SelectedYearId = closingResult.NewYearId;
                SessionContext.MaxYearId = closingResult.NewYearId;

                SessionContext.SelectedStartDate = newStartDate;
                SessionContext.SelectedEndDate = newEndDate;

                MessageBox.Show(
                    "CLOSING BOOKS COMPLETED SUCCESSFULLY!\n\n" +
                    $"New YearId: {closingResult.NewYearId}\n" +
                    $"UNIX: {closingResult.Unix}\n" +
                    $"Journal Entries: {closingResult.EntriesPosted}\n" +
                    $"HistoricalData Records: {closingResult.HistoricalRows}\n\n" +
                    "All entries have been committed.",
                    "LedgerLens 2.0 - Closing Books",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "CLOSING BOOKS FAILED.\n\n" +
                    ex.Message + "\n\n" +
                    "Check the database before attempting again.",
                    "Closing Books - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.ShowFinancialYearSelection();
            }
        }
    }
}