using System;
using System.ComponentModel.DataAnnotations;

namespace Ressential.Models
{
    [MetadataType(typeof(UserMetadata))] // Link to the metadata class
    public partial class User
    { 
    }

    public class UserMetadata
    {
        [Required(ErrorMessage = "Username is required")]
        [StringLength(100, ErrorMessage = "Username cannot exceed 100 characters")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email format")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 255 characters")]
        public string Password { get; set; }
    }
}
