using System;
using System.ComponentModel.DataAnnotations;

namespace Ressential.Models
{
    [MetadataType(typeof(BranchMetadata))] // Link to the metadata class
    public partial class Branch
    {
    }

    public class BranchMetadata
    {
        [Required(ErrorMessage = "Branch Name is required.")]
        [StringLength(100, ErrorMessage = "Branch Name cannot exceed 100 characters.")]
        public string BranchName { get; set; }

        [Required(ErrorMessage = "Branch Code is required.")]
        [StringLength(50, ErrorMessage = "Branch Code cannot exceed 50 characters.")]
        public string BranchCode { get; set; }

        [Required(ErrorMessage = "Contact is required.")]
        [RegularExpression(@"^\+?\d{10,15}$", ErrorMessage = "Enter a valid contact number.")]
        public string BranchContact { get; set; }

        [Required(ErrorMessage = "Owner Name is required.")]
        [StringLength(100, ErrorMessage = "Owner Name cannot exceed 100 characters.")]
        public string OwnerName { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
        public string Address { get; set; }

        [Required(ErrorMessage = "City is required.")]
        public string City { get; set; }

        [Required(ErrorMessage = "Country is required.")]
        public string Country { get; set; }

        [RegularExpression(@"^\d{5,10}$", ErrorMessage = "Postal Code must be between 5 and 10 digits.")]
        public string PostalCode { get; set; }
        [Required]
        public bool IsActive { get; set; }
    }
}
