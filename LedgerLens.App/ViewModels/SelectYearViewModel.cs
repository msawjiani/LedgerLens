using LedgerLens.App.Session;
using LedgerLens.Data.Models;
using System.Collections.ObjectModel;

namespace LedgerLens.App.ViewModels
{
    public class SelectYearViewModel
    {
        public string Title { get; }

        public ObservableCollection<AccountingYear> AccountingYears { get; }

        public AccountingYear? SelectedYear { get; set; }

        public SelectYearViewModel(List<AccountingYear> years)
        {
            Title = $"{SessionContext.IndividualName}'s Accounts";

            AccountingYears = new ObservableCollection<AccountingYear>(years);

            if (AccountingYears.Count > 0)
            {
                SelectedYear = AccountingYears[^1];
            }
        }
    }
}