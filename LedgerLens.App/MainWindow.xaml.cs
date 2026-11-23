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

    // STEP 5E - First Actual Share Sales Posting Test

    // STEP 5H.1 - Fractional Share Sales Rounding Test

    // STEP 5H.2 - Multiple FIFO Lots Rounding Test

    // STEP 5H.4 - Deliberate Rounding Difference Test

    // STEP 5H.9 - Actual Fractional Share Sale Test

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

        int shareId = 2540;
        int brokerAccountId = 337;

        DateTime saleDate = new DateTime(2024, 11, 20);

        decimal quantity = 1.0000m;
        decimal sellingPrice = 1.00m;

        string reference = "MFROUNDTEST";
        string narration = "Fractional Mutual Fund Rounding Test";

        // Verify capital loss account mapping

        if (SessionContext.STCLAccountCode != 302)
        {
            MessageBox.Show(
                "STCL AccountId must be 302 for this test database.",
                "Account Mapping Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        // Confirm before posting

        var confirmation = MessageBox.Show(
            $"DATABASE:\n{SessionContext.DatabaseFileName}\n\n" +

            $"ShareId: {shareId}\n" +
            $"Sale Date: {saleDate:dd-MMM-yyyy}\n" +
            $"Quantity: {quantity}\n" +
            $"Selling Rate: {sellingPrice:N2}\n\n" +

            $"Expected Sale Proceeds: Rs.1.00\n" +
            $"Expected Purchase Cost: Rs.1.01\n" +
            $"Expected STCL: Rs.0.01\n\n" +

            "POST THIS FRACTIONAL SHARE SALE?",

            "LedgerLens - Fractional Sale Test",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
            return;

        try
        {
            long unix = service.PostShareSale(
                shareId,
                SessionContext.SelectedYearId,
                saleDate,
                quantity,
                sellingPrice,
                brokerAccountId,
                reference,
                narration,
                SessionContext.LTCGAccountCode,
                SessionContext.STCGAccountCode,
                SessionContext.LTCLAccountCode,
                SessionContext.STCLAccountCode);

            MessageBox.Show(
                "FRACTIONAL SHARE SALE POSTED!\n\n" +
                $"UNIX: {unix}\n" +
                $"Quantity: {quantity}\n" +
                $"Reference: {reference}\n\n" +
                "Please verify GeneralLedger, ShareSales and qryZERO.",
                "LedgerLens - Sale Successful",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Posting failed:\n\n{ex.Message}",
                "LedgerLens - Share Sale Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
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