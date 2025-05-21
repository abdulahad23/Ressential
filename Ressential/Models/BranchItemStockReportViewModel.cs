using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public class BranchItemStockReportViewModel
    {
        public int BranchItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string Unit { get; set; }
        public decimal Stock { get; set; } // Current Quantity
        public decimal MinStock { get; set; } // MinimumStockLevel
        public decimal Price { get; set; } // CostPerUnit
        public decimal StockValue { get; set; } // Quantity * CostPerUnit
        public decimal OpeningStock { get; set; } // OpeningStockQuantity
        public decimal OpeningValue { get; set; } // OpeningStockValue
        public DateTime OpeningDate { get; set; } // OpeningStockDate
        public bool IsActive { get; set; } // Item active status
    }
} 