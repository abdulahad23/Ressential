using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(ItemMetadata))]
    public partial class Item
    {
        // Additional methods or properties can be added here
    }

    public class ItemMetadata
    {
     
        [Required(ErrorMessage = "Item Name is required")]
        [StringLength(100, ErrorMessage = "Item Name cannot exceed 100 characters")]
        public string ItemName { get; set; }

        [Required(ErrorMessage = "SKU is required")]
        [StringLength(50, ErrorMessage = "SKU cannot exceed 50 characters")]
        public string Sku { get; set; }

        [Required(ErrorMessage = "Item Category ID is required")]
        public int ItemCategoryId { get; set; }

        [Required(ErrorMessage = "Unit of Measure ID is required")]
        public int UnitOfMeasureId { get; set; }

        [Required(ErrorMessage = "Minimum Stock Level is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Minimum Stock Level must be a positive value")]
        public decimal MinimumStockLevel { get; set; }

        [Required(ErrorMessage = "Is Active status is required")]
        public bool IsActive { get; set; }


        [Required(ErrorMessage = "Opening Stock Quantity is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Opening Stock Quantity must be a positive value")]
        public decimal OpeningStockQuantity { get; set; }

        [Required(ErrorMessage = "Opening Stock Value is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Opening Stock Value must be a positive value")]
        public decimal OpeningStockValue { get; set; }

        [Required(ErrorMessage = "Opening Stock Date is required")]
        public DateTime OpeningStockDate { get; set; }

    }
}
