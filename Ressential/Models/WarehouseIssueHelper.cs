using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Ressential.Models;

namespace Ressential.Models
{
    public class WarehouseIssueHelper
    {
        public System.DateTime IssueDate { get; set; }
        public string IssueNo { get; set; }
        public string ReferenceNo { get; set; }
        public int RequisitionId { get; set; }
        public string RequisitionNo { get; set; }
        public int BranchID { get; set; }
        public string BranchName { get; set; }
        public string Memo { get; set; }
        public string Status { get; set; }
        public string RequisitionStatus { get; set; }
        public int CreatedBy { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public int ModifiedBy { get; set; }
        public System.DateTime ModifiedAt { get; set; }
        public virtual ICollection<WarehouseIssueDetailsHelper> WarehouseIssueDetails { get; set; }

    }
}