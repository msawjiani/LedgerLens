using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace LedgerLens.App.Models
{
    public static class ReconcilerMatcher
    {
        public static void Match(
            List<ReconcilerTransactionRow> source,
            List<ReconcilerTransactionRow> target)
        {
            // Reset previous automatic matches.

            foreach (var row in source.Concat(target))
            {
                row.MatchType = 0;
                row.RowColor = Brushes.White;
            }

            var matchedSource = new HashSet<int>();
            var matchedTarget = new HashSet<int>();

            // PASS 1: Exact date and opposite amount.

            for (int i = 0; i < source.Count; i++)
            {
                for (int j = 0; j < target.Count; j++)
                {
                    if (matchedTarget.Contains(j))
                        continue;

                    if (source[i].Amount + target[j].Amount == 0m &&
                        source[i].TDate.Date == target[j].TDate.Date)
                    {
                        source[i].MatchType = 1;
                        target[j].MatchType = 1;

                        source[i].RowColor =
                            new SolidColorBrush(
                                Color.FromRgb(200, 245, 200));

                        target[j].RowColor =
                            new SolidColorBrush(
                                Color.FromRgb(200, 245, 200));

                        matchedSource.Add(i);
                        matchedTarget.Add(j);

                        break;
                    }
                }
            }

            // PASS 2: Opposite amount within +/- 2 days.

            for (int i = 0; i < source.Count; i++)
            {
                if (matchedSource.Contains(i))
                    continue;

                int bestTarget = -1;
                int bestDayDifference = 3;

                for (int j = 0; j < target.Count; j++)
                {
                    if (matchedTarget.Contains(j))
                        continue;

                    if (source[i].Amount + target[j].Amount != 0m)
                        continue;

                    int days = Math.Abs(
                        (source[i].TDate.Date -
                         target[j].TDate.Date).Days);

                    if (days >= 1 &&
                        days <= 2 &&
                        days < bestDayDifference)
                    {
                        bestTarget = j;
                        bestDayDifference = days;
                    }
                }

                if (bestTarget == -1)
                    continue;

                source[i].MatchType = 2;
                target[bestTarget].MatchType = 2;

                source[i].RowColor =
                    new SolidColorBrush(
                        Color.FromRgb(205, 245, 235));

                target[bestTarget].RowColor =
                    new SolidColorBrush(
                        Color.FromRgb(205, 245, 235));

                matchedSource.Add(i);
                matchedTarget.Add(bestTarget);
            }
        }
    }
}