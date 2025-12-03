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
            // var (debitId, creditId) = await svc.MakeReceipt(
            //bankId: 104,
            //bankName: "ICICI Kilpauk",
            //fromId: 101,
            //fromName: "Capital Account",
            //yearId: 1,
            //tdate: new DateTime(2025, 04, 02),
            //amount: 250000m,
            //reference: "RCPT-1002",
            //narration: "Being Capital Deposisted in the bank");

            //  Console.WriteLine($"OK — debitId={debitId}, creditId={creditId}");


            // EXAMPLE 2: Payment -> Dr Union Bank(105), Cr Bank(104)
            //var (debitId, creditId) = await svc.MakePaymentAsync(
            //    bankId: 104,
            //    bankName: "Union Bank Account",
            //    toId: 102,
            //    toName: "Cash Account",
            //    yearId: 1,
            //    tdate: DateTime.Today,
            //    amount: 100000m,
            //    reference: "PMT-004",
            //    narration: "Being Transferred to Union Bank from ICICI");

            // Example 2a: Payment->Dr Electricity Expenses(312), Cr ICICI Bank
            //  var (debitId, creditId) = await svc.MakePaymentAsync(
            //bankId: 104,
            //bankName: "ICICI Bannk",
            //toId: 312,
            //toName: "Electricity Expenses",
            //yearId: 1,
            //tdate: new DateTime(2025, 04, 19),
            //amount: 212.25m,
            //reference: "PMT-003b",
            //narration: "Being Electricity Expenses paid from ICICI");

            // EXAMPLE 3: Payment->Dr UBI FD(118), Cr Bank(105) FD 1500
            // Option A: explicit types + use svc

            //(int debitId, int creditId) = await svc.MakePaymentAsync(
            //    bankId: 105, bankName: "Union Bank",
            //    toId: 118, toName: "Union Bank FD",
            //    yearId: 1,
            //    tdate: new DateTime(2025, 04, 11),
            //    amount: 25000m,
            //    reference: "PMT-0010a",
            //    narration: "Being Additonal Union Bank FD Made",
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
            //    tdate: new DateTime(2025, 06, 10),
            //    principalAmount: 20000m,
            //    interestAmount: 250m,
            //    reference: "RCPT-006",
            //    narration: "Being Consolidated Entry "
            //);

            // after DI: services.AddTransient<IShareTradeService, ShareTradeService>();





            //Example 5 Payment Share Purchase Dr ShareAccount (105) Cr Broker 311  2504
            //var shares = host.Services.GetRequiredService<IShareTradeService>();
            //var (drId, crId, spId, unix) = await shares.MakeSharePurchaseAsync(
            //    shareId: 2504,                  // from SharesChart
            //    debitSharesAccountId: 110,    // ICICI Shares with Schedules
            //    creditBrokerAccountId: 321,   // Stock Broker Account
            //    yearId: 1,
            //    glDate: new DateTime(2025, 04, 01), // books date
            //    purchaseDate: new DateTime(2025, 04, 21), // ACTUAL acquisition date
            //    qty: 100m,
            //    rate: 110.00m,
            //    reference: "SH-PUR-0009",
            //    narrationOverride: null,          // or pass your own narration
            //    sharesAccountName: "Shares with Schedules ICICI",  // optional
            //    brokerAccountName: "Stock Broker",  // optional
            //    companyName: null                 // null => we’ll fetch from SharesChart
            //);

            //Console.WriteLine($"OK: GL Dr={drId}, Cr={crId}, SharePurchasesId={spId}, Unix={unix}");
            var shares = host.Services.GetRequiredService<IShareTradeService>();
            var unixSale = await shares.MakeShareSaleAsync(
                    shareId: 2504,
                    brokerAccountId: 321,         // your broker/bank a/c
                    sharesAccountId: 110,         // ICICI shares a/c
                    ltCgAccountId: 300,           // LTCGAccountCode
                    stCgAccountId: 301,           // STCGAccountCode
                    ltClAccountId: 302,           // LTCLAccountCode
                    stClAccountId: 303,           // STCLAccountCode
                    yearId: 1,
                    saleDate: new DateTime(2025, 11, 19),
                    qtyToSell: 30m,
                    sellingPrice: 86.00m,
                    reference: "NovSS",
                    transactionType: "JE",
                    narrationOverride: null,
                    companyName: "CCC"  // or null to read from SharesChart
);

            Console.WriteLine($"Sale posted with Unix={unixSale}");


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
