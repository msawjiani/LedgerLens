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
using LedgerLens.Services;


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
    private void SharePurchase_Click(object sender, RoutedEventArgs e)
    {
        var options = Options.Create(new OleDbOptions
        {
            ConnectionString = SessionContext.ConnectionString
        });

        var factory = new OleDbConnectionFactory(options);

        var ledgerRepository = new LedgerAccountRepository(factory);

        var accounts = ledgerRepository.GetAll();

        var view = new SharePurchaseView();

        view.ShareTradeService = new ShareTradeService(
                factory,
                new TransactionRepository(factory));

        // CREDIT ACCOUNT
        view.CreditAccountCombo.ItemsSource = accounts;
        view.CreditAccountCombo.DisplayMemberPath = "Account";
        view.CreditAccountCombo.SelectedValuePath = "AccountId";

        // SHARES ACCOUNT
        view.SharesAccountCombo.ItemsSource = accounts
            .Where(a => a.Category == "BS")
            .ToList();
        view.SharesAccountCombo.DisplayMemberPath = "Account";
        view.SharesAccountCombo.SelectedValuePath = "AccountId";
        // COMPANY GRID
        var companyRepository = new CompanyRepository(factory);

        view.SharesAccountCombo.SelectionChanged += (sender, args) =>
        {
            // Clear the previous selection
            view.CompaniesDataGrid.SelectedItem = null;
            view.CompaniesDataGrid.ItemsSource = null;

            if (view.SharesAccountCombo.SelectedValue is int accountId)
            {
                view.CompaniesDataGrid.ItemsSource =
                    companyRepository.GetByAccountId(accountId);
            }
        };

        view.SharesAccountCombo.DropDownOpened += (sender, args) =>
        {
            if (view.CompaniesDataGrid.ItemsSource == null &&
                view.SharesAccountCombo.SelectedValue is int accountId)
            {
                view.CompaniesDataGrid.ItemsSource =
                    companyRepository.GetByAccountId(accountId);
            }
        };

        ContentArea.Content = view;
    }
    private void BankEntry_Click(object sender, RoutedEventArgs e)
    {
        var options = Options.Create(new OleDbOptions
        {
            ConnectionString = SessionContext.ConnectionString
        });

        var factory = new OleDbConnectionFactory(options);

        var ledgerAccountRepository =
            new LedgerAccountRepository(factory);

        var transactionRepository =
            new TransactionRepository(factory);

        var subledgerRepository =
            new SubledgerRepository(factory);

        // Accounting service
        var bankEntryService =
            new BankEntryService(factory, transactionRepository);

        var view = new BankEntryView
        {
            SubledgerRepository = subledgerRepository,

            DataContext = new BankEntryViewModel(
                ledgerAccountRepository,
                transactionRepository,
                bankEntryService)
        };

        ContentArea.Content = view;
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

    private void Companies_Click(object sender, RoutedEventArgs e)
    {
        var options = Options.Create(new OleDbOptions
        {
            ConnectionString = SessionContext.ConnectionString
        });

        var factory = new OleDbConnectionFactory(options);

        var companyRepository = new CompanyRepository(factory);
        var ledgerAccountRepository = new LedgerAccountRepository(factory);

        var view = new CompaniesView
        {
            DataContext = new CompaniesViewModel(
                companyRepository,
                ledgerAccountRepository)
        };

        ContentArea.Content = view;
    }

    private void Subledger_Click(object sender, RoutedEventArgs e)
    {
        var options = Options.Create(new OleDbOptions
        {
            ConnectionString = SessionContext.ConnectionString
        });

        var factory = new OleDbConnectionFactory(options);

        var subledgerRepository = new SubledgerRepository(factory);
        var ledgerAccountRepository = new LedgerAccountRepository(factory);

        var view = new SubledgerView
        {
            DataContext = new SubledgerViewModel(
                subledgerRepository,
                ledgerAccountRepository)
        };

        ContentArea.Content = view;
    }

    private void Journal_Click(object sender, RoutedEventArgs e)
    {
        var options = Options.Create(new OleDbOptions
        {
            ConnectionString = SessionContext.ConnectionString
        });

        var factory = new OleDbConnectionFactory(options);

        var ledgerAccountRepository =
            new LedgerAccountRepository(factory);

        var view = new JournalEntryView
        {
            JournalTransactionRepository = new TransactionRepository(factory),

            DataContext =
             new JournalEntryViewModel(
                 ledgerAccountRepository)
        };

        ContentArea.Content = view;
    }
    private void ImportExcel_Click(object sender, RoutedEventArgs e)
    {
        var options = Options.Create(new OleDbOptions
        {
            ConnectionString = SessionContext.ConnectionString
        });

        var factory = new OleDbConnectionFactory(options);

        var view = new TurboSharePurchaseView
        {
            LedgerAccountRepository = new LedgerAccountRepository(factory),

            CompanyRepository = new CompanyRepository(factory),

            ShareTradeService = new ShareTradeService(
                factory,
                new TransactionRepository(factory))
        };

        ContentArea.Content = view;
    }
    // STEP 3B - Temporary Share Sales FIFO Retrieval Test

    // STEP 3D - Test FIFO Allocation

    // STEP 4B - Test Share Sales Capital Gains

    // STEP 5C.2 - Test Share Sale Posting Preparation

    private void TestShareLots()
    {
        var options = Options.Create(new OleDbOptions
        {
            ConnectionString = SessionContext.ConnectionString
        });

        var factory = new OleDbConnectionFactory(options);

        var service = new ShareSalePostingService(
                                                factory,
                                                new TransactionRepository(factory));

        var entries = service.PrepareSale(
            2551,
            new DateTime(2024, 11, 20),
            35m,
            180m);

        string result = string.Join(
            "\n",
            entries.Select(x =>
                $"Lot: {x.PurchaseTransId} | " +
                $"GL: {x.SharesAtCostAccountId} | " +
                $"Qty: {x.QtySold} | " +
                $"Cost: {x.PurchaseCost:N2} | " +
                $"Proceeds: {x.SaleProceeds:N2} | " +
                $"Gain/Loss: {x.GainLoss:N2} | " +
                $"{x.Classification}"));

        result +=
            $"\n\nTotal Cost: {entries.Sum(x => x.PurchaseCost):N2}" +
            $"\nTotal Proceeds: {entries.Sum(x => x.SaleProceeds):N2}" +
            $"\nNet Gain/Loss: {entries.Sum(x => x.GainLoss):N2}";

        MessageBox.Show(
            result,
            "LedgerLens - Share Sale Posting Preparation",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
    private void SubledgerJournal_Click(
        object sender,
        RoutedEventArgs e)
    {
        var options = Options.Create(new OleDbOptions
        {
            ConnectionString = SessionContext.ConnectionString
        });

        var factory = new OleDbConnectionFactory(options);
        TestShareLots();
        var view = new SubledgerJournalView
        {
            LedgerAccountRepository =
                new LedgerAccountRepository(factory),

            SubledgerRepository =
                new SubledgerRepository(factory),

            // STEP 6B - Connect Posting Service
            SubledgerJournalService =
                new SubledgerJournalService(
                    factory,
                    new TransactionRepository(factory))
        };

        view.LoadAccounts();

        ContentArea.Content = view;
    }
}