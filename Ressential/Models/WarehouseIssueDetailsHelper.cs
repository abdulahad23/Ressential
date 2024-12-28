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
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal CostApplied { get; set; }
        public string Description { get; set; }
        public decimal PreviousIssuedQuantity { get; set; } = 0;
        public decimal IssuedQuantity { get; set; }
    }
}