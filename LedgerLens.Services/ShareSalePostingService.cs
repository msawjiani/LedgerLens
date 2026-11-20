using LedgerLens.Data.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LedgerLens.Services
{
    public sealed class ShareSalePostingService
    {
        private readonly ShareSaleService _shareSaleService;

        private readonly IConnectionFactory _factory;
        private readonly ITransactionRepository _repo;

        // STEP 5D.2 - Resolve Capital Gain / Loss Account

        private static int GetCapitalAccountId(
            string classification,
            int ltcgAccountId,
            int stcgAccountId,
            int ltclAccountId,
            int stclAccountId)
        {
            int accountId = classification switch
            {
                "LTCG" => ltcgAccountId,
                "STCG" => stcgAccountId,
                "LTCL" => ltclAccountId,
                "STCL" => stclAccountId,

                _ => throw new InvalidOperationException(
                    $"Unknown capital gain classification: {classification}")
            };

            if (accountId <= 0)
            {
                throw new InvalidOperationException(
                    $"Capital account is not configured for {classification}.");
            }

            return accountId;
        }
        public ShareSalePostingService(
            IConnectionFactory factory,
            ITransactionRepository repo)
        {
            _factory = factory;
            _repo = repo;

            _shareSaleService = new ShareSaleService(factory);
        }

        // STEP 5C.1 - Prepare Share Sale Posting

        public List<(int PurchaseTransId,
                     int SharesAtCostAccountId,
                     decimal QtySold,
                     decimal PurchaseCost,
                     decimal SaleProceeds,
                     decimal GainLoss,
                     string Classification)> PrepareSale(
            int shareId,
            DateTime saleDate,
            decimal quantity,
            decimal sellingPrice)
        {
            var details =
                _shareSaleService.CalculateSaleDetails(
                    shareId,
                    saleDate,
                    quantity,
                    sellingPrice);

            var result = details.Select(x =>
            {
                if (x.Lot.SharesAtCostAccountId <= 0)
                    throw new InvalidOperationException(
                        "Invalid Shares-at-Cost AccountId.");

                string classification =
                    _shareSaleService.ClassifyCapitalGain(
                        x.Lot.PurchaseDate,
                        saleDate,
                        x.GainLoss);

                return (
                    PurchaseTransId: x.Lot.PurchaseTransId,
                    SharesAtCostAccountId:
                        x.Lot.SharesAtCostAccountId,
                    QtySold: x.QtySold,
                    PurchaseCost: x.PurchaseCost,
                    SaleProceeds: x.SaleProceeds,
                    GainLoss: x.GainLoss,
                    Classification: classification
                );
            }).ToList();



            // Verify accounting balance
            decimal totalProceeds =
                result.Sum(x => x.SaleProceeds);

            decimal totalCost =
                result.Sum(x => x.PurchaseCost);

            decimal totalGainLoss =
                result.Sum(x => x.GainLoss);

            if (totalProceeds != totalCost + totalGainLoss)
                throw new InvalidOperationException(
                    "Share Sale accounting does not balance.");

            return result;
        }
    }
}