using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using global::LedgerLens.Data.Abstractions;
using global::LedgerLens.Data.Models;

namespace LedgerLens.Data.Repositories
{




        public class SubledgerRepository
        {
            private readonly IConnectionFactory _connectionFactory;

            public SubledgerRepository(IConnectionFactory connectionFactory)
            {
                _connectionFactory = connectionFactory;
            }

            public List<Subledger> GetByAccountId(int accountId)
            {
                var subledgers = new List<Subledger>();

                using var connection = _connectionFactory.CreateOpen();
                using var command = connection.CreateCommand();

                command.CommandText =
                """
            SELECT SubledgerId, AccountId, Subaccount
            FROM SubledgerChart
            WHERE AccountId = ?
            ORDER BY Subaccount
            """;

                var accountParameter = command.CreateParameter();
                accountParameter.Value = accountId;
                command.Parameters.Add(accountParameter);

                using var reader = command.ExecuteReader();

                while (reader != null && reader.Read())
                {
                    subledgers.Add(new Subledger
                    {
                        SubledgerId = Convert.ToInt32(reader["SubledgerId"]),
                        AccountId = Convert.ToInt32(reader["AccountId"]),
                        Subaccount = reader["Subaccount"]?.ToString() ?? ""
                    });
                }

                return subledgers;
            }

            public void Add(Subledger subledger)
            {
                using var connection = _connectionFactory.CreateOpen();
                using var command = connection.CreateCommand();

                command.CommandText =
                """
            INSERT INTO SubledgerChart
                (AccountId, Subaccount)
            VALUES
                (?, ?)
            """;

                var accountParameter = command.CreateParameter();
                accountParameter.Value = subledger.AccountId;
                command.Parameters.Add(accountParameter);

                var subaccountParameter = command.CreateParameter();
                subaccountParameter.Value = subledger.Subaccount;
                command.Parameters.Add(subaccountParameter);

                command.ExecuteNonQuery();
            }

            public void Update(Subledger subledger)
            {
                using var connection = _connectionFactory.CreateOpen();
                using var command = connection.CreateCommand();

                command.CommandText =
                """
            UPDATE SubledgerChart
            SET Subaccount = ?
            WHERE SubledgerId = ?
            """;

                var subaccountParameter = command.CreateParameter();
                subaccountParameter.Value = subledger.Subaccount;
                command.Parameters.Add(subaccountParameter);

                var idParameter = command.CreateParameter();
                idParameter.Value = subledger.SubledgerId;
                command.Parameters.Add(idParameter);

                command.ExecuteNonQuery();
            }
        }
    }

