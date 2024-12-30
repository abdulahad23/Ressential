using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace Ressential.Models
{
    [MetadataType(typeof(ItemMetadata))] // Link to the metadata class

    public partial class Item
    {
    }

    public class ItemMetadata
    {
        [Required(ErrorMessage = "Item Name is required.")]
        [StringLength(100, ErrorMessage = "Item Name cannot exceed 100 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "Item Name cannot contain special characters.")]
        public string ItemName { get; set; }

        [Required(ErrorMessage = "SKU is required.")]
        [StringLength(50, ErrorMessage = "SKU cannot exceed 50 characters.")]
        public string Sku { get; set; }

        [Required(ErrorMessage = "Item Category is required.")]
        public int ItemCategoryId { get; set; }

        [Required(ErrorMessage = "Unit of Measure is required.")]
        public int UnitOfMeasureId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Minimum Stock Level must be a non-negative number.")]
        public decimal MinimumStockLevel { get; set; }

        [Required]
        public bool IsActive { get; set; }

       

        [Range(0, double.MaxValue, ErrorMessage = "Opening Stock Quantity must be a non-negative number.")]
        public decimal OpeningStockQuantity { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Opening Stock Value must be a non-negative number.")]
        public decimal OpeningStockValue { get; set; }

        [Required]
        public DateTime OpeningStockDate { get; set; }

    }


}