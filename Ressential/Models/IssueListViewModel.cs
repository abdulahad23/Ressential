using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public class IssueListViewModel
    {
        public int IssueId { get; set; }
        public string IssueNo { get; set; }
        public DateTime IssueDate { get; set; }
        public string ReferenceNo { get; set; }
        public string BranchName { get; set; }
        public string RequisitionNo { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
    }
}