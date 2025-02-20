using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Ressential.Utilities
{
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        private readonly string permission;

        public HasPermissionAttribute(string permission)
        {
            this.permission = permission;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (Helper.GetPermissions() == null)
                return false;

            var userPermissions = Helper.GetPermissions();
            return userPermissions.Contains(permission);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            filterContext.Result = new RedirectToRouteResult(
                new RouteValueDictionary(new { controller = "Account", action = "Unauthorized" }));
        }
    }
}