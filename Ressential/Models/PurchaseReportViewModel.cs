using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public class PurchaseReportViewModel
    {
        public int PurchaseId { get; set; }
        public string PurchaseNo { get; set; }
        public DateTime Date { get; set; }
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public string Reference { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public List<PurchaseItemDetail> Items { get; set; }
    }

    public class PurchaseItemDetail
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemCode { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
} 