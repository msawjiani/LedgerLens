using System.Data.OleDb;
using LedgerLens.Data.Models;

namespace LedgerLens.Data.Abstractions
{
    public interface ITransactionRepository
    {
        int InsertGeneralLedger(GeneralLedger gl, OleDbTransaction tx);
        long GetNextUnix(OleDbTransaction tx);

        // Subledger insert (link to a specific GL TransactionId)
        void InsertSubledgerTrans(SubledgerTrans sub, OleDbTransaction tx);

        // NEW:
        int InsertSharePurchase(SharePurchase sp, OleDbTransaction tx);
        string? GetShareCompanyName(int shareId, OleDbTransaction tx); // optional helper for narration
    }
}

