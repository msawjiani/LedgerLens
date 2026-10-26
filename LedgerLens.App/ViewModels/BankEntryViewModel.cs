using LedgerLens.App.Session;
using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;
using LedgerLens.Data.Repositories;
using LedgerLens.Services.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Data;



namespace LedgerLens.App.ViewModels
{
    public sealed class BankEntryViewModel : INotifyPropertyChanged
    {
        private readonly LedgerAccountRepository _ledgerAccountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBankEntryService _bankEntryService;

        public List<LedgerAccount> BankAccounts { get; }
        public List<LedgerAccount> GLAccounts { get; }
        public ICollectionView GLAccountsView { get; }

        private string _glSearchText = string.Empty;

        public void FilterGLAccounts(string searchText)
        {
            _glSearchText = searchText ?? string.Empty;
            GLAccountsView.Refresh();
        }

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

        private string _entryDate = DateTime.Today.ToString("dd-MMM-yyyy");

        public string EntryDate
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

        private bool _isReceipt ;
        public bool IsReceipt
        {
            get => _isReceipt;
            set
            {
                _isReceipt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowSubledgerButton));

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
                OnPropertyChanged(nameof(ShowSubledgerButton));

  
            }
        }

        public bool ShowSubledgerButton =>
            SelectedGLAccount?.SubledgerFlag == "SL"
            && (IsReceipt || IsPayment);

        public BankEntryViewModel(
            LedgerAccountRepository ledgerAccountRepository,
            ITransactionRepository transactionRepository,
            IBankEntryService bankEntryService)
        {
            _ledgerAccountRepository = ledgerAccountRepository;
            _transactionRepository = transactionRepository;
            _bankEntryService = bankEntryService;

            BankAccounts = _ledgerAccountRepository
                .GetAll()
                .Where(a => a.Category == "BANK")
                .OrderBy(a => a.Account)
                .ToList();

            GLAccounts = _ledgerAccountRepository
                .GetAll()
                .OrderBy(a => a.Account)
                .ToList();
            GLAccountsView = CollectionViewSource.GetDefaultView(GLAccounts);

            GLAccountsView.Filter = item =>
            {
                if (item is not LedgerAccount account)
                    return false;

                return string.IsNullOrWhiteSpace(_glSearchText)
                    || account.Account.Contains(
                        _glSearchText,
                        StringComparison.OrdinalIgnoreCase);
            };
        }
        public bool ValidateEntryDate(out string errorMessage)
        {
            errorMessage = string.Empty;

            // 1. Check for blank date
            if (string.IsNullOrWhiteSpace(EntryDate))
            {
                errorMessage = "Please enter a transaction date.";
                return false;
            }

            // 2. Accept single-digit or double-digit days
            string[] acceptedFormats =
            {
                "d-MMM-yyyy",
                "dd-MMM-yyyy"
            };

            // 3. Check whether the date actually exists
            bool isValidDate = DateTime.TryParseExact(
                EntryDate.Trim(),
                acceptedFormats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTime date);

            if (!isValidDate)
            {
                errorMessage =
                    "Invalid transaction date. Please enter a valid date " +
                    "in dd-MMM-yyyy format.";

                return false;
            }

            // 4. Only a valid date reaches this point
            if (date.Date < SessionContext.SelectedStartDate.Date ||
                date.Date > SessionContext.SelectedEndDate.Date)
            {
                errorMessage =
                    $"Transaction date must be between " +
                    $"{SessionContext.SelectedStartDate:dd-MMM-yyyy} and " +
                    $"{SessionContext.SelectedEndDate:dd-MMM-yyyy}.";

                return false;
            }

            // 5. All checks passed
            return true;
        }
        public void IncrementDate()
        {
            if (!ValidateEntryDate(out string errorMessage))
            {
                MessageBox.Show(
                    errorMessage,
                    "Invalid Date",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string[] formats = { "d-MMM-yyyy", "dd-MMM-yyyy" };

            DateTime currentDate = DateTime.ParseExact(
                EntryDate.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);

            DateTime nextDate = currentDate.AddDays(1);

            if (nextDate.Date > SessionContext.SelectedEndDate.Date)
            {
                MessageBox.Show(
                    "Cannot increment beyond the accounting year.",
                    "Date Increment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            EntryDate = nextDate.ToString(
                "dd-MMM-yyyy",
                CultureInfo.InvariantCulture);
        }
        public void IncrementReference()
        {
            string reference = Reference?.Trim() ?? "";

            if (string.IsNullOrEmpty(reference))
            {
                MessageBox.Show(
                    "Please enter a Reference first.",
                    "Reference Increment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            int position = reference.Length;

            // Find the numeric suffix
            while (position > 0 && char.IsDigit(reference[position - 1]))
            {
                position--;
            }

            if (position == reference.Length)
            {
                MessageBox.Show(
                    "Reference must end with a number.",
                    "Reference Increment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string prefix = reference[..position];
            string numberPart = reference[position..];

            if (!long.TryParse(numberPart, out long number) ||
                number == long.MaxValue)
            {
                MessageBox.Show(
                    "Reference number cannot be incremented.",
                    "Reference Increment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string nextNumber = (number + 1)
                .ToString()
                .PadLeft(numberPart.Length, '0');

            string nextReference = prefix + nextNumber;

            if (nextReference.Length > 20)
            {
                MessageBox.Show(
                    "Reference cannot exceed 20 characters.",
                    "Reference Increment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Reference = nextReference;
        }
        public bool ValidateReference(out string errorMessage)
        {
            errorMessage = string.Empty;

            // Reference cannot be blank
            if (string.IsNullOrWhiteSpace(Reference))
            {
                errorMessage = "Please enter a Reference.";
                return false;
            }

            // Database field allows a maximum of 20 characters
            if (Reference.Length > 20)
            {
                errorMessage =
                    "Reference cannot exceed 20 characters.";

                return false;
            }

            return true;
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
        public async Task<(bool Success, string Message)> SaveReceiptAsync()
        {
            // 1. Date validation
            if (!ValidateEntryDate(out string dateError))
                return (false, dateError);

            // 2. Reference validation
            if (!ValidateReference(out string referenceError))
                return (false, referenceError);

            // 3. Bank / Cash selection
            if (SelectedBank == null)
                return (false, "Please select a Bank / Cash Account.");

            // 4. GL Account selection
            if (SelectedGLAccount == null)
                return (false, "Please select a Ledger Account.");

            // 5. Prevent posting to the same account
            if (SelectedBank.AccountId == SelectedGLAccount.AccountId)
                return (false, "Bank and Ledger Account cannot be the same.");

            // 6. Subledger and share transactions will be implemented separately
            if (SelectedGLAccount.SubledgerFlag == "SL" ||
                SelectedGLAccount.SubledgerFlag == "SH")
            {
                return (false,
                    "Subledger and Share transactions are not yet supported here.");
            }

            // 7. Receipt must be selected
            if (!IsReceipt || IsPayment)
                return (false, "Please select Receipt.");

            // 8. Amount validation
            if (!decimal.TryParse(
                    Amount,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out decimal amount) || amount <= 0)
            {
                return (false, "Please enter a valid positive Amount.");
            }

            // 9. Narration validation
            if (string.IsNullOrWhiteSpace(Narration) ||
                Narration.Trim().Equals("Being", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Please enter a Narration.");
            }

            // 10. Convert the already validated date
            string[] formats = { "d-MMM-yyyy", "dd-MMM-yyyy" };

            DateTime transactionDate = DateTime.ParseExact(
                EntryDate.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);

            // 11. Post the Receipt
            try
            {
                await _bankEntryService.MakeReceipt(
                    SelectedBank.AccountId,
                    SelectedBank.Account,
                    SelectedGLAccount.AccountId,
                    SelectedGLAccount.Account,
                    SessionContext.SelectedYearId,
                    transactionDate,
                    amount,
                    Reference.Trim(),
                    Narration.Trim());

                // 12. Refresh grid and balance
                LoadSelectedBank();

                // 13. Clear amount, but retain date, reference and bank
                Amount = string.Empty;
                Narration = "Being ";

                return (true, "Receipt saved successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Unable to save Receipt: {ex.Message}");
            }
        }
        public async Task<(bool Success, string Message)> SavePaymentAsync()
        {
            if (!ValidateEntryDate(out string dateError))
                return (false, dateError);

            if (!ValidateReference(out string referenceError))
                return (false, referenceError);

            if (SelectedBank == null)
                return (false, "Please select a Bank / Cash Account.");

            if (SelectedGLAccount == null)
                return (false, "Please select a Ledger Account.");

            if (SelectedBank.AccountId == SelectedGLAccount.AccountId)
                return (false, "Bank and Ledger Account cannot be the same.");

            // Subledger and Share transactions will use separate workflows.
            if (SelectedGLAccount.SubledgerFlag == "SL" ||
                SelectedGLAccount.SubledgerFlag == "SH")
            {
                return (false,
                    "Subledger and Share transactions are not yet supported here.");
            }

            if (!IsPayment || IsReceipt)
                return (false, "Please select Payment.");

            if (!decimal.TryParse(
                    Amount,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out decimal amount) || amount <= 0)
            {
                return (false, "Please enter a valid positive Amount.");
            }

            if (string.IsNullOrWhiteSpace(Narration) ||
                Narration.Trim().Equals(
                    "Being",
                    StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Please enter a Narration.");
            }

            string[] formats = { "d-MMM-yyyy", "dd-MMM-yyyy" };

            DateTime transactionDate = DateTime.ParseExact(
                EntryDate.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);

            try
            {
                await _bankEntryService.MakePaymentAsync(
                    SelectedBank.AccountId,
                    SelectedBank.Account,
                    SelectedGLAccount.AccountId,
                    SelectedGLAccount.Account,
                    SessionContext.SelectedYearId,
                    transactionDate,
                    amount,
                    Reference.Trim(),
                    Narration.Trim());

                LoadSelectedBank();

                Amount = string.Empty;
                Narration = "Being ";

                return (true, "Payment saved successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Unable to save Payment: {ex.Message}");
            }
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