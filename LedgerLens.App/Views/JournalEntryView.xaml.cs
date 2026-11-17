using LedgerLens.App.Session;
using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;
using LedgerLens.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Interaction logic for JournalEntryView.xaml
    /// </summary>
    public partial class JournalEntryView : UserControl
    {
        public JournalEntryView()
        {
            InitializeComponent();
            
        }
        public ITransactionRepository? JournalTransactionRepository
        {
            get;
            set;
        }

        private void JournalGrid_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.F4)
                return;

            DependencyObject? current =
                Keyboard.FocusedElement as DependencyObject;

            ComboBox? combo = null;

            while (current != null)
            {
                // Focus is already inside the ComboBox
                if (current is ComboBox cb)
                {
                    combo = cb;
                    break;
                }

                // Focus is on a DataGrid cell
                if (current is DataGridCell cell)
                {
                    // Only the Account column
                    if (cell.Column == JournalGrid.Columns[0])
                    {
                        combo = FindVisualChild<ComboBox>(cell);
                    }

                    break;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            if (combo == null)
                return;

            combo.Focus();
            combo.IsDropDownOpen = !combo.IsDropDownOpen;

            e.Handled = true;
        }
        private void CheckSave_Click(
                object sender,
                RoutedEventArgs e)
        {
            if (DataContext is not JournalEntryViewModel vm)
                return;
            MessageBox.Show(
                $"IsSaveEnabled: {vm.IsSaveEnabled}\n\n" +
                vm.GetSaveDiagnostic(),
                "LedgerLens - Journal Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        private void DeleteJournalLine_Click(
                object sender,
                RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.DataContext is not JournalLine line ||
                DataContext is not JournalEntryViewModel vm)
            {
                return;
            }

            // Commit any pending grid edits.
            if (!JournalGrid.CommitEdit(
                    DataGridEditingUnit.Cell, true))
                return;

            if (!JournalGrid.CommitEdit(
                    DataGridEditingUnit.Row, true))
                return;

            // Remove the selected journal line.
            vm.JournalLines.Remove(line);

            // Always retain at least one blank row.
            vm.EnsureTrailingBlankRow();
        }
        private static T? FindVisualChild<T>(
                        DependencyObject parent)
                        where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child =
                    VisualTreeHelper.GetChild(parent, i);

                if (child is T match)
                    return match;

                T? result = FindVisualChild<T>(child);

                if (result != null)
                    return result;
            }

            return null;
        }
        private void SaveJournal_Click(
            object sender,
            RoutedEventArgs e)
        {
            // Commit pending DataGrid edits.

            if (!JournalGrid.CommitEdit(
                    DataGridEditingUnit.Cell, true))
                return;

            if (!JournalGrid.CommitEdit(
                    DataGridEditingUnit.Row, true))
                return;


            if (DataContext is not JournalEntryViewModel vm)
                return;


            // Final ViewModel validation.

            if (!vm.IsSaveEnabled)
            {
                MessageBox.Show(
                    vm.GetSaveDiagnostic(),
                    "Journal Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            if (JournalTransactionRepository == null)
            {
                MessageBox.Show(
                    "TransactionRepository is not configured.",
                    "Journal Entry",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }


            // Parse the Journal date.

            if (!DateTime.TryParseExact(
                    vm.DateInput.Trim(),
                    new[] { "d-MMM-yyyy", "dd-MMM-yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime journalDate))
            {
                MessageBox.Show("Invalid Journal date.");
                return;
            }


            // Convert Debit and Credit into signed amounts.

            var lines = vm.JournalLines
                .Where(x => x.AccountId.HasValue)
                .Select(x => (
                    AccountId: x.AccountId!.Value,
                    Amount: (x.Debit ?? 0m) -
                            (x.Credit ?? 0m)
                ))
                .ToList();


            // Confirm the database before saving.

            var confirmation = MessageBox.Show(
                $"Database:\n{SessionContext.DatabaseFileName}\n\n" +
                $"Date: {journalDate:dd-MMM-yyyy}\n" +
                $"Reference: {vm.RefInput}\n\n" +
                $"Entries: {lines.Count}\n" +
                $"Debit: {vm.TotalDebit:N2}\n" +
                $"Credit: {vm.TotalCredit:N2}\n\n" +
                "POST THIS JOURNAL?",
                "Confirm Journal Posting",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);


            if (confirmation != MessageBoxResult.Yes)
                return;


            // Actual database posting.

            try
            {
                long unix = JournalTransactionRepository.PostJournal(
                    SessionContext.SelectedYearId,
                    journalDate,
                    vm.RefInput,
                    vm.NarrationInput,
                    lines);


                MessageBox.Show(
                    $"JOURNAL SAVED SUCCESSFULLY!\n\n" +
                    $"UNIX: {unix}\n" +
                    $"Reference: {vm.RefInput}\n" +
                    $"Journal Lines: {lines.Count}\n\n" +
                    $"Debit: {vm.TotalDebit:N2}\n" +
                    $"Credit: {vm.TotalCredit:N2}",
                    "LedgerLens - Journal Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);


                // Clear the saved entry to prevent duplicate posting.
                vm.RefInput = string.Empty;
                vm.NarrationInput = string.Empty;

                vm.JournalLines.Clear();

                for (int i = 0; i < 3; i++)
                {
                    vm.JournalLines.Add(new JournalLine());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Journal could not be posted.\n\n" +
                    ex.Message,
                    "Journal Posting Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
