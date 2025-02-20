using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Utilities
{
    public class UserDetails
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string ProfileImage { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }

        public int BranchId { get; set; }

        public List<string> Permissions { get; set; }
    }
}