using System;
using System.ComponentModel.DataAnnotations;

namespace Ressential.Models
{
    [MetadataType(typeof(BranchItemMetadata))] // Link to the metadata class
    public partial class BranchItem
    {
        // Additional logic, if needed, can be added here.
    }

    public class BranchItemMetadata
    {
        [Required(ErrorMessage = "Item is required.")]
        public int ItemId { get; set; }

        [Required(ErrorMessage = "Branch is required.")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "Minimum Stock Level is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "Minimum Stock Level must be a positive value.")]
        public decimal MinimumStockLevel { get; set; }

        [Required]
        public bool IsActive { get; set; }

      
    }
}
