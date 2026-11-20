using System.Data.OleDb;
using LedgerLens.Data.Models;


namespace LedgerLens.Data.Abstractions
{
    public interface ITransactionRepository
    {
        int InsertGeneralLedger(GeneralLedger gl, OleDbTransaction tx);
        List<BankEntryRow> GetBankEntries(int accountId, int yearId);
        long GetNextUnix(OleDbTransaction tx);

        // Subledger insert (link to a specific GL TransactionId)
        void InsertSubledgerTrans(SubledgerTrans sub, OleDbTransaction tx);

        // NEW:
        int InsertSharePurchase(SharePurchase sp, OleDbTransaction tx);
        // STEP 5C.3C - Share Sales Posting

        int InsertShareSale(
            SharesSales sale,
            OleDbTransaction tx);
        string? GetShareCompanyName(int shareId, OleDbTransaction tx); // optional helper for narration

        bool HasOtherFDTransactions(
                int subledgerId,
                int yearId,
                int unix);

        List<int> GetSubledgerIdsForUnix(int yearId, int unix);

        bool IsClosedByDifferentUnix(
                            int subledgerId,
                            int unix);
        int DeleteBankEntry(
                    int transactionId,
                    int bankAccountId,
                    int yearId,
                    int unix);

        long PostJournal(
                    int yearId,
                    DateTime date,
                    string reference,
                    string narration,
                    IReadOnlyList<(int AccountId, decimal Amount)> lines);
    }
}

