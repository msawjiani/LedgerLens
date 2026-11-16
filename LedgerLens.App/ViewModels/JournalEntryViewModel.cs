using LedgerLens.App.Session;
using LedgerLens.Data.Models;
using LedgerLens.Data.Repositories;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Globalization;

public class JournalEntryViewModel : INotifyPropertyChanged
{
    public JournalEntryViewModel(
        LedgerAccountRepository ledgerAccountRepository)
    {
        var accounts = ledgerAccountRepository.GetAll();
        
        Accounts = new ObservableCollection<JournalAccountOption>(
            accounts
                .Where(a =>
                    !string.Equals(
                        a.Category?.Trim(),
                        "BANK",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    IsOrdinaryAccount(a.SubledgerFlag))
                .Select(a => new JournalAccountOption
                {
                    AccountId = a.AccountId,
                    Account = a.Account
                })
        );
        _currentBalances = ledgerAccountRepository.GetCurrentBalances();
        foreach (var line in JournalLines)
            line.PropertyChanged += JournalLine_PropertyChanged;

        JournalLines.CollectionChanged +=
            JournalLines_CollectionChanged;
    }

    private static bool IsOrdinaryAccount(string? flag)
    {
        string value = (flag ?? "").Trim().ToUpperInvariant();

        return value is "" or "N" or "NO" or "NONE" or "0";
    }
    // Temporary accounts for testing the ComboBox
    public ObservableCollection<JournalAccountOption> Accounts
    {
        get;
    }

    private string _dateInput = string.Empty;
    public string DateInput
    {
        get => _dateInput;
        set
        {
            _dateInput = value;
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(DateInput)));
            RecalculateTotals();
        }
    }
    private readonly Dictionary<int, decimal> _currentBalances;
    private string _refInput = string.Empty;
    public string RefInput
    {
        get => _refInput;
        set
        {
            _refInput = value;
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(RefInput)));
            RecalculateTotals();
        }
    }

    private string _narrationInput = string.Empty;
    public string NarrationInput
    {
        get => _narrationInput;
        set
        {
            _narrationInput = value;
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(NarrationInput)));
            RecalculateTotals();
        }
    }
    public decimal TotalDebit =>
    JournalLines.Sum(x => x.Debit ?? 0m);

    public decimal TotalCredit =>
        JournalLines.Sum(x => x.Credit ?? 0m);

    public decimal Difference =>
        TotalDebit - TotalCredit;

    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsSaveEnabled
    {
        get
        {
            // Only the latest accounting year permits posting.
            if (SessionContext.SelectedYearId != SessionContext.MaxYearId)
                return false;

            // Validate Date
            if (!DateTime.TryParseExact(
                    DateInput?.Trim(),
                    new[] { "d-MMM-yyyy", "dd-MMM-yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime date))
            {
                return false;
            }

            if (date.Date < SessionContext.SelectedStartDate.Date ||
                date.Date > SessionContext.SelectedEndDate.Date)
                return false;

            // Validate Reference and Narration
            if (string.IsNullOrWhiteSpace(RefInput) ||
                string.IsNullOrWhiteSpace(NarrationInput))
                return false;

            // No duplicate ledger accounts
            if (HasDuplicateAccounts)
                return false;

            // Ignore completely blank rows
            var entries = JournalLines
                .Where(x =>
                    x.AccountId.HasValue ||
                    x.Debit.HasValue ||
                    x.Credit.HasValue)
                .ToList();

            if (entries.Count < 2)
                return false;

            foreach (var line in entries)
            {
                if (!line.AccountId.HasValue)
                    return false;

                // Account must exist in the available list
                if (!Accounts.Any(a =>
                    a.AccountId == line.AccountId.Value))
                    return false;

                bool hasDebit = line.Debit is > 0m;
                bool hasCredit = line.Credit is > 0m;

                // Exactly one side must contain a positive amount
                if (hasDebit == hasCredit)
                    return false;

                if (line.Debit is < 0m ||
                    line.Credit is < 0m)
                    return false;
            }

            // Journal must balance
            return TotalDebit > 0m &&
                   TotalCredit > 0m &&
                   TotalDebit == TotalCredit;
        }
    }
    public string GetSaveDiagnostic()
    {
        if (SessionContext.SelectedYearId != SessionContext.MaxYearId)
            return $"Accounting Year mismatch.\n" +
                   $"Selected: {SessionContext.SelectedYearId}\n" +
                   $"Maximum: {SessionContext.MaxYearId}";

        if (!DateTime.TryParseExact(
                DateInput?.Trim(),
                "dd-MMM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date))
            return $"Invalid DateInput: '{DateInput}'";

        if (date.Date < SessionContext.SelectedStartDate.Date ||
            date.Date > SessionContext.SelectedEndDate.Date)
            return $"Date outside accounting year.\n" +
                   $"Date: {date:dd-MMM-yyyy}\n" +
                   $"From: {SessionContext.SelectedStartDate:dd-MMM-yyyy}\n" +
                   $"To: {SessionContext.SelectedEndDate:dd-MMM-yyyy}";

        if (string.IsNullOrWhiteSpace(RefInput))
            return "Reference is EMPTY.";

        if (string.IsNullOrWhiteSpace(NarrationInput))
            return "Narration is EMPTY.";

        if (HasDuplicateAccounts)
            return "Duplicate Ledger Accounts detected.";

        var entries = JournalLines
            .Where(x =>
                x.AccountId.HasValue ||
                x.Debit.HasValue ||
                x.Credit.HasValue)
            .ToList();

        if (entries.Count < 2)
            return $"Insufficient Journal Entries: {entries.Count}";

        foreach (var line in entries)
        {
            if (!line.AccountId.HasValue)
                return "A Journal row has no AccountId.";

            if (!Accounts.Any(a => a.AccountId == line.AccountId.Value))
                return $"AccountId {line.AccountId} is not in the account list.";

            bool hasDebit = line.Debit is > 0m;
            bool hasCredit = line.Credit is > 0m;

            if (hasDebit == hasCredit)
                return $"Invalid Debit/Credit combination for AccountId {line.AccountId}.";

            if (line.Debit is < 0m || line.Credit is < 0m)
                return $"Negative amount for AccountId {line.AccountId}.";
        }

        if (TotalDebit <= 0m ||
            TotalCredit <= 0m ||
            TotalDebit != TotalCredit)
        {
            return $"Journal not balanced.\n" +
                   $"Debit: {TotalDebit:N2}\n" +
                   $"Credit: {TotalCredit:N2}";
        }

        return "ALL VALIDATIONS PASSED!";
    }
    private void RecalculateTotals()
    {
        PropertyChanged?.Invoke(this,
            new PropertyChangedEventArgs(nameof(TotalDebit)));

        PropertyChanged?.Invoke(this,
            new PropertyChangedEventArgs(nameof(TotalCredit)));

        PropertyChanged?.Invoke(this,
            new PropertyChangedEventArgs(nameof(Difference)));

        PropertyChanged?.Invoke(this,
            new PropertyChangedEventArgs(nameof(DebitDifference)));

        PropertyChanged?.Invoke(this,
            new PropertyChangedEventArgs(nameof(CreditDifference)));
        PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(
                    nameof(HasDuplicateAccounts)));
        PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(IsSaveEnabled)));
    }

    private void JournalLine_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (sender is JournalLine line &&
            e.PropertyName == nameof(JournalLine.AccountId))
        {
            // Retrieve the current ledger balance
            // when an account is selected.
            if (line.AccountId.HasValue &&
                _currentBalances.TryGetValue(
                    line.AccountId.Value,
                    out decimal balance))
            {
                line.CurrentBalance = balance;
            }
            else
            {
                // Account has no existing transactions,
                // or no account is selected.
                line.CurrentBalance = 0m;
            }

            // Preserve our automatic row creation.
            EnsureTrailingBlankRow();
        }

        RecalculateTotals();
    }

    private void JournalLines_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (JournalLine line in e.OldItems)
                line.PropertyChanged -= JournalLine_PropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (JournalLine line in e.NewItems)
                line.PropertyChanged += JournalLine_PropertyChanged;
        }

        RecalculateTotals();
    }
    public void EnsureTrailingBlankRow()
    {
        if (JournalLines.Count == 0 ||
            JournalLines.Last().AccountId.HasValue)
        {
            JournalLines.Add(new JournalLine());
        }
    }
    // Our editable journal rows
    public bool HasDuplicateAccounts =>
                JournalLines
                    .Where(x => x.AccountId.HasValue)
                    .GroupBy(x => x.AccountId!.Value)
                    .Any(g => g.Count() > 1);
    private static T? FindVisualChild<T>(
    DependencyObject parent)
    where T : DependencyObject
    {
        for (int i = 0;
             i < VisualTreeHelper.GetChildrenCount(parent);
             i++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(parent, i);

            if (child is T match)
                return match;

            T? result = FindVisualChild<T>(child);

            if (result != null)
                return result;
        }

        return null;
    }
    public ObservableCollection<JournalLine> JournalLines { get; }
        = new()
        {
            new JournalLine(),
            new JournalLine(),
            new JournalLine()
        };
    public decimal? DebitDifference =>
    TotalCredit > TotalDebit
        ? TotalCredit - TotalDebit
        : null;

    public decimal? CreditDifference =>
        TotalDebit > TotalCredit
            ? TotalDebit - TotalCredit
            : null;
}

public class JournalAccountOption
{
    public int AccountId { get; set; }
    public string Account { get; set; } = string.Empty;
}