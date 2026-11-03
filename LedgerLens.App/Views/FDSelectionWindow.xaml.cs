using LedgerLens.Data.Models;
using LedgerLens.Data.Repositories;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;


namespace LedgerLens.App.Views
{
    /// <summary>
    /// Interaction logic for FDSelectionWindow.xaml
    /// </summary>
    public partial class FDSelectionWindow : Window
    {
        private readonly int _accountId;
        private readonly decimal _receiptAmount;
        public FDBalance? SelectedFD { get; private set; }


        public decimal PrincipalAmount { get; private set; }
        public decimal InterestAmount { get; private set; }

        public bool IsFullyClosed { get; private set; }
        public FDSelectionWindow(
            int accountId,
            string glAccountName,
            SubledgerRepository subledgerRepository,
            bool isReceipt = false,
            decimal receiptAmount = 0m)
            {
                InitializeComponent();
                _receiptAmount = receiptAmount;
                TotalReceiptTextBox.Text = receiptAmount.ToString("N2");
                PrincipalTextBox.Text = receiptAmount.ToString("N2");


            _accountId = accountId;

                GLAccountNameText.Text = $"GL Account: {glAccountName}";

                FDGrid.ItemsSource =
                    subledgerRepository.GetActiveFDBalances(_accountId);

                // Show Interest textbox only for FD Receipts
                ReceiptPanel.Visibility = isReceipt
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void FDGrid_MouseDoubleClick(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FDGrid.SelectedItem is not FDBalance fd)
                return;

            // Additional calculations and validation for FD Receipts only
            if (ReceiptPanel.Visibility == Visibility.Visible)
            {
                if (!decimal.TryParse(
                    InterestTextBox.Text,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal interest))
                {
                    MessageBox.Show(
                        "Please enter a valid interest amount.",
                        "FD Receipt",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                decimal principal = _receiptAmount - interest;

                if (interest < 0 || principal < 0)
                {
                    MessageBox.Show(
                        "Interest and Principal cannot be negative.",
                        "FD Receipt",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (principal > fd.TotalFD)
                {
                    MessageBox.Show(
                        "Principal cannot exceed the outstanding FD balance.",
                        "FD Receipt",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                // Full Closure validation
                if (FullyClosedCheckBox.IsChecked == true &&
                    principal != fd.TotalFD)
                {
                    MessageBox.Show(
                        $"Cannot fully close this FD.\n\n" +
                        $"FD Outstanding: Rs.{fd.TotalFD:N2}\n" +
                        $"Principal Withdrawn: Rs.{principal:N2}\n\n" +
                        "For full closure, the entire outstanding principal must be withdrawn.",
                        "FD Full Closure",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                InterestAmount = interest;
                PrincipalAmount = principal;
            }

            IsFullyClosed =
                ReceiptPanel.Visibility == Visibility.Visible &&
                FullyClosedCheckBox.IsChecked == true;

            SelectedFD = fd;
            DialogResult = true;
        }
        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (DialogResult == true)
                return;
            var result = MessageBox.Show(
                "You have not selected a Fixed Deposit.\n\n" +
                "Do you want to close this window?",
                "No Fixed Deposit Selected",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
        }
        private void InterestTextBox_LostFocus(
            object sender,
            RoutedEventArgs e)
        {
            string input = InterestTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                InterestTextBox.Text = "0.00";
                return;
            }

            try
            {
                decimal result;

                if (input.StartsWith("="))
                {
                    string expression = input.Substring(1).Trim();

                    // Support subtraction and addition
                    int operatorIndex = expression.IndexOfAny(
                        new char[] { '+', '-' }, 1);

                    if (operatorIndex < 0)
                        throw new FormatException();

                    decimal first = decimal.Parse(
                        expression.Substring(0, operatorIndex),
                        System.Globalization.CultureInfo.InvariantCulture);

                    decimal second = decimal.Parse(
                        expression.Substring(operatorIndex + 1),
                        System.Globalization.CultureInfo.InvariantCulture);

                    char operation = expression[operatorIndex];

                    result = operation == '+'
                        ? first + second
                        : first - second;
                }
                else
                {
                    result = decimal.Parse(
                        input,
                        System.Globalization.CultureInfo.InvariantCulture);
                }

                if (result < 0)
                {
                    MessageBox.Show(
                        "Interest cannot be negative.",
                        "Invalid Interest",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                InterestTextBox.Text = result.ToString(
                    "0.00",
                    System.Globalization.CultureInfo.InvariantCulture);
                
                decimal principal = _receiptAmount - result;
                PrincipalTextBox.Text = principal.ToString("N2");

            }
            catch (Exception ex) when (
                ex is FormatException ||
                ex is OverflowException)
            {
                MessageBox.Show(
                    "Enter a valid amount or calculation.\n\n" +
                    "Examples:\n5000\n=145200-138000",
                    "Invalid Interest",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

    }
}
