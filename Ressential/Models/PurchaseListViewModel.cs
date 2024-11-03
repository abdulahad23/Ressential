using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public class PurchaseListViewModel
    {
        public int PurchaseId { get; set; }
        public string PurchaseNo { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string ReferenceNo { get; set; }
        public string VendorName { get; set; }
        public decimal TotalAmount { get; set; }
    }
}