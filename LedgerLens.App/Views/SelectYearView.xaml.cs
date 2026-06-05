using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace LedgerLens.App.Views
{
    public partial class SelectYearView : UserControl
    {
        public SelectYearView()
        {
            InitializeComponent();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("Open books double-clicked");
        }
    }
}