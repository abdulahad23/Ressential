using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ressential.Models
{
    [MetadataType(typeof(ProductMetadata))]
    public partial class Product
    {
    }

    public class ProductMetadata
    {
        [Required(ErrorMessage = "Product Code is required")]
        [StringLength(50)]
        public string ProductCode { get; set; }

        [Required(ErrorMessage = "Product Name is required")]
        [StringLength(100)]
        public string ProductName { get; set; }

      

        public string ProductImage { get; set; }

        [Required(ErrorMessage = "Product Price is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Product Price must be a positive value.")]
        public decimal ProductPrice { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

        [Required(ErrorMessage = "Product Category ID is required")]
        public int ProductCategoryId { get; set; }

        [Required]
        public bool IsActive { get; set; }

        
    }
}
