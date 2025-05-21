using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public class ItemStockReportViewModel
    {
        public int ItemId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string Unit { get; set; }
        public decimal Stock { get; set; }
        public decimal MinStock { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
    }
} 