using LedgerLens.App.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace LedgerLens.App.Views
{
    public partial class SubledgerView : UserControl
    {
        public SubledgerView()
        {
            InitializeComponent();
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SubledgerViewModel viewModel)
            {
                viewModel.NewSubledger();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SubledgerViewModel viewModel)
            {
                viewModel.SaveSubledger();
            }
        }
    }
}