using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;

namespace LedgerLens.Data.Repositories
{
    public class CompanyRepository
    {
        private readonly IConnectionFactory _connectionFactory;

        public CompanyRepository(IConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public List<Company> GetByAccountId(int accountId)
        {
            var companies = new List<Company>();

            using var connection = _connectionFactory.CreateOpen();
            using var command = connection.CreateCommand();

            command.CommandText =
            """
            SELECT ShareId, Company, AccountId
            FROM SharesChart
            WHERE AccountId = ?
            ORDER BY Company
            """;

            var accountIdParameter = command.CreateParameter();
            accountIdParameter.Value = accountId;
            command.Parameters.Add(accountIdParameter);

            using var reader = command.ExecuteReader();

            while (reader != null && reader.Read())
            {
                companies.Add(new Company
                {
                    ShareId = Convert.ToInt32(reader["ShareId"]),
                    CompanyName = reader["Company"]?.ToString() ?? "",
                    AccountId = Convert.ToInt32(reader["AccountId"])
                });
            }

            return companies;
        }

        public void Add(Company company)
        {
            using var connection = _connectionFactory.CreateOpen();
            using var command = connection.CreateCommand();

            command.CommandText =
            """
                INSERT INTO SharesChart
                    (Company, AccountId)
                VALUES
                    (?, ?)
            """;

            var companyParameter = command.CreateParameter();
            companyParameter.Value = company.CompanyName;
            command.Parameters.Add(companyParameter);

            var accountIdParameter = command.CreateParameter();
            accountIdParameter.Value = company.AccountId;
            command.Parameters.Add(accountIdParameter);

            command.ExecuteNonQuery();
        }

        public void Update(Company company)
        {
            using var connection = _connectionFactory.CreateOpen();
            using var command = connection.CreateCommand();

            command.CommandText =
            """
                UPDATE SharesChart
                SET
                    Company = ?,
                    AccountId = ?
                WHERE
                    ShareId = ?
             """;

            var companyParameter = command.CreateParameter();
            companyParameter.Value = company.CompanyName;
            command.Parameters.Add(companyParameter);

            var accountIdParameter = command.CreateParameter();
            accountIdParameter.Value = company.AccountId;
            command.Parameters.Add(accountIdParameter);

            var shareIdParameter = command.CreateParameter();
            shareIdParameter.Value = company.ShareId;
            command.Parameters.Add(shareIdParameter);

            command.ExecuteNonQuery();
        }
    }
}