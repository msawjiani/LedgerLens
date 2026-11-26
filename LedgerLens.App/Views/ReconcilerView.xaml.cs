using LedgerLens.App.Models;
using LedgerLens.App.Session;
using LedgerLens.Data;
using LedgerLens.Data.Repositories;
using LedgerLens.Services;
using Microsoft.Extensions.Options;
using System;
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
using System.Linq;


namespace LedgerLens.App.Views
{
    /// <summary>
    /// Interaction logic for ReconcilerView.xaml
    /// </summary>
    public partial class ReconcilerView : UserControl
    {

        private string? _targetConnectionString;
        private int? _targetYearId;
        private Brush? _selectedMarkerBrush;
        public ReconcilerView()
        {
            InitializeComponent();

            // STEP 8E - DISCO Manual Colour Markers

            BlueMarkerButton.Click += MarkerButton_Click;
            YellowMarkerButton.Click += MarkerButton_Click;
            OrangeMarkerButton.Click += MarkerButton_Click;
            PurpleMarkerButton.Click += MarkerButton_Click;
            PinkMarkerButton.Click += MarkerButton_Click;
            TealMarkerButton.Click += MarkerButton_Click;
            ClearMarkerButton.Click += MarkerButton_Click;

            // Double-click to paint a transaction.

            SourceGrid.MouseDoubleClick += ReconcilerGrid_MouseDoubleClick;
            TargetGrid.MouseDoubleClick += ReconcilerGrid_MouseDoubleClick;

            SourceIndividualText.Text =
                SessionContext.IndividualName;
            SelectTargetButton.Click += SelectTargetButton_Click;
            FinancialYearText.Text =
                $"{SessionContext.SelectedStartDate:dd-MMM-yyyy} to " +
                $"{SessionContext.SelectedEndDate:dd-MMM-yyyy}";
            // STEP 8B.3 - Load Source Ledger Accounts

            var sourceFactory = new OleDbConnectionFactory(
                Options.Create(new OleDbOptions
                {
                    ConnectionString = SessionContext.ConnectionString
                }));

            var sourceAccountRepository =
                new LedgerAccountRepository(sourceFactory);

            SourceAccountCombo.ItemsSource =
                sourceAccountRepository.GetAll();

            SourceAccountCombo.DisplayMemberPath = "Account";
            SourceAccountCombo.SelectedValuePath = "AccountId";
            SourceAccountCombo.SelectionChanged +=
                SourceAccountCombo_SelectionChanged;
        }

        private void SourceAccountCombo_SelectionChanged(
                object sender,
                SelectionChangedEventArgs e)
        {
            SourceGrid.ItemsSource = null;

            if (SourceAccountCombo.SelectedValue is not int accountId)
                return;

            try
            {
                var sourceFactory = new OleDbConnectionFactory(
                    Options.Create(new OleDbOptions
                    {
                        ConnectionString = SessionContext.ConnectionString
                    }));

                var repository =
                    new TransactionRepository(sourceFactory);

                var entries = repository.GetBankEntries(
                    accountId,
                    SessionContext.SelectedYearId);

                var displayEntries = entries.Select(entry =>
                    new ReconcilerTransactionRow
                    {
                        TransactionId = entry.TransactionId,
                        Unix = entry.Unix,
                        TDate = entry.TDate,
                        Ref = entry.Ref,
                        Particulars = entry.Particulars,
                        Amount = entry.Amount,
                        Narration = entry.Narration
                    }).ToList();

                SourceGrid.ItemsSource = displayEntries;
                MatchReconcilerEntries();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Reconciler - Source Transactions",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        private void SelectTargetButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".accdb",
                Filter = "Access Database File (.accdb)|*.accdb",
                Title = "Select Target Individual"
            };

            if (dialog.ShowDialog() != true)
                return;

            // Reset the previous target selection.
            _targetConnectionString = null;
            _targetYearId = null;

            TargetIndividualText.Text = "Not Selected";

            TargetAccountCombo.ItemsSource = null;
            TargetGrid.ItemsSource = null;

            try
            {
                string connectionString =
                    $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={dialog.FileName};";

                var factory = new OleDbConnectionFactory(
                    Options.Create(new OleDbOptions
                    {
                        ConnectionString = connectionString
                    }));

                var individualRepository =
                    new IndividualRepository(factory);

                var individual =
                    individualRepository.GetIndividual();

                if (individual == null)
                    throw new InvalidOperationException(
                        "No individual details found in the selected database.");

                var yearRepository =
                    new AccountYearRepository(factory);

                var years = yearRepository.GetAccountingYears();

                var matchingYear = years.FirstOrDefault(y =>
                    y.StartDate.Date == SessionContext.SelectedStartDate.Date &&
                    y.EndDate.Date == SessionContext.SelectedEndDate.Date);

                if (matchingYear == null)
                    throw new InvalidOperationException(
                        "The target individual does not have a matching financial year.\n\n" +
                        $"Required: {SessionContext.SelectedStartDate:dd-MMM-yyyy} to " +
                        $"{SessionContext.SelectedEndDate:dd-MMM-yyyy}");

                _targetConnectionString = connectionString;
                _targetYearId = matchingYear.YearId;

                TargetIndividualText.Text = individual.IndividualName;
                // STEP 8B.4 - Load Target Ledger Accounts


                var targetAccountRepository =
                    new LedgerAccountRepository(factory);

                TargetAccountCombo.ItemsSource =
                    targetAccountRepository.GetAll();

                TargetAccountCombo.DisplayMemberPath = "Account";
                TargetAccountCombo.SelectedValuePath = "AccountId";

                // STEP 8C.4 - Load target transactions
                // when a target account is selected.

                TargetAccountCombo.SelectionChanged -=
                    TargetAccountCombo_SelectionChanged;

                TargetAccountCombo.SelectionChanged +=
                    TargetAccountCombo_SelectionChanged;

                MessageBox.Show(
                    $"Target Individual: {individual.IndividualName}\n\n" +
                    $"Financial Year: {matchingYear.StartDate:dd-MMM-yyyy} to " +
                    $"{matchingYear.EndDate:dd-MMM-yyyy}\n\n" +
                    $"Target YearId: {matchingYear.YearId}",
                    "Reconciler - Target Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Reconciler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        private void TargetAccountCombo_SelectionChanged(
                object sender,
                SelectionChangedEventArgs e)
        {
            TargetGrid.ItemsSource = null;

            if (_targetConnectionString == null ||
                !_targetYearId.HasValue ||
                TargetAccountCombo.SelectedValue is not int accountId)
            {
                return;
            }

            try
            {
                var targetFactory = new OleDbConnectionFactory(
                    Options.Create(new OleDbOptions
                    {
                        ConnectionString = _targetConnectionString
                    }));

                var repository =
                    new TransactionRepository(targetFactory);
                var entries = repository.GetBankEntries(
                    accountId,
                    _targetYearId.Value);

                var displayEntries = entries.Select(entry =>
                    new ReconcilerTransactionRow
                    {
                        TransactionId = entry.TransactionId,
                        Unix = entry.Unix,
                        TDate = entry.TDate,
                        Ref = entry.Ref,
                        Particulars = entry.Particulars,
                        Amount = entry.Amount,
                        Narration = entry.Narration
                    }).ToList();

                TargetGrid.ItemsSource = displayEntries;
                MatchReconcilerEntries();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Reconciler - Target Transactions",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        private void MatchReconcilerEntries()
        {
            if (SourceGrid.ItemsSource
                    is not List<ReconcilerTransactionRow> sourceEntries ||
                TargetGrid.ItemsSource
                    is not List<ReconcilerTransactionRow> targetEntries)
                return;

            // Automatic reconciliation
            ReconcilerMatcher.Match(sourceEntries, targetEntries);

            // Count matched pairs from SOURCE only.
            // Counting both grids would double the matches.

            int exactMatches = sourceEntries.Count(
                x => x.MatchType == 1);

            int nearMatches = sourceEntries.Count(
                x => x.MatchType == 2);

            // Count unmatched transactions on BOTH sides.

            int unmatchedSource = sourceEntries.Count(
                x => x.MatchType == 0);

            int unmatchedTarget = targetEntries.Count(
                x => x.MatchType == 0);

            // Debit positive, Credit negative.
            // Opposite entries should cancel.

            decimal sourceTotal = sourceEntries.Sum(x => x.Amount);
            decimal targetTotal = targetEntries.Sum(x => x.Amount);

            decimal difference = sourceTotal + targetTotal;

            // Display reconciliation summary.

            ExactMatchesText.Text = exactMatches.ToString();

            NearMatchesText.Text = nearMatches.ToString();

            UnmatchedText.Text =
                $"{unmatchedSource} / {unmatchedTarget}";

            DifferenceText.Text =
                difference.ToString("N2");
        }

        private void MarkerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button == ClearMarkerButton)
            {
                _selectedMarkerBrush = null;
            }
            else
            {
                _selectedMarkerBrush = button.Background;
            }
        }

        private void ReconcilerGrid_MouseDoubleClick(
    object sender,
    MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject element)
                return;

            // Find the actual DataGrid row that was double-clicked.

            while (element != null &&
                   element is not DataGridRow)
            {
                element = VisualTreeHelper.GetParent(element);
            }

            if (element is not DataGridRow gridRow)
                return;

            if (gridRow.Item is not ReconcilerTransactionRow row)
                return;

            // CLEAR restores the automatic matching colour.

            if (_selectedMarkerBrush == null)
            {
                RestoreAutomaticColor(row);
                return;
            }

            // Clicking a row already painted with the selected
            // manual colour toggles that marking off.

            if (ReferenceEquals(row.RowColor, _selectedMarkerBrush))
            {
                RestoreAutomaticColor(row);
            }
            else
            {
                row.RowColor = _selectedMarkerBrush;
            }
        }

        private void RestoreAutomaticColor(
    ReconcilerTransactionRow row)
        {
            switch (row.MatchType)
            {
                case 1:
                    // Exact match
                    row.RowColor = new SolidColorBrush(
                        Color.FromRgb(200, 245, 200));
                    break;

                case 2:
                    // Match within 2 days
                    row.RowColor = new SolidColorBrush(
                        Color.FromRgb(205, 245, 235));
                    break;

                default:
                    // Unmatched
                    row.RowColor = Brushes.White;
                    break;
            }
        }


    }
}
