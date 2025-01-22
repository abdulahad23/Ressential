using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace Ressential.Models
{
    [MetadataType(typeof(ItemCategoryMetadata))] // Link to the metadata class

    public partial class ItemCategory
    {
    }

    public class ItemCategoryMetadata
    {
        public int ItemCategoryId { get; set; }

        [Required(ErrorMessage = "Category Name is required.")]
        [StringLength(100, ErrorMessage = "Category Name cannot exceed 100 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "Category Name cannot contain special characters.")]

        public string ItemCategoryName { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string Description { get; set; }
    }

}