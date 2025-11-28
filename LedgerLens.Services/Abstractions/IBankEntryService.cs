using System;
using System.Threading.Tasks;

namespace LedgerLens.Services.Abstractions
{
    public interface IBankEntryService
    {
        /// <summary>
        /// Normal 2-line receipt (Bank Dr, Other Cr).
        /// </summary>
        Task<(int bankDrId, int otherCrId)> MakeReceipt(
            int bankId, string bankName,
            int fromId, string fromName,
            int yearId, DateTime tdate, decimal amount,
            string reference, string narration);
        // ✅ Bring Payment back (Other Dr, Bank Cr). Keep this name if your impl uses it.
        Task<(int debitId, int creditId)> MakePaymentAsync(
            int bankId, string bankName,
            int toId, string toName,
            int yearId, DateTime tdate, decimal amount,
            string reference, string narration,
            int? subledgerId = null);
        /// <summary>
        /// Receipt for FD closure/partial-closure with subledger:
        /// Dr Bank (principal+interest), Cr FD (principal) + SubledgerTrans, Cr Interest (interest).
        /// Returns (bankDrId, fdCrId, interestCrIdOrNull).
        /// </summary>
        Task<(int bankDrId, int fdCrId, int? interestCrId)> MakeReceiptWithSubledgerAsync(
            int bankId, string bankName,
            int fdAccountId, string fdName,
            int interestAccountId, string interestName,
            int fdSubledgerId,
            int yearId, DateTime tdate,
            decimal principalAmount, decimal interestAmount,
            string reference, string narration);
    }
}
