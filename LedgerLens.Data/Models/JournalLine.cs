using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LedgerLens.Data.Models
{
    public class JournalLine : INotifyPropertyChanged
    {
        private int? _accountId;
        private decimal? _debit;
        private decimal? _credit;

        public int? AccountId
        {
            get => _accountId;
            set
            {
                _accountId = value;
                OnPropertyChanged();
            }
        }

        public decimal? Debit
        {
            get => _debit;
            set
            {
                if (_debit == value)
                    return;

                _debit = value;
                OnPropertyChanged();

                // Entering Debit clears Credit
                if (value.HasValue && value.Value != 0)
                {
                    if (_credit.HasValue)
                    {
                        _credit = null;
                        OnPropertyChanged(nameof(Credit));
                    }
                }
            }
        }

        public decimal? Credit
        {
            get => _credit;
            set
            {
                if (_credit == value)
                    return;

                _credit = value;
                OnPropertyChanged();

                // Entering Credit clears Debit
                if (value.HasValue && value.Value != 0)
                {
                    if (_debit.HasValue)
                    {
                        _debit = null;
                        OnPropertyChanged(nameof(Debit));
                    }
                }
            }
        }

        public event PropertyChangedEventHandler?
            PropertyChanged;

        protected void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}