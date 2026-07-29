using LedgerLens.Data.Models;
using LedgerLens.Data.Repositories;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LedgerLens.App.Session;
using System.Windows;

namespace LedgerLens.App.ViewModels
{
    public class LedgerAccountsViewModel : INotifyPropertyChanged
    {
        private readonly LedgerAccountRepository _repository;

        private List<LedgerAccount> _allAccounts = new();

        public ObservableCollection<LedgerAccount> FilteredAccounts { get; } = new();

        private LedgerAccount? _selectedAccount;
        public LedgerAccount? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                _selectedAccount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRetainedEarningsSelected));

                EditingAccount = value == null
                    ? null
                    : new LedgerAccount
                    {
                        AccountId = value.AccountId,
                        Account = value.Account,
                        Category = value.Category,
                        SubledgerFlag = value.SubledgerFlag,
                        DashboardGroup = value.DashboardGroup
                    };
            }
        }
        private LedgerAccount? _editingAccount;

        public LedgerAccount? EditingAccount
        {
            get => _editingAccount;
            set
            {
                _editingAccount = value;
                OnPropertyChanged();
            }
        }
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

        public List<string> CategoryOptions { get; } = new()
            {
                "BS",
                "BANK",
                "PL",
                "RE"
            };
        public List<string> SubledgerFlagOptions { get; } = new()
        {
            "NO",
            "SL",
            "SH"
        };

        public List<string> DashboardGroupOptions { get; } = new()
        {
            "Bank",
            "Capital",
            "Capital Gains",
            "Cash",
            "Dividend",
            "Drawings",
            "Family",
            "FD",
            "Interest",
            "Mutual Funds",
            "Other",
            "Pension",
            "Property",
            "Retained Earnings",
            "Shares",
            "Tax"
        };
        public bool IsRetainedEarningsSelected =>
            SelectedAccount?.Category == "RE";
        public LedgerAccountsViewModel(LedgerAccountRepository repository)
        {
            _repository = repository;
            Load();
        }

        private void Load()
        {
            _allAccounts = _repository.GetAll();
            ApplySearch();
        }

        private void ApplySearch()
        {
            FilteredAccounts.Clear();

            var search = SearchText.Trim();

            var result = string.IsNullOrWhiteSpace(search)
                ? _allAccounts
                : _allAccounts.Where(x =>
                    x.AccountId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (x.Account ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (x.Category ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (x.SubledgerFlag ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (x.DashboardGroup ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                ).ToList();

            foreach (var account in result)
                FilteredAccounts.Add(account);

            SelectedAccount = FilteredAccounts.FirstOrDefault();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private string? ValidateLedgerAccount()
        {
            if (EditingAccount == null)
                return "Please select an account or click New.";

            if (string.IsNullOrWhiteSpace(EditingAccount.Account))
                return "Account name is required.";

            if (string.IsNullOrWhiteSpace(EditingAccount.Category))
                return "Please select a category.";

            if (string.IsNullOrWhiteSpace(EditingAccount.SubledgerFlag))
                return "Please select a Subledger Flag.";

            if (string.IsNullOrWhiteSpace(EditingAccount.DashboardGroup))
                return "Please select a Dashboard Group.";

            if (EditingAccount.Category == "PL" &&
                EditingAccount.SubledgerFlag != "NO")
            {
                return "Profit and Loss accounts must have Subledger Flag set to NO.";
            }

            if (EditingAccount.Category == "BANK" &&
                EditingAccount.SubledgerFlag != "NO")
            {
                return "Bank accounts must have Subledger Flag set to NO.";
            }

            if (EditingAccount.Category == "RE" &&
                EditingAccount.SubledgerFlag != "NO")
            {
                return "Retained Earnings must have Subledger Flag set to NO.";
            }

            return null;
        }
        public void NewAccount()
        {
            SelectedAccount = null;

            EditingAccount = new LedgerAccount
            {
                AccountId = 0,
                Account = "",
                Category = "BS",
                SubledgerFlag = "NO",
                DashboardGroup = "Other"
            };
        }
        public void SaveSelectedAccount()
        {
            if (EditingAccount == null)
            {
                MessageBox.Show(
                    "Please select an account.",
                    "LedgerLens",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var validationMessage = ValidateLedgerAccount();

            if (validationMessage != null)
            {
                MessageBox.Show(
                    validationMessage,
                    "Invalid Ledger Account",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (EditingAccount.AccountId == SessionContext.RetainedEarningsId)
            {
                MessageBox.Show(
                    "Retained Earnings is a system account and cannot be modified.",
                    "LedgerLens",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (string.IsNullOrWhiteSpace(EditingAccount.Account))
            {
                MessageBox.Show(
                    "Account name cannot be blank.",
                    "LedgerLens",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }
            bool duplicateExists = _allAccounts.Any(x =>
                            x.AccountId != EditingAccount.AccountId &&
                            string.Equals(
                                x.Account.Trim(),
                                EditingAccount.Account.Trim(),
                                StringComparison.OrdinalIgnoreCase));

                                    if (duplicateExists)
                                    {
                                        MessageBox.Show(
                                            "Another ledger account already has this name.",
                                            "LedgerLens",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Warning);

                                        return;
                                    }
            string savedAccountName = EditingAccount.Account.Trim();
            int accountId = EditingAccount.AccountId;
            
            

            if (EditingAccount.AccountId == 0)
            {
                if (EditingAccount.Category == "RE")
                {
                    MessageBox.Show(
                        "A new Retained Earnings account cannot be created.",
                        "LedgerLens",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                _repository.Add(EditingAccount);
            }
            else
            {
                if (EditingAccount.AccountId == SessionContext.RetainedEarningsId)
                {
                    MessageBox.Show(
                        "Retained Earnings is a system account and cannot be modified.",
                        "LedgerLens",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                _repository.Update(EditingAccount);
            }

            _allAccounts = _repository.GetAll();
            ApplySearch();


            if (accountId > 0)
            {
                SelectedAccount = FilteredAccounts
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.Account,
                            savedAccountName,
                            StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                SelectedAccount = FilteredAccounts
                    .FirstOrDefault(x =>
                        string.Equals(
                        x.Account,
                        savedAccountName,
                            StringComparison.OrdinalIgnoreCase));
            }
        }


    }
}