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

             var svc = host.Services.GetRequiredService<IBankEntryService>();

            //EXAMPLE 1: Receipt->Cr Bank(106) Dr.BankInterest(200)
            //  var (debitId, creditId) = await svc.MakeReceipt(
            //bankId: 104,
            //bankName: "ICICI Kilpauk",
            //fromId: 10231,
            //fromName: "Capital Account",
            //yearId: 1,
            //tdate: DateTime.Today,
            //amount: 500000m,
            //reference: "RCPT-1001",
            //narration: "Being Capital Deposisted in the bank");

            //Console.WriteLine($"OK — debitId={debitId}, creditId={creditId}");


            // EXAMPLE 2: Payment -> Dr Union Bank(105), Cr Bank(104)
            //var (debitId, creditId) = await svc.MakePaymentAsync(
            //    bankId: 104,
            //    bankName: "ICICI Bannk",
            //    toId: 105,
            //    toName: "Union Bank",
            //    yearId: 1,
            //    tdate: DateTime.Today,
            //    amount: 100000m,
            //    reference: "PMT-002",
            //    narration: "Being Transferred to Union Bank from ICICI");

            // Example 2a: Payment -> Dr Electricity Expenses (312), Cr ICICI Bank
          //  var (debitId, creditId) = await svc.MakePaymentAsync(
          //bankId: 104,
          //bankName: "ICICI Bannk",
          //toId: 312,
          //toName: "Electricity Expenses",
          //yearId: 1,
          //tdate: DateTime.Today,
          //amount: 111.75m,
          //reference: "PMT-003a",
          //narration: "Being Electricity Expenses paid from ICICI");

            // EXAMPLE 3: Payment->Dr UBI FD(118), Cr Bank(105) FD 1500
            //Option A: explicit types + use svc

            //(int debitId, int creditId) = await svc.MakePayment(
            //    bankId: 105, bankName: "Union Bank",
            //    toId: 118, toName: "Union Bank FD",
            //    yearId: 1,
            //    tdate: DateTime.Today,
            //    amount: 25000m,
            //    reference: "PMT-0001",
            //    narration: "Being Union Bank FD Made",
            //    subledgerId: 1500
            //);
            //Console.WriteLine($"OK — debitId={debitId}, creditId={creditId}");

            //Example 4 Receipt Dr Bank (105) Cr Fd 118 UBI FD FD 1500

            //var (bankDrId, fdCrId, interestCrId) = await svc.MakeReceiptWithSubledgerAsync(
            //    bankId: 105,                         // Union Bank (GL)
            //    bankName: "Union Bank Account",
            //    fdAccountId: 118,                    // FD GL account
            //    fdName: "Union Bank FD",             // <-- string, for particulars
            //    interestAccountId: 203,              // Interest income GL
            //    interestName: "Interest from FD's",  // for particulars
            //    fdSubledgerId: 1500,                 // <-- the FD SubledgerId (not a txn id)
            //    yearId: 1,
            //    tdate: DateTime.Today,
            //    principalAmount: 25000m,
            //    interestAmount: 250m,
            //    reference: "RCPT-006",
            //    narration: "Being Consolidated Entry Principal:25000 Interest:250"
            //);

            // after DI: services.AddTransient<IShareTradeService, ShareTradeService>();
            var shares = host.Services.GetRequiredService<IShareTradeService>();


            var (drId, crId, spId, unix) = await shares.MakeSharePurchaseAsync(
                shareId: 2504,                  // from SharesChart
                debitSharesAccountId: 110,    // ICICI Shares with Schedules
                creditBrokerAccountId: 321,   // Stock Broker Account
                yearId: 1,
                glDate: new DateTime(2025, 04, 01), // books date
                purchaseDate: new DateTime(2009, 06, 01), // ACTUAL acquisition date
                qty: 250m,
                rate: 102.35m,
                reference: "SH-PUR-0007",
                narrationOverride: null,          // or pass your own narration
                sharesAccountName: "Shares A/c",  // optional
                brokerAccountName: "Stock Broker",  // optional
                companyName: null                 // null => we’ll fetch from SharesChart
            );

            Console.WriteLine($"OK: GL Dr={drId}, Cr={crId}, SharePurchasesId={spId}, Unix={unix}");


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
                    // Data layer DI
                    var cs = ctx.Configuration.GetConnectionString("LedgerLens") ?? "";
                    services.Configure<OleDbOptions>(o => o.ConnectionString = cs);
                    services.AddSingleton<IConnectionFactory, OleDbConnectionFactory>();
                    services.AddTransient<ITransactionRepository, TransactionRepository>();

                    // Services
                    services.AddTransient<IBankEntryService, BankEntryService>();
                    services.AddTransient<IShareTradeService, ShareTradeService>();
                });
                
    }
}
