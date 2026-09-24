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
using LedgerLens.App.ViewModels;

namespace LedgerLens.App.Views
{
    /// <summary>
    /// Interaction logic for CompaniesView.xaml
    /// </summary>
    public partial class CompaniesView : UserControl
    {
        public CompaniesView()
        {
            InitializeComponent();
        }
        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CompaniesViewModel viewModel)
            {
                viewModel.NewCompany();
            }
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CompaniesViewModel viewModel)
            {
                viewModel.SaveCompany();
            }
        }
    }

}
