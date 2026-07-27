using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;

namespace LedgerLens.Data.Repositories;

public sealed class LedgerAccountRepository
{
    private readonly IConnectionFactory _connectionFactory;

    public LedgerAccountRepository(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<LedgerAccount> GetAll()
    {
        var accounts = new List<LedgerAccount>();

        using var connection = _connectionFactory.CreateOpen();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
            SELECT
                AccountId,
                Account,
                Category,
                SubledgerFlag,
                DashboardGroup
            FROM Chart
            ORDER BY Account
        """;

        using var reader = command.ExecuteReader();

        while (reader!.Read())
        {
            accounts.Add(new LedgerAccount
            {
                AccountId = reader.GetInt32(0),
                Account = reader.GetString(1),
                Category = reader.GetString(2),
                SubledgerFlag = reader.GetString(3),
                DashboardGroup = reader.IsDBNull(4)
                                    ? string.Empty
                                    : reader.GetString(4)
            });
        }

        return accounts;
    }

    public void Add(LedgerAccount account)
    {
        using var connection = _connectionFactory.CreateOpen();
        using var command = connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO Chart
                (Account, Category, SubledgerFlag, DashboardGroup)
            VALUES
                (?, ?, ?, ?)
            """;

        var accountParameter = command.CreateParameter();
        accountParameter.Value = account.Account;
        command.Parameters.Add(accountParameter);

        var categoryParameter = command.CreateParameter();
        categoryParameter.Value = account.Category;
        command.Parameters.Add(categoryParameter);

        var subledgerParameter = command.CreateParameter();
        subledgerParameter.Value = account.SubledgerFlag;
        command.Parameters.Add(subledgerParameter);

        var dashboardParameter = command.CreateParameter();
        dashboardParameter.Value = account.DashboardGroup;
        command.Parameters.Add(dashboardParameter);

        command.ExecuteNonQuery();
    }


    public void Update(LedgerAccount account)
    {
        using var connection = _connectionFactory.CreateOpen();
        using var command = connection.CreateCommand();

        command.CommandText =
        """
            UPDATE Chart
            SET
                Account = ?,
                Category = ?,
                SubledgerFlag = ?,
                DashboardGroup = ?
            WHERE
                AccountId = ?
          """;

        var accountParameter = command.CreateParameter();
        accountParameter.Value = account.Account;
        command.Parameters.Add(accountParameter);

        var categoryParameter = command.CreateParameter();
        categoryParameter.Value = account.Category;
        command.Parameters.Add(categoryParameter);

        var subledgerParameter = command.CreateParameter();
        subledgerParameter.Value = account.SubledgerFlag;
        command.Parameters.Add(subledgerParameter);

        var dashboardGroupParameter = command.CreateParameter();
        dashboardGroupParameter.Value = account.DashboardGroup;
        command.Parameters.Add(dashboardGroupParameter);

        var accountIdParameter = command.CreateParameter();
        accountIdParameter.Value = account.AccountId;
        command.Parameters.Add(accountIdParameter);

        command.ExecuteNonQuery();
    }
}