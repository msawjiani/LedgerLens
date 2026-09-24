using LedgerLens.Data.Models;
using LedgerLens.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LedgerLens.App.ViewModels
{
    public class SubledgerViewModel : INotifyPropertyChanged
    {
        private readonly SubledgerRepository _subledgerRepository;
        private readonly LedgerAccountRepository _ledgerAccountRepository;

        private List<Subledger> _allSubledgers = new();

        public ObservableCollection<LedgerAccount> SubledgerAccounts { get; }
            = new();

        public ObservableCollection<Subledger> Subledgers { get; }
            = new();

        private LedgerAccount? _selectedLedgerAccount;

        public LedgerAccount? SelectedLedgerAccount
        {
            get => _selectedLedgerAccount;
            set
            {
                _selectedLedgerAccount = value;
                OnPropertyChanged();

                LoadSubledgers();
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
        private Subledger? _selectedSubledger;

        public Subledger? SelectedSubledger
        {
            get => _selectedSubledger;
            set
            {
                _selectedSubledger = value;
                OnPropertyChanged();

                EditingSubledger = value == null
                    ? null
                    : new Subledger
                    {
                        SubledgerId = value.SubledgerId,
                        AccountId = value.AccountId,
                        Subaccount = value.Subaccount
                    };
            }
        }

        private Subledger? _editingSubledger;

        public Subledger? EditingSubledger
        {
            get => _editingSubledger;
            set
            {
                _editingSubledger = value;
                OnPropertyChanged();
            }
        }
        public SubledgerViewModel(
            SubledgerRepository subledgerRepository,
            LedgerAccountRepository ledgerAccountRepository)
        {
            _subledgerRepository = subledgerRepository;
            _ledgerAccountRepository = ledgerAccountRepository;

            LoadSubledgerAccounts();
        }

        private void LoadSubledgerAccounts()
        {
            SubledgerAccounts.Clear();

            var accounts = _ledgerAccountRepository
                .GetAll()
                .Where(x => x.SubledgerFlag == "SL")
                .OrderBy(x => x.Account);

            foreach (var account in accounts)
            {
                SubledgerAccounts.Add(account);
            }

            SelectedLedgerAccount = SubledgerAccounts.FirstOrDefault();
        }

        private void LoadSubledgers()
        {
            Subledgers.Clear();
            _allSubledgers.Clear();

            if (SelectedLedgerAccount == null)
                return;

            _allSubledgers = _subledgerRepository
                .GetByAccountId(SelectedLedgerAccount.AccountId);

            SearchText = "";

            ApplySearch();
        }

        private void ApplySearch()
        {
            Subledgers.Clear();

            var search = SearchText.Trim();

            var result = string.IsNullOrWhiteSpace(search)
                ? _allSubledgers
                : _allSubledgers.Where(x =>
                    x.SubledgerId.ToString().Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase) ||

                    (x.Subaccount ?? "").Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var subledger in result)
            {
                Subledgers.Add(subledger);
                
            }
            SelectedSubledger = Subledgers.FirstOrDefault();
        }

        public void NewSubledger()
        {
            if (SelectedLedgerAccount == null)
                return;

            SelectedSubledger = null;

            EditingSubledger = new Subledger
            {
                SubledgerId = 0,
                AccountId = SelectedLedgerAccount.AccountId,
                Subaccount = ""
            };
        }

        public void SaveSubledger()
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

            if (EditingSubledger == null)
            {
                MessageBox.Show(
                    "Please select a subaccount or click New.",
                    "LedgerLens",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (string.IsNullOrWhiteSpace(EditingSubledger.Subaccount))
            {
                MessageBox.Show(
                    "Subaccount description is required.",
                    "LedgerLens",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string subaccountName = EditingSubledger.Subaccount.Trim();

            bool duplicateExists = _allSubledgers.Any(x =>
                x.SubledgerId != EditingSubledger.SubledgerId &&
                string.Equals(
                    (x.Subaccount ?? "").Trim(),
                    subaccountName,
                    StringComparison.OrdinalIgnoreCase));

            if (duplicateExists)
            {
                MessageBox.Show(
                    "Another subaccount already has this name for the selected Ledger Account.",
                    "LedgerLens",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            EditingSubledger.Subaccount = subaccountName;
            EditingSubledger.AccountId = SelectedLedgerAccount.AccountId;

            if (EditingSubledger.SubledgerId == 0)
            {
                _subledgerRepository.Add(EditingSubledger);
            }
            else
            {
                _subledgerRepository.Update(EditingSubledger);
            }

            _allSubledgers = _subledgerRepository
                .GetByAccountId(SelectedLedgerAccount.AccountId);

            ApplySearch();

            SelectedSubledger = Subledgers.FirstOrDefault(x =>
                string.Equals(
                    x.Subaccount,
                    subaccountName,
                    StringComparison.OrdinalIgnoreCase));
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