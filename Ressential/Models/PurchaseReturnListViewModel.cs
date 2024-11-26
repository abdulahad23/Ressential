using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public class PurchaseReturnListViewModel
    {
        public int PurchaseReturnId { get; set; }
        public string PurchaseReturnNo { get; set; }
        public DateTime PurchaseReturnDate { get; set; }
        public string ReferenceNo { get; set; }
        public string VendorName { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
    }
}