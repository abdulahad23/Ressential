using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Ressential.Controllers
{
    public class AccountController : Controller
    {
        db_RessentialEntities1 _db = new db_RessentialEntities1();
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
            if (user != null)
            {
                // Set user session on successful login
                Session["UserEmail"] = email;
                return RedirectToAction("Index","Warehouse");  // Redirect to the dashboard or another protected area
            }
            else
            {
                ModelState.AddModelError("", "Invalid email or password.");
            }

            return View();
        }
    }
}