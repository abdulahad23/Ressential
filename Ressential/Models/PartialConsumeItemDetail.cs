using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(ConsumeItemDetailsMetadata))]
    public partial class ConsumeItemDetail
    {
    }

    public class ConsumeItemDetailsMetadata
    {

        [Required(ErrorMessage = "Item ID is required")]
        public int ItemId { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

        [Required(ErrorMessage = "Item Quantity is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Item Quantity must be a positive value.")]
        public decimal ItemQuantity { get; set; }

    }
}
