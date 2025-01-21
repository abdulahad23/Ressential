using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(WarehouseIssueMetadata))]
    public partial class WarehouseIssue
    {
    }

    public class WarehouseIssueMetadata
    {
        [Required(ErrorMessage = "Issue Date is required")]
        public DateTime IssueDate { get; set; }


        [StringLength(50)]
        public string ReferenceNo { get; set; }


        [StringLength(255)]
        public string Memo { get; set; }

     
    }
}
