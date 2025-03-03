using Ressential.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Ressential.Utilities
{
    public class HasBranchAccessAttribute : AuthorizeAttribute
    {
        private int branchId;

        public HasBranchAccessAttribute()
        {
            this.branchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            branchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            return Helper.HasBranchAccess(branchId);
        }
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            filterContext.Result = new RedirectToRouteResult(
                new RouteValueDictionary(new { controller = "Account", action = "Unauthorized" }));
        }
    }


    public class HasWarehouseAccessAttribute : AuthorizeAttribute
    {
        public HasWarehouseAccessAttribute()
        {
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (Helper.HasBranchAccess(0))
            {
                return true;
            }
            return false; 
        }
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            filterContext.Result = new RedirectToRouteResult(
                new RouteValueDictionary(new { controller = "Account", action = "Unauthorized" }));
        }
    }
}