using LedgerLens.App.ViewModels;
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
    /// Interaction logic for BankEntryView.xaml
    /// </summary>
    public partial class BankEntryView : UserControl
    {
        public BankEntryView()
        {
            InitializeComponent();
        }
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BankEntryViewModel vm)
                return;

            if (!vm.IsReceipt)
            {
                MessageBox.Show(
                    "Only Receipt posting is enabled at this stage.",
                    "Bank Entry",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var result = await vm.SaveReceiptAsync();

            MessageBox.Show(
                result.Message,
                result.Success ? "Receipt Saved" : "Validation / Save Error",
                MessageBoxButton.OK,
                result.Success
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);
        }

    }

}
