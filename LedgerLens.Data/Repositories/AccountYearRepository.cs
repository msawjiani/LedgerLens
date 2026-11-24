using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;
using System.Data.OleDb;

namespace LedgerLens.Data.Repositories
{
    public sealed class AccountYearRepository
    {
        private readonly IConnectionFactory _connectionFactory;

        public AccountYearRepository(IConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public List<AccountingYear> GetAccountingYears()
        {
            var years = new List<AccountingYear>();

            using var connection = _connectionFactory.CreateOpen();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT YearId, StartDate, EndDate FROM AccountYears";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                years.Add(new AccountingYear
                {
                    YearId = reader.GetInt32(0),
                    StartDate = reader.GetDateTime(1),
                    EndDate = reader.GetDateTime(2),
                    
                });
            }

            return years
                .OrderBy(y => y.EndDate)
                .ToList();
        }
        // STEP 7D.4 - Insert Next Accounting Year
        // Uses caller's OleDb transaction

        public int InsertAccountingYear(
            DateTime startDate,
            DateTime endDate,
            OleDbTransaction tx)
        {
            if (tx?.Connection == null)
                throw new InvalidOperationException(
                    "An active database transaction is required.");

            if (startDate.Date > endDate.Date)
                throw new InvalidOperationException(
                    "Invalid accounting year dates.");

            using var cmd = tx.Connection.CreateCommand();

            cmd.Transaction = tx;

            cmd.CommandText =
                "INSERT INTO AccountYears " +
                "([StartDate], [EndDate]) " +
                "VALUES (?, ?)";

            cmd.Parameters.Add(new OleDbParameter
            {
                OleDbType = OleDbType.Date,
                Value = startDate.Date
            });

            cmd.Parameters.Add(new OleDbParameter
            {
                OleDbType = OleDbType.Date,
                Value = endDate.Date
            });

            cmd.ExecuteNonQuery();

            // Retrieve the newly generated AutoNumber YearId.

            cmd.Parameters.Clear();

            cmd.CommandText = "SELECT @@IDENTITY";

            object? result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException(
                    "Unable to retrieve the new Accounting YearId.");

            return Convert.ToInt32(result);
        }
    }
}