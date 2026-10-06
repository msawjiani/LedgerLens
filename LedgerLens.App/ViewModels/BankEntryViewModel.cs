using LedgerLens.App.Session;
using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;
using LedgerLens.Data.Repositories;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LedgerLens.App.ViewModels
{
    public sealed class BankEntryViewModel : INotifyPropertyChanged
    {
        private readonly LedgerAccountRepository _ledgerAccountRepository;
        private readonly ITransactionRepository _transactionRepository;

        public List<LedgerAccount> BankAccounts { get; }
        public List<LedgerAccount> GLAccounts { get; }

        private LedgerAccount? _selectedBank;
        public LedgerAccount? SelectedBank
        {
            get => _selectedBank;

            set
            {
                _selectedBank = value;
                OnPropertyChanged();

                LoadSelectedBank();
            }
        }

        private List<BankEntryRow> _bankEntries = new();
        public List<BankEntryRow> BankEntries
        {
            get => _bankEntries;

            private set
            {
                _bankEntries = value;
                OnPropertyChanged();
            }
        }

        private decimal _currentBalance;
        public decimal CurrentBalance
        {
            get => _currentBalance;

            private set
            {
                _currentBalance = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _entryDate = DateTime.Today;
        public DateTime? EntryDate
        {
            get => _entryDate;
            set
            {
                _entryDate = value;
                OnPropertyChanged();
            }
        }

        private string _reference = string.Empty;
        public string Reference
        {
            get => _reference;
            set
            {
                _reference = value;
                OnPropertyChanged();
            }
        }

        private LedgerAccount? _selectedGLAccount;
        public LedgerAccount? SelectedGLAccount
        {
            get => _selectedGLAccount;
            set
            {
                _selectedGLAccount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowSubledgerButton));
            }
        }

        private string _amount = string.Empty;
        public string Amount
        {
            get => _amount;
            set
            {
                _amount = value;
                OnPropertyChanged();
            }
        }

        private string _narration = "Being ";
        public string Narration
        {
            get => _narration;
            set
            {
                _narration = value;
                OnPropertyChanged();
            }
        }

        private bool _isReceipt = true;
        public bool IsReceipt
        {
            get => _isReceipt;
            set
            {
                _isReceipt = value;
                OnPropertyChanged();

                if (value)
                {
                    IsPayment = false;
                }
            }
        }

        private bool _isPayment;
        public bool IsPayment
        {
            get => _isPayment;
            set
            {
                _isPayment = value;
                OnPropertyChanged();

                if (value)
                {
                    IsReceipt = false;
                }
            }
        }

        public bool ShowSubledgerButton =>
            SelectedGLAccount?.SubledgerFlag == "SL";

        public BankEntryViewModel(
            LedgerAccountRepository ledgerAccountRepository,
            ITransactionRepository transactionRepository)
        {
            _ledgerAccountRepository = ledgerAccountRepository;
            _transactionRepository = transactionRepository;

            BankAccounts = _ledgerAccountRepository
                .GetAll()
                .Where(a => a.Category == "BANK")
                .OrderBy(a => a.Account)
                .ToList();
            GLAccounts = _ledgerAccountRepository
                .GetAll()
                .OrderBy(a => a.Account)
                .ToList();
        }

        private void LoadSelectedBank()
        {
            if (SelectedBank == null)
            {
                BankEntries = new List<BankEntryRow>();
                CurrentBalance = 0M;
                return;
            }

            var entries = _transactionRepository.GetBankEntries(
                SelectedBank.AccountId,
                SessionContext.SelectedYearId);

            decimal runningBalance = 0M;

            foreach (var row in entries)
            {
                runningBalance += row.Amount;
                row.RunningBalance = runningBalance;
            }

            BankEntries = entries;
            CurrentBalance = runningBalance;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}