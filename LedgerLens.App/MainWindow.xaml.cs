using LedgerLens.App.Session;
using LedgerLens.App.ViewModels;
using LedgerLens.App.Views;
using LedgerLens.Data;
using LedgerLens.Data.Repositories;
using Microsoft.Extensions.Options;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LedgerLens.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
    private void SelectIndividual_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            FileName = "Document",
            DefaultExt = ".accdb",
            Filter = "Access Database File (.accdb)|*.accdb"
        };

        bool? result = dialog.ShowDialog();

        if (result == true)
        {
            SessionContext.DatabaseFileName = dialog.FileName;

            SessionContext.ConnectionString =
                $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={SessionContext.DatabaseFileName};";

            SessionContext.DatabaseFileName = dialog.FileName;

            SessionContext.ConnectionString =
                $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={SessionContext.DatabaseFileName};";

            var options = Options.Create(new OleDbOptions
            {
                ConnectionString = SessionContext.ConnectionString
            });

            var factory = new OleDbConnectionFactory(options);

            var individualRepository = new IndividualRepository(factory);

            var individual = individualRepository.GetIndividual();

            if (individual == null)
            {
                MessageBox.Show("No individual details found in this database.");
                return;
            }

            SessionContext.IndividualName = individual.IndividualName;
            SessionContext.PAN = individual.PANNumber;
            var accountYearRepository = new AccountYearRepository(factory);

            var years = accountYearRepository.GetAccountingYears();

            if (years.Count == 0)
            {
                MessageBox.Show("No accounting years found in this database.");
                return;
            }

            var latestYear = years[^1];

            SessionContext.MaxYearId = latestYear.YearId;
            SessionContext.SelectedYearId = latestYear.YearId;
            SessionContext.SelectedStartDate = latestYear.StartDate;
            SessionContext.SelectedEndDate = latestYear.EndDate;
            SessionContext.InterestAccountCode = individual.InterestAccountCode;
            SessionContext.InterestAccountDesc = individual.InterestAccountDesc;

            SessionContext.LTCGAccountCode = individual.LTCGAccountCode;
            SessionContext.LTCGAccountDesc = individual.LTCGAccountDesc;

            SessionContext.STCGAccountCode = individual.STCGAccountCode;
            SessionContext.STCGAccountDesc = individual.STCGAccountDesc;

            SessionContext.LTCLAccountCode = individual.LTCLAccountCode;
            SessionContext.LTCLAccountDesc = individual.LTCLAccountDesc;

            SessionContext.STCLAccountCode = individual.STCLAccountCode;
            SessionContext.STCLAccountDesc = individual.STCLAccountDesc;

            SessionContext.RetainedEarningsId = individual.RetainedEarningsId;

            var selectYearView = new SelectYearView
            {
                DataContext = new SelectYearViewModel(years)
            };

            ContentArea.Content = selectYearView;



        }
    }

    private void LedgerAccounts_Click(object sender, RoutedEventArgs e)
    {
        var options = Options.Create(new OleDbOptions
        {
            ConnectionString = SessionContext.ConnectionString
        });

        var factory = new OleDbConnectionFactory(options);

        var repository = new LedgerAccountRepository(factory);

        var view = new LedgerAccountsView
        {
            DataContext = new LedgerAccountsViewModel(repository)
        };

        ContentArea.Content = view;
    }
}