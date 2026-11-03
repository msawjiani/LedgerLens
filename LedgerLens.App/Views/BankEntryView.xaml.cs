using LedgerLens.App.ViewModels;
using LedgerLens.Data.Models;
using LedgerLens.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    /// Interaction logic for BankEntryView.xaml
    /// </summary>
    /// 


    public partial class BankEntryView : UserControl
    {
        public SubledgerRepository? SubledgerRepository { get; set; }
        private FDBalance? _selectedFD;
        private decimal _fdReceiptPrincipal = 0m;
        private decimal _fdReceiptInterest = 0m;
        private bool _fdReceiptFullyClosed = false;

        public BankEntryView()
        {
            InitializeComponent();
            PreviewKeyDown += BankEntryView_PreviewKeyDown;
        }
        private void BankEntryView_PreviewKeyDown(
                                                    object sender,
                                                    KeyEventArgs e)
        {
            if (DataContext is not BankEntryViewModel vm)
                return;

            if (e.Key == Key.F2)
            {
                vm.IncrementDate();
                e.Handled = true;
            }
            else if (e.Key == Key.F3)
            {
                vm.IncrementReference();
                e.Handled = true;
            }
        }
        private void GLAccountCombo_Loaded(
                                    object sender,
                                    RoutedEventArgs e)
        {
            if (GLAccountCombo.Template.FindName(
                "PART_EditableTextBox",
                GLAccountCombo) is TextBox textBox)
            {
                textBox.TextChanged -= GLAccountSearch_TextChanged;
                textBox.TextChanged += GLAccountSearch_TextChanged;
            }
        }

        private void GLAccountSearch_TextChanged(
                          object sender,
                          TextChangedEventArgs e)
        {
            if (DataContext is not BankEntryViewModel vm)
                return;

            if (sender is not TextBox textBox)
                return;

            string searchText = textBox.Text;

            // If the displayed text matches the selected account,
            // this is not a new search.
            if (vm.SelectedGLAccount != null &&
                string.Equals(
                    searchText,
                    vm.SelectedGLAccount.Account,
                    StringComparison.Ordinal))
            {
                return;
            }

            // Otherwise, invalidate the previous selection.
            vm.SelectedGLAccount = null;

            vm.FilterGLAccounts(searchText);

            GLAccountCombo.IsDropDownOpen = true;
        }

        private void Subledger_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BankEntryViewModel vm)
                return;

            if (vm.SelectedGLAccount == null)
                return;

            if (SubledgerRepository == null)
            {
                MessageBox.Show(
                    "Subledger repository is not available.",
                    "FD Selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }
            decimal receiptAmount = 0m;

            if (vm.IsReceipt &&
                !decimal.TryParse(vm.Amount, out receiptAmount))
            {
                MessageBox.Show(
                    "Please enter a valid receipt amount before selecting the FD.",
                    "FD Receipt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (vm.IsReceipt && receiptAmount <= 0)
            {
                MessageBox.Show(
                    "Receipt amount must be greater than zero.",
                    "FD Receipt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var window = new FDSelectionWindow(
                vm.SelectedGLAccount.AccountId,
                vm.SelectedGLAccount.Account,
                SubledgerRepository,
                vm.IsReceipt,
                receiptAmount)
                {
                    Owner = Window.GetWindow(this)
                };

            if (window.ShowDialog() == true && window.SelectedFD != null)
            {
                _selectedFD = window.SelectedFD;

                if (sender is Button button)
                    button.ToolTip = _selectedFD.Subaccount;

                if (vm.IsPayment)
                {
                    // Existing FD Payment behaviour
                    vm.Narration = $"Being {_selectedFD.Subaccount}";
                }
                else if (vm.IsReceipt)
                {
                    _fdReceiptPrincipal = window.PrincipalAmount;
                    _fdReceiptInterest = window.InterestAmount;
                    _fdReceiptFullyClosed = window.IsFullyClosed;

                    vm.Narration =
                        $"Being {_selectedFD.Subaccount} - " +
                        $"Principal Rs.{_fdReceiptPrincipal:N2} + " +
                        $"Interest Rs.{_fdReceiptInterest:N2}";
                }
            }
        }
        private void GLAccountCombo_SelectionChanged(
                                                        object sender,
                                                        SelectionChangedEventArgs e)
        {
            _selectedFD = null;

            if (SLButton != null)
                SLButton.ToolTip = null;
        }
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BankEntryViewModel vm)
                return;

            if (!vm.IsReceipt && !vm.IsPayment)
            {
                MessageBox.Show(
                    "Please select Receipt or Payment.",
                    "Bank Entry",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            if (vm.SelectedGLAccount?.SubledgerFlag == "SL" && _selectedFD == null)
            {
                MessageBox.Show(
                    "Please select a Fixed Deposit before saving.",
                    "FD Selection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }
            // Determine the appropriate posting method

            (bool Success, string Message) result;

            if (vm.IsReceipt)
            {
                if (vm.SelectedGLAccount?.SubledgerFlag == "SL")
                {
                    // FD Receipt
                    result = await vm.SaveFDReceiptAsync(
                        _selectedFD!.SubledgerId,
                        _fdReceiptPrincipal,
                        _fdReceiptInterest,
                        _fdReceiptFullyClosed);
                }
                else
                {
                    // Ordinary Bank Receipt
                    result = await vm.SaveReceiptAsync();
                }
            }
            else
            {
                // Existing Bank Payment behaviour
                result = await vm.SavePaymentAsync(
                    _selectedFD?.SubledgerId);
            }

            MessageBox.Show(
                result.Message,
                result.Success ? "Entry Saved" : "Validation / Save Error",
                MessageBoxButton.OK,
                result.Success
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);
            if (result.Success)
            {
                _selectedFD = null;

                _fdReceiptPrincipal = 0m;
                _fdReceiptInterest = 0m;
                _fdReceiptFullyClosed = false;

                SLButton.ToolTip = null;
            }
        }

    }

}
