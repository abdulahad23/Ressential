using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public class RequisitionReportViewModel
    {
        public int RequisitionId { get; set; }
        public string RequisitionNo { get; set; }
        public DateTime Date { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string CreatedBy { get; set; }
        public int TotalItems { get; set; }
        public List<RequisitionItemViewModel> Items { get; set; }
    }

    public class RequisitionItemViewModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string Unit { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal IssuedQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public string Status { get; set; }
    }
} 