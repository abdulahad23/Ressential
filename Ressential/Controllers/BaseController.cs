using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Ressential.Controllers
{
    public class BaseController : Controller
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Get the current action and controller
            var actionName = filterContext.ActionDescriptor.ActionName;
            var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;

            // Define public actions that don't require authentication
            var publicActions = new List<(string Controller, string Action)>
        {
            ("Customer", "Login"),
            ("Customer", "Register"),
            ("Customer", "Logout"),
            ("Customer", "ForgotPassword"),
            ("Customer", "Shop"),
            ("Customer", "GetBranches"),
            ("Customer", "SetBranch"),
            ("Customer", "ResetPassword"),
            ("Customer", "VerifyOtp"),
            ("Customer", "VerifyRegistrationOtp")
        };

            // Check if the current action is public
            if (!publicActions.Contains((controllerName, actionName)) &&
                (Session["IsAuthenticated"] == null || !(bool)Session["IsAuthenticated"]))
            {
                filterContext.Result = RedirectToAction("Login", "Customer");
            }

            base.OnActionExecuting(filterContext);
        }
    }
}