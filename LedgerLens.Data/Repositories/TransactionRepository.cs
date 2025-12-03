using System;
using System.Data.OleDb;
using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;

namespace LedgerLens.Data.Repositories
{
    public sealed class TransactionRepository : ITransactionRepository
    {
        public long GetNextUnix(OleDbTransaction tx)
        {
            using var cmd = tx.Connection!.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT MAX([Unix]) FROM GeneralLedger";
            var o = cmd.ExecuteScalar();
            var max = (o == DBNull.Value) ? 0 : Convert.ToInt64(o);
            return max + 1;
        }

        public int InsertGeneralLedger(GeneralLedger gl, OleDbTransaction tx)
        {
            using var cmd = tx.Connection!.CreateCommand();
            cmd.Transaction = tx;

            cmd.CommandText = @"
                INSERT INTO GeneralLedger
                 (AccountId, YearId, Unix, TDate, Ref, Particulars, Amount, TransactionType, Narration)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)";

            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = gl.AccountId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = gl.YearId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = Convert.ToInt32(gl.Unix) });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = gl.Tdate });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = gl.Ref ?? "" });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = gl.Particulars ?? "" });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Double, Value = (double)gl.Amount });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = gl.TransactionType ?? "" });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.LongVarWChar, Value = gl.Narration ?? "" });

            cmd.ExecuteNonQuery();

            cmd.Parameters.Clear();
            cmd.CommandText = "SELECT @@IDENTITY";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void InsertSubledgerTrans(SubledgerTrans sub, OleDbTransaction tx)
        {
            using var cmd = tx.Connection!.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
            INSERT INTO SubledgerTrans
             (SubledgerId, Unix, TransactionId)
            VALUES (?, ?, ?)";

            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = sub.SubledgerId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = Convert.ToInt32(sub.Unix) });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = sub.TransactionId });
            cmd.ExecuteNonQuery();
        }
        public int InsertSharePurchase(SharePurchase sp, OleDbTransaction tx)
        {
            using var cmd = tx.Connection!.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                    INSERT INTO SharePurchases
                     (ShareId, PurchaseDate, TransactionId, Unix, QtyPurchased, PurchaseRate)
                    VALUES (?, ?, ?, ?, ?, ?)";
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = sp.ShareId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date, Value = sp.PurchaseDate });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = sp.TransactionId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = sp.Unix });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Double, Value = (double)sp.QtyPurchased });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Double, Value = (double)sp.PurchaseRate });
            cmd.ExecuteNonQuery();

            cmd.Parameters.Clear();
            cmd.CommandText = "SELECT @@IDENTITY";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public string? GetShareCompanyName(int shareId, OleDbTransaction tx)
        {
            using var cmd = tx.Connection!.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT Company FROM SharesChart WHERE ShareId = ?";
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = shareId });
            var o = cmd.ExecuteScalar();
            return o == DBNull.Value ? null : Convert.ToString(o);
        }

        public IEnumerable<ShareBalanceRow> GetShareBalanceRows(int shareId, OleDbTransaction tx)
        {
            var list = new List<ShareBalanceRow>();

            using var cmd = tx.Connection!.CreateCommand();
            cmd.Transaction = tx;

            cmd.CommandText = @"
                SELECT PurchaseTransId, ShareId, PurchaseDate, Balance, PurchaseRate
                FROM QryShareBalance
                WHERE ShareId = ?
                ORDER BY PurchaseDate";

            cmd.Parameters.Add(new OleDbParameter
            {
                OleDbType = OleDbType.Integer,
                Value = shareId
            });

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                var row = new ShareBalanceRow
                {
                    PurchaseTransId = rdr.GetInt32(0),
                    ShareId = rdr.GetInt32(1),
                    PurchaseDate = rdr.GetDateTime(2),
                    Balance = rdr.IsDBNull(3) ? 0m : (decimal)Convert.ToDouble(rdr.GetValue(3)),
                    PurchaseRate = rdr.IsDBNull(4) ? 0m : (decimal)Convert.ToDouble(rdr.GetValue(4))
                };
                list.Add(row);
            }

            return list;
        }

        public int InsertShareSale(ShareSale sale, OleDbTransaction tx)
        {
            using var cmd = tx.Connection!.CreateCommand();
            cmd.Transaction = tx;

            cmd.CommandText = @"
                INSERT INTO ShareSales
                 (ShareId, PurchaseTransId, QtySold, SellingPrice, TransactionId, Unix)
                VALUES (?, ?, ?, ?, ?, ?)";

            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = sale.ShareId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = sale.PurchaseTransId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Double, Value = (double)sale.QtySold });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Double, Value = (double)sale.SellingPrice });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = sale.TransactionId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = sale.Unix });

            cmd.ExecuteNonQuery();

            cmd.Parameters.Clear();
            cmd.CommandText = "SELECT @@IDENTITY";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

}

