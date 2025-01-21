using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(PurchaseDetailMetadata))]
    public partial class PurchaseDetail
    {
    }

    public class PurchaseDetailMetadata
    {
        

        [Required(ErrorMessage = "Item ID is required")]
        public int ItemId { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Unit Price is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Unit Price must be greater than zero.")]
        public decimal UnitPrice { get; set; }

        [StringLength(255, ErrorMessage = "Description can be up to 255 characters long.")]
        public string Description { get; set; }
    }
}
