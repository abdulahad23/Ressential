using System;
using System.ComponentModel.DataAnnotations;

namespace Ressential.Models
{
    [MetadataType(typeof(ProductCategoryMetadata))] // Link to metadata class
    public partial class ProductCategory
    {
        // Additional logic (if needed) can be added here
    }

    public class ProductCategoryMetadata
    {
        [Required(ErrorMessage = "Category Name is required.")]
        [StringLength(100, ErrorMessage = "Category Name cannot exceed 100 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "Category Name cannot contain special characters.")]

        public string ProductCategoryName { get; set; }

        [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters.")]
        public string Description { get; set; }

        
    }
}
