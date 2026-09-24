using LedgerLens.Data.Models;
using LedgerLens.Data.Repositories;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace LedgerLens.App.ViewModels
{
    public class CompaniesViewModel : INotifyPropertyChanged
    {
        private readonly CompanyRepository _companyRepository;
        private readonly LedgerAccountRepository _ledgerAccountRepository;
        private List<Company> _allCompanies = new();
        private string _searchText = "";

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplySearch();
            }
        }



        public ObservableCollection<LedgerAccount> ShareAccounts { get; }
            = new();

        public ObservableCollection<Company> Companies { get; }
            = new();

        private LedgerAccount? _selectedLedgerAccount;

        public LedgerAccount? SelectedLedgerAccount
        {
            get => _selectedLedgerAccount;
            set
            {
                _selectedLedgerAccount = value;
                OnPropertyChanged();

                LoadCompanies();
            }
        }
        private Company? _selectedCompany;

        public Company? SelectedCompany
        {
            get => _selectedCompany;
            set
            {
                _selectedCompany = value;
                OnPropertyChanged();

                EditingCompany = value == null
                    ? null
                    : new Company
                    {
                        ShareId = value.ShareId,
                        CompanyName = value.CompanyName,
                        AccountId = value.AccountId
                    };
            }
        }

        private Company? _editingCompany;

        public Company? EditingCompany
        {
            get => _editingCompany;
            set
            {
                _editingCompany = value;
                OnPropertyChanged();
            }
        }
        public CompaniesViewModel(
            CompanyRepository companyRepository,
            LedgerAccountRepository ledgerAccountRepository)
        {
            _companyRepository = companyRepository;
            _ledgerAccountRepository = ledgerAccountRepository;

            LoadShareAccounts();
        }

        public void NewCompany()
        {
            if (SelectedLedgerAccount == null)
                return;

            SelectedCompany = null;

            EditingCompany = new Company
            {
                ShareId = 0,
                CompanyName = "",
                AccountId = SelectedLedgerAccount.AccountId
            };
        }

        public void SaveCompany()
        {
            if (SelectedLedgerAccount == null)
            {
                MessageBox.Show(
                    "Please select a Ledger Account.",
                    "LedgerLens",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (EditingCompany == null)
            {
                MessageBox.Show(
                    "Please select a company or click New.",
                    "LedgerLens",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (string.IsNullOrWhiteSpace(EditingCompany.CompanyName))
            {
                MessageBox.Show(
                    "Company name is required.",
                    "LedgerLens",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string companyName = EditingCompany.CompanyName.Trim();

            bool duplicateExists = _allCompanies.Any(x =>
                x.ShareId != EditingCompany.ShareId &&
                string.Equals(
                    x.CompanyName.Trim(),
                    companyName,
                    StringComparison.OrdinalIgnoreCase));

            if (duplicateExists)
            {
                MessageBox.Show(
                    "Another company already has this name for the selected Ledger Account.",
                    "LedgerLens",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            EditingCompany.CompanyName = companyName;
            EditingCompany.AccountId = SelectedLedgerAccount.AccountId;

            int shareId = EditingCompany.ShareId;

            if (shareId == 0)
            {
                _companyRepository.Add(EditingCompany);
            }
            else
            {
                _companyRepository.Update(EditingCompany);
            }

            // Reload from database
            _allCompanies = _companyRepository
                .GetByAccountId(SelectedLedgerAccount.AccountId);

            ApplySearch();

            // Reselect the company that was just saved
            SelectedCompany = Companies.FirstOrDefault(x =>
                string.Equals(
                    x.CompanyName,
                    companyName,
                    StringComparison.OrdinalIgnoreCase));
        }

        private void LoadShareAccounts()
        {
            ShareAccounts.Clear();

            var accounts = _ledgerAccountRepository
                .GetAll()
                .Where(x => x.SubledgerFlag == "SH")
                .OrderBy(x => x.Account);

            foreach (var account in accounts)
                ShareAccounts.Add(account);

            SelectedLedgerAccount = ShareAccounts.FirstOrDefault();
        }

        private void LoadCompanies()
        {
            Companies.Clear();
            _allCompanies.Clear();

            if (SelectedLedgerAccount == null)
                return;

            _allCompanies = _companyRepository
                .GetByAccountId(SelectedLedgerAccount.AccountId);

            SearchText = "";

            ApplySearch();
        }
        private void ApplySearch()
        {
            Companies.Clear();

            var search = SearchText.Trim();

            var result = string.IsNullOrWhiteSpace(search)
                ? _allCompanies
                : _allCompanies.Where(x =>
                    x.ShareId.ToString().Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase) ||

                    (x.CompanyName ?? "").Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var company in result)
            {
                Companies.Add(company);
            }
            SelectedCompany = Companies.FirstOrDefault();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(name));
        }


    }
}