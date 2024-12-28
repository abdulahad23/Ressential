using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;
using Ressential.Models;
using Ressential.Utilities;
using System.Net;
using System.Net.Mail;

namespace Ressential.Controllers
{
    public class AccountController : Controller
    {
        DB_RessentialEntities _db = new DB_RessentialEntities();
        private static string otpCode;
        private static string emailAddress;
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
                    ProfileImage = user.ProfileImage,
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


        public void SetClaimsIdentity(UserDetails user, int? branchId = null)
        {
            var claims = new List<Claim>();
            try
            {
                var defaultBranchId = _db.Branches
            .Where(b => b.IsActive)
            .OrderBy(b => b.BranchId)
            .Select(b => b.BranchId)
            .FirstOrDefault();

                // Setting    
                claims.Add(new Claim(ClaimTypes.Name, user.UserName));
                claims.Add(new Claim(ClaimTypes.Sid, user.UserId.ToString()));
                claims.Add(new Claim("IsActive", user.IsActive.ToString()));
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
                claims.Add(new Claim("ProfileImage", user.ProfileImage));

                if (defaultBranchId != 0)
                {
                    claims.Add(new Claim("BranchId", defaultBranchId.ToString()));
                }

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
        public static void UpdateProfileImageClaim(HttpContextBase context, string profileImage)
        {
            try
            {
                var ctx = context.GetOwinContext();
                var authenticationManager = ctx.Authentication;

                // Get the current user's claims identity
                var identity = authenticationManager.User.Identity as ClaimsIdentity;

                if (identity == null)
                    throw new InvalidOperationException("No user is currently authenticated.");

                // Find the existing ProfileImage claim
                var existingClaim = identity.FindFirst("ProfileImage");

                if (existingClaim != null)
                {
                    // Remove the old claim
                    identity.RemoveClaim(existingClaim);
                }

                // Add the updated ProfileImage claim
                identity.AddClaim(new Claim("ProfileImage", profileImage));

                // Update the authentication cookie with the modified claims
                authenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                authenticationManager.SignIn(new AuthenticationProperties() { IsPersistent = true }, identity);
            }
            catch (Exception ex)
            {
                // Handle exception
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

        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Email is required.";
                return View();
            }

            // Generate OTP
            Random random = new Random();
            otpCode = random.Next(100000, 999999).ToString();
            emailAddress = email;

            // Send OTP to the user's email
            try
            {
                SendOtpEmail(email, otpCode);
                TempData["SuccessMessage"] = "OTP has been sent to your email.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error sending OTP: " + ex.Message;
                return View();
            }

            return RedirectToAction("VerifyOtp");
        }

        public ActionResult VerifyOtp()
        {
            return View();
        }

        [HttpPost]
        public ActionResult VerifyOtp(string otp)
        {
            if (otp == otpCode)
            {
                TempData["SuccessMessage"] = "OTP verified. You can now reset your password.";
                return RedirectToAction("ResetPassword");
            }

            TempData["ErrorMessage"] = "Invalid OTP. Please try again.";
            return View();
        }

        public ActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ResetPassword(string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["ErrorMessage"] = "Passwords do not match.";
                return View();
            }

            var user = _db.Users.Where(c => c.Email == emailAddress).FirstOrDefault();
            user.Password = newPassword;
            _db.SaveChanges();
            TempData["SuccessMessage"] = "Password has been reset successfully.";
            return RedirectToAction("Login");
        }

        private void SendOtpEmail(string email, string otp)
        {
            var fromAddress = new MailAddress("myressential@gmail.com", "Restaurant Portal");
            var toAddress = new MailAddress(email);
            const string fromPassword = "fgio azrf ibzt ccly"; // Gmail app password or generated token
            const string subject = "Your OTP for Password Reset";
            string body = $"Your OTP for password reset is: {otp}. This OTP is valid for 10 minutes.";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body
            })
            {
                smtp.Send(message);
            }
        }
    }
}