using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace Ressential.Models
{
    [MetadataType(typeof(RoleMetadata))] // Link to the metadata class

    public partial class Role
    {
    }

    public class RoleMetadata
    {
        [Required(ErrorMessage = "Role name is required.")]
        public string RoleName { get; set; }
    }

}