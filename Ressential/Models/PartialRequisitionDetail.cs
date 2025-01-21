using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(RequisitionDetailMetadata))]
    public partial class RequisitionDetail
    {
    }

    public class RequisitionDetailMetadata
    {
       
        [Required(ErrorMessage = "Item ID is required")]
        public int ItemId { get; set; }

        [StringLength(255, ErrorMessage = "Description can be up to 255 characters long.")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
        public decimal Quantity { get; set; }
    }
}
