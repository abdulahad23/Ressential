using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;
using Ressential.Models;
using Ressential.Utilities;

namespace Ressential.Controllers
{
    public class PermissionsController : Controller
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            UpdateUserPermissions();
        }

        private void UpdateUserPermissions()
        {
            if (User.Identity.IsAuthenticated)
            {
                var userId = Helper.GetUserInfo("userId");
                if (int.TryParse(userId, out int parsedUserId))
                {
                    var bra = Helper.GetUserInfo("branchId");
                    using (var _db = new DB_RessentialEntities())
                    {
                        var user = _db.Users.FirstOrDefault(u => u.UserId == parsedUserId && u.IsActive);
                        if (user != null)
                        {
                            var permissions = user.Role.RolePermissions
                            .Select(p => p.Permission.PermissionsCategory.PermissionCategoryName + " " + p.Permission.PermissionName)
                            .Distinct()
                            .ToList();

                            var branchPermissions = _db.UserBranchPermissions
                               .Where(ubp => ubp.UserId == parsedUserId)
                               .Select(ubp => ubp.BranchId)
                               .ToList();

                            if (user.HasWarehousePermission)
                            {
                                branchPermissions.Add(0); // Add 0 for warehouse permission
                            }


                            var ctx = HttpContext.GetOwinContext();
                            var authenticationManager = ctx.Authentication;
                            var identity = User.Identity as ClaimsIdentity;

                            if (identity != null)
                            {
                                // Remove old permissions claims
                                var existingClaims = identity.FindAll("Permissions").ToList();
                                foreach (var claim in existingClaims)
                                {
                                    identity.RemoveClaim(claim);
                                }

                                // Remove old branch permissions claims
                                var existingBranchPermissionsClaims = identity.FindAll("BranchPermissions").ToList();
                                foreach (var claim in existingBranchPermissionsClaims)
                                {
                                    identity.RemoveClaim(claim);
                                }

                                // Add updated permissions claims
                                identity.AddClaim(new Claim("Permissions", string.Join(",", permissions)));

                                // Add updated branch permissions claims
                                identity.AddClaim(new Claim("BranchPermissions", string.Join(",", branchPermissions)));


                                // Refresh authentication cookie
                                authenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                                authenticationManager.SignIn(new AuthenticationProperties() { IsPersistent = true }, identity);
                            }
                        }
                    }
                }
            }
        }
    }
}
