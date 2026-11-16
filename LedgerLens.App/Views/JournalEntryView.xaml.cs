using LedgerLens.Data.Models;
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
    }
}
