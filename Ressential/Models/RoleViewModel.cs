using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public class RoleViewModel
    {
        public Role Role { get; set; }
        public List<PermissionsCategory> PermissionsCategories { get; set; }
        public List<int> SelectedPermissions { get; set; }
    }
}