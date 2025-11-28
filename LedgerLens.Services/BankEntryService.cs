using LedgerLens.Data.Abstractions;
using LedgerLens.Data.Models;
using LedgerLens.Services.Abstractions;
using System;
using System.Data.OleDb;
using System.Globalization;
using System.Threading.Tasks;

namespace LedgerLens.Services
{
    public sealed class BankEntryService : IBankEntryService
    {
        private readonly IConnectionFactory _factory;
        private readonly ITransactionRepository _repo;

        public BankEntryService(IConnectionFactory factory, ITransactionRepository repo)
        {
            _factory = factory;
            _repo = repo;
        }

        private static string Plus(decimal a) => $"+{a.ToString("N2", CultureInfo.InvariantCulture)}";
        private static string Minus(decimal a) => $"-{a.ToString("N2", CultureInfo.InvariantCulture)}";

        public Task<(int debitId, int creditId)> MakePaymentAsync(
    int bankId, string bankName,
    int toId, string toName,
    int yearId, DateTime tdate, decimal amount,
    string reference, string narration,
    int? subledgerId = null)
        {
            // Payment semantics:
            //   Other Dr  (positive)   -> "To Bank (+amt)"
            //   Bank  Cr  (negative)   -> "By Other (-amt)"
            var otherDr = new GeneralLedger
            {
                AccountId = toId,
                YearId = yearId,
                Tdate = tdate,
                Ref = reference,
                Particulars = $"To {bankName} ({Plus(amount)})",
                Amount = amount,            // Dr = +
                TransactionType = "BE",
                Narration = narration
            };

            var bankCr = new GeneralLedger
            {
                AccountId = bankId,
                YearId = yearId,
                Tdate = tdate,
                Ref = reference,
                Particulars = $"By {toName} ({Minus(amount)})",
                Amount = -amount,           // Cr = -
                TransactionType = "BE",
                Narration = narration
            };

            // No subledger? simple 2-line post.
            if (subledgerId is null)
                return Post2Async(otherDr, bankCr);

            // With subledger: attach SubledgerTrans to the DEBIT (other) row.
            return PostPaymentWithSubledgerAsync(otherDr, bankCr, subledgerId.Value);
        }



        public Task<(int bankDrId, int otherCrId)> MakeReceipt(
            int bankId, string bankName,
            int fromId, string fromName,
            int yearId, DateTime tdate, decimal amount,
            string reference, string narration)
            => Post2Async(
                debit: new GeneralLedger
                {
                    AccountId = bankId,
                    YearId = yearId,
                    Tdate = tdate,
                    Ref = reference,
                    Particulars = $"To {fromName} ({Plus(amount)})",
                    Amount = amount,
                    TransactionType = "BE",
                    Narration = narration
                },
                credit: new GeneralLedger
                {
                    AccountId = fromId,
                    YearId = yearId,
                    Tdate = tdate,
                    Ref = reference,
                    Particulars = $"By {bankName} ({Minus(amount)})",
                    Amount = -amount,
                    TransactionType = "BE",
                    Narration = narration
                });

        public Task<(int bankDrId, int fdCrId, int? interestCrId)> MakeReceiptWithSubledgerAsync(
            int bankId, string bankName,
            int fdAccountId, string fdName,
            int interestAccountId, string interestName,
            int fdSubledgerId,
            int yearId, DateTime tdate,
            decimal principalAmount, decimal interestAmount,
            string reference, string narration)
        {
            if (principalAmount < 0 || interestAmount < 0)
                throw new ArgumentOutOfRangeException(nameof(principalAmount), "Amounts must be non-negative.");

            var total = principalAmount + interestAmount;
            if (total <= 0)
                throw new ArgumentOutOfRangeException(nameof(principalAmount), "Total must be > 0.");

            // Build GL rows (signs are critical)
            var bankDr = new GeneralLedger
            {
                AccountId = bankId,
                YearId = yearId,
                Tdate = tdate,
                Ref = reference,
                // Nice readable particulars, include both parts if interest > 0
                Particulars = interestAmount > 0
                    ? $"To {fdName} ({Plus(principalAmount)}) & {interestName} ({Plus(interestAmount)})"
                    : $"To {fdName} ({Plus(principalAmount)})",
                Amount = total,              // Dr = +
                TransactionType = "BE",
                Narration = narration
            };

            var fdCr = new GeneralLedger
            {
                AccountId = fdAccountId,
                YearId = yearId,
                Tdate = tdate,
                Ref = reference,
                Particulars = $"By {bankName} ({Minus(principalAmount)})",
                Amount = -principalAmount,   // Cr = -
                TransactionType = "BE",
                Narration = narration
            };

            // Optional third line for interest
            GeneralLedger? interestCr = null;
            if (interestAmount > 0)
            {
                interestCr = new GeneralLedger
                {
                    AccountId = interestAccountId,
                    YearId = yearId,
                    Tdate = tdate,
                    Ref = reference,
                    Particulars = $"By {bankName} ({Minus(interestAmount)})",
                    Amount = -interestAmount, // Cr = -
                    TransactionType = "BE",
                    Narration = narration
                };
            }

            return PostReceiptWithSubledgerAsync(bankDr, fdCr, interestCr, fdSubledgerId);
        }

        // =============== Internals ===============

        private Task<(int bankDrId, int otherCrId)> Post2Async(GeneralLedger debit, GeneralLedger credit)
        {
            using var con = _factory.CreateOpen();
            using var tx = con.BeginTransaction();
            try
            {
                var unix = _repo.GetNextUnix(tx);
                debit.Unix = (int)unix;
                credit.Unix = (int)unix;

                var drId = _repo.InsertGeneralLedger(debit, tx);
                var crId = _repo.InsertGeneralLedger(credit, tx);

                tx.Commit();
                return Task.FromResult((drId, crId));
            }
            catch (OleDbException ex)
            {
                try { tx.Rollback(); } catch { }
                var msg = ex.Message?.ToLowerInvariant() ?? "";
                if (msg.Contains("related record is required") || msg.Contains("reference integrity"))
                    throw new InvalidOperationException("Invalid account/year: the referenced record does not exist in Chart/AccountingYear.", ex);
                throw;
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }
        private Task<(int debitId, int creditId)> PostPaymentWithSubledgerAsync(
    GeneralLedger otherDr,
    GeneralLedger bankCr,
    int subledgerId)
        {
            using var con = _factory.CreateOpen();
            using var tx = con.BeginTransaction();
            try
            {
                var unix = _repo.GetNextUnix(tx);
                otherDr.Unix = (int)unix;
                bankCr.Unix = (int)unix;

                var drId = _repo.InsertGeneralLedger(otherDr, tx);
                var crId = _repo.InsertGeneralLedger(bankCr, tx);

                // SubledgerTrans must point to the OTHER (debit) transaction id for payments
                _repo.InsertSubledgerTrans(
                    new SubledgerTrans { SubledgerId = subledgerId, Unix = unix, TransactionId = drId }, tx);

                tx.Commit();
                return Task.FromResult((drId, crId));
            }
            catch (OleDbException ex)
            {
                try { tx.Rollback(); } catch { }
                var msg = ex.Message?.ToLowerInvariant() ?? "";
                if (msg.Contains("related record is required") || msg.Contains("reference integrity"))
                    throw new InvalidOperationException("Invalid account/year: the referenced record does not exist in Chart/AccountingYear.", ex);
                throw;
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }

        private Task<(int bankDrId, int fdCrId, int? interestCrId)> PostReceiptWithSubledgerAsync(
            GeneralLedger bankDr,
            GeneralLedger fdCr,
            GeneralLedger? interestCr,
            int fdSubledgerId)
        {
            using var con = _factory.CreateOpen();
            using var tx = con.BeginTransaction();
            try
            {
                var unix = _repo.GetNextUnix(tx);
                bankDr.Unix = (int)unix;
                fdCr.Unix = (int)unix;
                if (interestCr != null) interestCr.Unix = (int)unix;

                // Order: bank Dr -> FD Cr -> interest Cr (if any)
                var bankDrId = _repo.InsertGeneralLedger(bankDr, tx);
                var fdCrId = _repo.InsertGeneralLedger(fdCr, tx);
                int? interestCrId = null;
                if (interestCr != null)
                    interestCrId = _repo.InsertGeneralLedger(interestCr, tx);

                // SubledgerTrans must point to the FD credit (NOT the bank row)
                _repo.InsertSubledgerTrans(
                    new SubledgerTrans { SubledgerId = fdSubledgerId, Unix = unix, TransactionId = fdCrId }, tx);

                tx.Commit();
                return Task.FromResult((bankDrId, fdCrId, interestCrId));
            }
            catch (OleDbException ex)
            {
                try { tx.Rollback(); } catch { }
                var msg = ex.Message?.ToLowerInvariant() ?? "";
                if (msg.Contains("related record is required") || msg.Contains("reference integrity"))
                    throw new InvalidOperationException("Invalid account/year: the referenced record does not exist in Chart/AccountingYear.", ex);
                throw;
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }
    }
}
