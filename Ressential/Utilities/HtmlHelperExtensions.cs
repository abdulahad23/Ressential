using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Ressential.Utilities
{
    public static class HtmlHelperExtensions
    {
        public static bool HasPermission(this HtmlHelper htmlHelper, string permission)
        {
            var permissions = Helper.GetPermissions();
            return permissions != null && permissions.Contains(permission);
        }
    }
}