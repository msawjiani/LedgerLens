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
using System.Windows.Shapes;



namespace LedgerLens.App.Views
{
    public partial class LedgerAccountsView : UserControl
    {
        public LedgerAccountsView()
        {
            InitializeComponent();
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LedgerAccountsViewModel viewModel)
            {
                viewModel.SaveSelectedAccount();
            }
        }
        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LedgerAccountsViewModel viewModel)
            {
                viewModel.NewAccount();
            }
        }
    }
}