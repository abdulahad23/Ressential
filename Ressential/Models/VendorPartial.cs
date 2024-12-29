using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace Ressential.Models
{
    [MetadataType(typeof(VendorMetadata))] // Link to the metadata class
    public partial class Vendor
    {


    }
    public class VendorMetadata
    {
        [Required]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        [RegularExpression(@"^(?![.,'""\s]+$)[a-zA-Z0-9.,'""\s]+$", ErrorMessage = "Name cannot consist only of special characters and must include at least one alphanumeric character.")]
        public string Name { get; set; }

        [StringLength(100, ErrorMessage = "Company Name cannot exceed 100 characters")]
        public string CompanyName { get; set; }

        [EmailAddress(ErrorMessage = "Invalid Email format")]
        public string Email { get; set; }

        [RegularExpression(@"^\+?\d{10,15}$", ErrorMessage = "Enter a valid contact number.")]
        public string Contact { get; set; }

        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        public string Address { get; set; }

        public string City { get; set; }

        public string Country { get; set; }

        [RegularExpression(@"^\d{5,10}$", ErrorMessage = "Postal Code must be between 5 and 10 digits")]
        public string PostalCode { get; set; }
        public int CreatedBy { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public Nullable<int> ModifiedBy { get; set; }
        public Nullable<System.DateTime> ModifiedAt { get; set; }
    }
}