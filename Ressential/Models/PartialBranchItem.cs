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


        [Required(ErrorMessage = "Minimum Stock Level is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "Minimum Stock Level must be a positive value.")]
        public decimal MinimumStockLevel { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "Quantity must be a positive value.")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Cost Per Unit is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "Cost Per Unit must be a positive value.")]
        public decimal CostPerUnit { get; set; }

        [Required(ErrorMessage = "Opening Stock Quantity is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "Opening Stock Quantity must be a positive value.")]
        public decimal OpeningStockQuantity { get; set; }

        [Required(ErrorMessage = "Opening Stock Value is required.")]
        [Range(0, double.MaxValue, ErrorMessage = "Opening Stock Value must be a positive value.")]
        public decimal OpeningStockValue { get; set; }

        [Required(ErrorMessage = "Opening Stock Date is required.")]
        public DateTime OpeningStockDate { get; set; }

       
    }
}
