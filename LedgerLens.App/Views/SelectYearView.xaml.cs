using LedgerLens.App.Session;
using LedgerLens.Data.Models;
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
            if (DataContext is not ViewModels.SelectYearViewModel vm)
                return;

            if (vm.SelectedYear is not AccountingYear selectedYear)
                return;

            SessionContext.SelectedYearId = selectedYear.YearId;
            SessionContext.SelectedStartDate = selectedYear.StartDate;
            SessionContext.SelectedEndDate = selectedYear.EndDate;

            bool isLatestYear = SessionContext.SelectedYearId == SessionContext.MaxYearId;

            MessageBox.Show(
                $"{SessionContext.IndividualName}'s books opened\n" +
                $"{SessionContext.SelectedStartDate:dd-MMM-yyyy} to {SessionContext.SelectedEndDate:dd-MMM-yyyy}\n\n" +
                (isLatestYear ? "Entries allowed." : "Read-only year.")
            );
        }
    }
}