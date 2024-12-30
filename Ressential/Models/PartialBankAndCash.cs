using System;
using System.ComponentModel.DataAnnotations;

namespace Ressential.Models
{
    [MetadataType(typeof(AccountMetadata))] // Link to the metadata class
    public partial class Account
    {

    }

    public class AccountMetadata
    {
        [Required]
        [RegularExpression(@"^(Bank|Cash)$", ErrorMessage = "Account Type must be 'Bank' or 'Cash'.")]
        public string AccountType { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 9, ErrorMessage = "Account Title must be between 9 and 100 characters.")]
        public string AccountTitle { get; set; }

        [StringLength(100, ErrorMessage = "Bank Name cannot exceed 100 characters")]
        public string BankName { get; set; }

        [StringLength(50, ErrorMessage = "Account Number cannot exceed 50 characters")]
        public string AccountNumber { get; set; }

        [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters")]
        public string Description { get; set; }

       
    }
}
