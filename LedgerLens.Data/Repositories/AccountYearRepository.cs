using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;

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
    }
}