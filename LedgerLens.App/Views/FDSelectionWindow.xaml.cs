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
using System.Windows.Shapes;
using System.ComponentModel;
using LedgerLens.Data.Repositories;

namespace LedgerLens.App.Views
{
    /// <summary>
    /// Interaction logic for FDSelectionWindow.xaml
    /// </summary>
    public partial class FDSelectionWindow : Window
    {
        private readonly int _accountId;

        public FDSelectionWindow(
            int accountId,
            string glAccountName,
            SubledgerRepository subledgerRepository)
            {
                InitializeComponent();

                _accountId = accountId;

                GLAccountNameText.Text = $"GL Account: {glAccountName}";

                FDGrid.ItemsSource =
                    subledgerRepository.GetActiveFDBalances(_accountId);
            }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
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
    }
}
