using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
    public class AccountController : Controller
    {
        DB_RessentialEntities _db = new DB_RessentialEntities();
        // GET: Account
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(string email, string password)
        {
            // Look for a user with matching email and password
            var user = _db.Users.FirstOrDefault(u => u.Email == email && u.Password == password);
            if (user != null && user.IsActive == true)
            {
                UserDetails userDetails = new UserDetails{
                    Email = user.Email,
                    UserId = user.UserId,
                    IsActive = user.IsActive,
                    UserName = user.UserName,
                };
                SetClaimsIdentity(userDetails);
                // Set user session on successful login
                Session["UserEmail"] = email;
                return RedirectToAction("Index", "Warehouse");  // Redirect to the dashboard or another protected area
            }
            else if(user != null && user.IsActive == false)
            {
                ModelState.AddModelError("", "Your account is deactivated. Please contact your administrator.");
            }
            else
            {
                ModelState.AddModelError("", "Invalid email or password.");
            }

            return View();
        }


        public void SetClaimsIdentity(UserDetails user)
        {
            var claims = new List<Claim>();
            try
            {
                // Setting    
                claims.Add(new Claim(ClaimTypes.Name, user.UserName));
                claims.Add(new Claim(ClaimTypes.Sid, user.UserId.ToString()));
                claims.Add(new Claim("IsActive", user.IsActive.ToString()));
                claims.Add(new Claim(ClaimTypes.Email, user.Email));


                var claimIdenties = new ClaimsIdentity(claims, DefaultAuthenticationTypes.ApplicationCookie);
                var ctx = Request.GetOwinContext();
                var authenticationManager = ctx.Authentication;
                // Sign In.    
                authenticationManager.SignIn(new AuthenticationProperties() { IsPersistent = true }, claimIdenties);


                var identity = new ClaimsIdentity(claims, DefaultAuthenticationTypes.ApplicationCookie);
                var claimsPrincipal = new ClaimsPrincipal(identity);
                Thread.CurrentPrincipal = claimsPrincipal;
            }
            catch (Exception ex)
            {
                // Info    
                throw ex;
            }


        }
        public ActionResult Logout()
        {
            var ctx = Request.GetOwinContext();
            var authenticationManager = ctx.Authentication;
            authenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            Session.Clear();
            return RedirectToAction("Login", "Account");
        }


    }
}