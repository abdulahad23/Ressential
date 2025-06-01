using System;

namespace Ressential.Models
{
    public class ProfitLossReportViewModel
    {
        // Revenue Section
        public decimal TotalSalesRevenue { get; set; }
        public decimal OtherRevenue { get; set; }
        public decimal TotalRevenue { get; set; }

        // Cost of Sales Section
        public decimal TotalSalesCost { get; set; }
        public decimal GrossProfit { get; set; }

        // Other Cost Section
        public decimal ConsumeCost { get; set; }
        public decimal WastageCost { get; set; }
        public decimal TotalOtherCost { get; set; }

        // Profit Section
        public decimal OperatingProfit { get; set; }
        public decimal NetProfit { get; set; }

        // Metrics
        public decimal GrossProfitMargin { get; set; }
        public decimal NetProfitMargin { get; set; }
    }
} 