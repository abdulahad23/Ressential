using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(RequisitionMetadata))]
    public partial class Requisition
    {
    }

    public class RequisitionMetadata
    {
        

        [Required(ErrorMessage = "Requisition Date is required")]
        public DateTime RequisitionDate { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

       
    }
}
