using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public class WarehouseIssueDetailsHelper
    {
        public int IssueDetailId { get; set; }
        public int IssueId { get; set; }
        public int RequisitionId { get; set; }
        public int ItemId { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostApplied { get; set; }
        public string Description { get; set; }
        public decimal IssuedQuantity { get; set; }
    }
}