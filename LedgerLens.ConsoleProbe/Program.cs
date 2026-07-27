using LedgerLens.Data;
using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Repositories;
using LedgerLens.Services;
using LedgerLens.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LedgerLens.ConsoleProbe
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            using var host = CreateHostBuilder().Build();

            var ledgerAccountRepository =
                host.Services.GetRequiredService<LedgerAccountRepository>();

            var accounts = ledgerAccountRepository.GetAll();

            Console.WriteLine("First 10 Ledger Accounts");
            Console.WriteLine("------------------------");

            foreach (var account in accounts.Take(10))
            {
                Console.WriteLine(
                    $"{account.AccountId} | " +
                    $"{account.Account} | " +
                    $"{account.Category} | " +
                    $"{account.SubledgerFlag} | " +
                    $"{account.DashboardGroup}");
            }

            Console.WriteLine();
            Console.WriteLine($"Total Accounts: {accounts.Count}");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(cfg =>
                {
                    cfg.SetBasePath(AppContext.BaseDirectory);
                    cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((ctx, services) =>
                {
                    var cs = ctx.Configuration.GetConnectionString("LedgerLens") ?? "";

                    services.Configure<OleDbOptions>(o =>
                        o.ConnectionString = cs);

                    services.AddSingleton<IConnectionFactory, OleDbConnectionFactory>();

                    services.AddTransient<ITransactionRepository, TransactionRepository>();
                    services.AddTransient<LedgerAccountRepository>();

                    services.AddTransient<IBankEntryService, BankEntryService>();
                    services.AddTransient<IShareTradeService, ShareTradeService>();
                });
    }
}