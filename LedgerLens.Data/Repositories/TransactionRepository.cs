using System;
using System.Data.OleDb;
using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;

namespace LedgerLens.Data.Repositories
{
    public sealed class TransactionRepository : ITransactionRepository
    {
        private readonly IConnectionFactory _connectionFactory;

        public TransactionRepository(IConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }
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

        public List<BankEntryRow> GetBankEntries(int accountId, int yearId)
        {
            var entries = new List<BankEntryRow>();

            using var connection = _connectionFactory.CreateOpen();
            using var command = connection.CreateCommand();

            command.CommandText =
                """
                    SELECT
                        TransactionId,
                        AccountId,
                        Unix,
                        TDate,
                        Ref,
                        Particulars,
                        Amount,
                        Narration
                    FROM GeneralLedger
                    WHERE AccountId = ?
                      AND YearId = ?
                    ORDER BY TDate, Unix
                    """;

            var accountParameter = command.CreateParameter();
            accountParameter.Value = accountId;
            command.Parameters.Add(accountParameter);

            var yearParameter = command.CreateParameter();
            yearParameter.Value = yearId;
            command.Parameters.Add(yearParameter);

            using var reader = command.ExecuteReader();

            while (reader!.Read())
            {
                entries.Add(new BankEntryRow
                {
                    TransactionId = reader.GetInt32(0),
                    AccountId = reader.GetInt32(1),
                    Unix = reader.GetInt32(2),
                    TDate = reader.GetDateTime(3),
                    Ref = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Particulars = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Amount = reader.GetDecimal(6),
                    Narration = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    RunningBalance = 0M
                });
            }

            return entries;
        }
        public bool HasOtherFDTransactions(
                                            int subledgerId,
                                            int yearId,
                                            int unix)
        {
            using var connection = _connectionFactory.CreateOpen();
            using var command = connection.CreateCommand();

            command.CommandText =
                """
                    SELECT COUNT(*)
                    FROM SubledgerTrans AS st
                    INNER JOIN GeneralLedger AS gl
                        ON st.TransactionId = gl.TransactionId
                    WHERE st.SubledgerId = ?
                      AND (gl.YearId <> ? OR gl.[Unix] <> ?)
                 """;

            var p1 = command.CreateParameter();
            p1.Value = subledgerId;
            command.Parameters.Add(p1);

            var p2 = command.CreateParameter();
            p2.Value = yearId;
            command.Parameters.Add(p2);

            var p3 = command.CreateParameter();
            p3.Value = unix;
            command.Parameters.Add(p3);

            int count = Convert.ToInt32(command.ExecuteScalar());

            return count > 0;
        }

        public List<int> GetSubledgerIdsForUnix(int yearId, int unix)
        {
            var subledgerIds = new List<int>();

            using var connection = _connectionFactory.CreateOpen();
            using var command = connection.CreateCommand();

            command.CommandText =
                """
        SELECT DISTINCT st.SubledgerId
        FROM SubledgerTrans AS st
        INNER JOIN GeneralLedger AS gl
            ON st.TransactionId = gl.TransactionId
        WHERE gl.YearId = ?
          AND gl.[Unix] = ?
        """;

            var p1 = command.CreateParameter();
            p1.Value = yearId;
            command.Parameters.Add(p1);

            var p2 = command.CreateParameter();
            p2.Value = unix;
            command.Parameters.Add(p2);

            using var reader = command.ExecuteReader();

            while (reader != null && reader.Read())
            {
                subledgerIds.Add(
                    Convert.ToInt32(reader["SubledgerId"]));
            }

            return subledgerIds;
        }

        public bool IsClosedByDifferentUnix(
                int subledgerId,
                int unix)
        {
            using var connection = _connectionFactory.CreateOpen();
            using var command = connection.CreateCommand();

            command.CommandText =
                """
                    SELECT COUNT(*)
                    FROM SubledgerChart
                    WHERE SubledgerId = ?
                      AND Active = 'N'
                      AND ([Unix] IS NULL OR [Unix] <> ?)
                """;

            var p1 = command.CreateParameter();
            p1.Value = subledgerId;
            command.Parameters.Add(p1);

            var p2 = command.CreateParameter();
            p2.Value = unix;
            command.Parameters.Add(p2);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }
        public int DeleteBankEntry(
            int transactionId,
            int bankAccountId,
            int yearId,
            int unix)
        {
            if (transactionId <= 0 || bankAccountId <= 0 ||
                yearId <= 0 || unix <= 0)
            {
                throw new InvalidOperationException(
                    "Invalid Bank Entry identification.");
            }

            using var con = _connectionFactory.CreateOpen();
            using var tx = con.BeginTransaction();

            try
            {
                // 1. Allow deletion only in the latest accounting year.
                using (var cmd = con.CreateCommand())
                {
                    cmd.Transaction = tx;

                    cmd.CommandText =
                        "SELECT MAX(YearID) FROM AccountYears";

                    object? result = cmd.ExecuteScalar();

                    if (result == null || result == DBNull.Value ||
                        Convert.ToInt32(result) != yearId)
                    {
                        throw new InvalidOperationException(
                            "Deletion is permitted only in the current accounting year.");
                    }
                }

                // 2. Verify the exact bank transaction.
                using (var cmd = con.CreateCommand())
                {
                    cmd.Transaction = tx;

                    cmd.CommandText =
                        """
                    SELECT COUNT(*)
                    FROM GeneralLedger
                    WHERE TransactionId = ?
                      AND AccountId = ?
                      AND YearId = ?
                      AND [Unix] = ?
                    """;

                    cmd.Parameters.AddWithValue("@p1", transactionId);
                    cmd.Parameters.AddWithValue("@p2", bankAccountId);
                    cmd.Parameters.AddWithValue("@p3", yearId);
                    cmd.Parameters.AddWithValue("@p4", unix);

                    if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                    {
                        throw new InvalidOperationException(
                            "Bank transaction could not be verified.");
                    }
                }

                // Protect opening, closing and other non-Bank Entry transactions
                using (var cmd = con.CreateCommand())
                {
                    cmd.Transaction = tx;

                    cmd.CommandText =
                        """
                        SELECT COUNT(*)
                        FROM GeneralLedger
                        WHERE YearId = ?
                          AND [Unix] = ?
                          AND (
                              [TransactionType] IS NULL
                              OR [TransactionType] <> 'BE'
                          )
                        """;

                    cmd.Parameters.AddWithValue("@p1", yearId);
                    cmd.Parameters.AddWithValue("@p2", unix);

                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    {
                        throw new InvalidOperationException(
                            "This transaction cannot be deleted from Bank Entry.\n\n" +
                            "Opening balances, year-end adjustments and " +
                            "other non-Bank Entry transactions are protected.");
                    }
                }

                // 3. Find the FDs associated with this UNIX.
                // Positive FD movement = Payment.
                // Negative FD movement = Receipt.
                var fdMovements = new Dictionary<int, bool>();

                using (var cmd = con.CreateCommand())
                {
                    cmd.Transaction = tx;

                    cmd.CommandText =
                        """
                        SELECT st.SubledgerId, gl.Amount
                        FROM SubledgerTrans AS st
                        INNER JOIN GeneralLedger AS gl
                            ON st.TransactionId = gl.TransactionId
                        WHERE gl.YearId = ?
                          AND gl.[Unix] = ?
                        """;

                    cmd.Parameters.AddWithValue("@p1", yearId);
                    cmd.Parameters.AddWithValue("@p2", unix);

                    using var reader = cmd.ExecuteReader();

                    while (reader != null && reader.Read())
                    {
                        int subledgerId = Convert.ToInt32(reader["SubledgerId"]);
                        decimal amount = Convert.ToDecimal(reader["Amount"]);

                        if (amount == 0)
                        {
                            throw new InvalidOperationException(
                                "Cannot delete an FD transaction containing a zero principal movement.");
                        }

                        bool isPayment = amount > 0;

                        if (fdMovements.TryGetValue(subledgerId, out bool previousType))
                        {
                            if (previousType != isPayment)
                            {
                                throw new InvalidOperationException(
                                    "Mixed FD movements in one UNIX require separate review.");
                            }
                        }
                        else
                        {
                            fdMovements.Add(subledgerId, isPayment);
                        }
                    }
                }

                // 4. Validate FD dependencies.
                var fdsToReactivate = new List<int>();

                foreach (var fd in fdMovements)
                {
                    int subledgerId = fd.Key;
                    bool isPayment = fd.Value;

                    if (isPayment)
                    {
                        // FD Payment: block if any other FD transaction exists.
                        using var cmd = con.CreateCommand();

                        cmd.Transaction = tx;

                        cmd.CommandText =
                            """
                                    SELECT COUNT(*)
                                    FROM SubledgerTrans AS st
                                    INNER JOIN GeneralLedger AS gl
                                        ON st.TransactionId = gl.TransactionId
                                    WHERE st.SubledgerId = ?
                                      AND (gl.YearId <> ? OR gl.[Unix] <> ?)
                                    """;

                        cmd.Parameters.AddWithValue("@p1", subledgerId);
                        cmd.Parameters.AddWithValue("@p2", yearId);
                        cmd.Parameters.AddWithValue("@p3", unix);

                        if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                        {
                            throw new InvalidOperationException(
                                $"Cannot delete FD Payment.\n\n" +
                                $"SubledgerId: {subledgerId}\n" +
                                "Other FD transactions depend on this payment.");
                        }
                    }
                    else
                    {
                        // FD Receipt: check whether another UNIX closed the FD.
                        using var cmd = con.CreateCommand();

                        cmd.Transaction = tx;

                        cmd.CommandText =
                            """
                                SELECT Active, [Unix]
                                FROM SubledgerChart
                                WHERE SubledgerId = ?
                                """;

                        cmd.Parameters.AddWithValue("@p1", subledgerId);

                        using var reader = cmd.ExecuteReader();

                        if (reader == null || !reader.Read())
                        {
                            throw new InvalidOperationException(
                                "FD could not be found in SubledgerChart.");
                        }

                        string active = reader["Active"]?.ToString() ?? "";

                        int? closingUnix =
                            reader["Unix"] == DBNull.Value
                                ? null
                                : Convert.ToInt32(reader["Unix"]);

                        if (active == "N")
                        {
                            if (closingUnix != unix)
                            {
                                throw new InvalidOperationException(
                                    $"Cannot delete FD Receipt.\n\n" +
                                    $"SubledgerId: {subledgerId}\n" +
                                    "This FD was closed by another transaction.");
                            }

                            // This is the actual closing transaction.
                            fdsToReactivate.Add(subledgerId);
                        }
                        else if (active != "Y")
                        {
                            throw new InvalidOperationException(
                                "The FD has an invalid Active status.");
                        }
                    }
                }

                // 5. Delete child records FIRST.
                using (var cmd = con.CreateCommand())
                {
                    cmd.Transaction = tx;

                    cmd.CommandText =
                        """
                            DELETE FROM SubledgerTrans
                            WHERE TransactionId IN
                            (
                                SELECT TransactionId
                                FROM GeneralLedger
                                WHERE YearId = ?
                                  AND [Unix] = ?
                            )
                            """;

                    cmd.Parameters.AddWithValue("@p1", yearId);
                    cmd.Parameters.AddWithValue("@p2", unix);

                    cmd.ExecuteNonQuery();
                }

                // 6. Delete the complete General Ledger UNIX group.
                int deletedRows;

                using (var cmd = con.CreateCommand())
                {
                    cmd.Transaction = tx;

                    cmd.CommandText =
                        """
                        DELETE FROM GeneralLedger
                        WHERE YearId = ?
                          AND [Unix] = ?
                        """;

                    cmd.Parameters.AddWithValue("@p1", yearId);
                    cmd.Parameters.AddWithValue("@p2", unix);

                    deletedRows = cmd.ExecuteNonQuery();

                    if (deletedRows <= 0)
                    {
                        throw new InvalidOperationException(
                            "No General Ledger transactions were deleted.");
                    }
                }

                // 7. Restore FD status when deleting its closing UNIX.
                foreach (int subledgerId in fdsToReactivate)
                {
                    using var cmd = con.CreateCommand();

                    cmd.Transaction = tx;

                    cmd.CommandText =
                        """
                            UPDATE SubledgerChart
                            SET Active = 'Y',
                                [Unix] = NULL
                            WHERE SubledgerId = ?
                              AND [Unix] = ?
                              AND Active = 'N'
                            """;

                    cmd.Parameters.AddWithValue("@p1", subledgerId);
                    cmd.Parameters.AddWithValue("@p2", unix);

                    if (cmd.ExecuteNonQuery() != 1)
                    {
                        throw new InvalidOperationException(
                            "Unable to reactivate the Fixed Deposit.");
                    }
                }

                // 8. Everything succeeded.
                tx.Commit();

                return deletedRows;
            }
            catch
            {
                try
                {
                    tx.Rollback();
                }
                catch
                {
                    // Preserve the original exception.
                }

                throw;
            }
        }




    }
}
