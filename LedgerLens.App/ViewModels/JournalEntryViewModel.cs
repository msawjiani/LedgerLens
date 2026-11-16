using LedgerLens.Data.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using LedgerLens.Data.Repositories;

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
        }
    }
    public decimal TotalDebit =>
    JournalLines.Sum(x => x.Debit ?? 0m);

    public decimal TotalCredit =>
        JournalLines.Sum(x => x.Credit ?? 0m);

    public decimal Difference =>
        TotalDebit - TotalCredit;

    public event PropertyChangedEventHandler? PropertyChanged;

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