using System;

namespace Ressential.Models
{
    public class CostReportViewModel
    {
        // Common properties
        public DateTime Date { get; set; }
        public decimal TotalCost { get; set; }

        // Detail view properties
        public string ReferenceNo { get; set; }
        public string Type { get; set; }
        public string ItemName { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostPerUnit { get; set; }

        // Summary view properties
        public decimal SalesCost { get; set; }
        public decimal ConsumeCost { get; set; }
        public decimal WastageCost { get; set; }
    }
} 