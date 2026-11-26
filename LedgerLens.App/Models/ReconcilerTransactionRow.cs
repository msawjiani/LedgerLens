using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace LedgerLens.App.Models
{
    public sealed class ReconcilerTransactionRow :
        INotifyPropertyChanged
    {
        public int TransactionId { get; set; }

        public int Unix { get; set; }

        public DateTime TDate { get; set; }

        public string Ref { get; set; } = "";

        public string Particulars { get; set; } = "";

        public decimal Amount { get; set; }

        public string Narration { get; set; } = "";

        // Automatic matching classification
        public int MatchType { get; set; } = 0;

        // Background colour used by the DISCO grid
        private Brush _rowColor = Brushes.White;

        public Brush RowColor
        {
            get => _rowColor;

            set
            {
                _rowColor = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler?
            PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}