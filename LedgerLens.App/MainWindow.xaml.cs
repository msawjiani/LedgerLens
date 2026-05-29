using LedgerLens.App.Session;
using LedgerLens.App.ViewModels;
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
using LedgerLens.Data;
using LedgerLens.Data.Repositories;
using Microsoft.Extensions.Options;

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

            MessageBox.Show($"Loaded: {SessionContext.PAN}");

        }
    }
}