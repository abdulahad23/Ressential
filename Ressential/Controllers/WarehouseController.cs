using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;

namespace Ressential.Controllers
{
    public class WarehouseController : Controller
    {
        db_RessentialEntities1 _db = new db_RessentialEntities1();
        // GET: Warehouse
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult CreateItem()
        {
            return View();
        }
        public ActionResult ItemList()
        {
            return View();
        }
        public ActionResult CreateVendor()
        {
            return View();
        }
        public ActionResult VendorList()
        {
            return View();
        }
        public ActionResult CreateBranch()
        {
            return View();
        }
        public ActionResult BranchList()
        {
            return View();
        }
        public ActionResult CreatePurchase()
        {
            return View();
        }
        public ActionResult CreatePurchaseReturn()
        {
            return View();
        }
        public ActionResult PurchaseList()
        {
            return View();
        }
      
        public ActionResult PurchaseReturnList()
        {
            return View();
        }
        public ActionResult CreateIssue()
        {
            return View();
        }
        public ActionResult IssueList()
        {
            return View();
        }
        public ActionResult CreateBankAndCash()
        {
            return View();
        }
        public ActionResult BankAndCashList()
        {
            return View();
        }
        public ActionResult CreatePaymentVouncher()
        {
            return View();
        }
        public ActionResult PaymentVouncherList()
        {
            return View();
        }
        public ActionResult CreateReceiptVouncher()
        {
            return View();
        }
        public ActionResult ReceiptVouncherList()
        {
            return View();
        }
        public ActionResult RequisitionList()
        {
            return View();
        }
        public ActionResult StockReturnList()
        {
            return View();
        }
        public ActionResult UserList()
        {
            var users = _db.Users.Include(u => u.Role).ToList();
            return View(users);
        }
        public ActionResult CreateUser()
        {
            ViewBag.Roles = new SelectList(_db.Roles.ToList(), "RoleID", "RoleName");
            return View();
        }

        [HttpPost]
        public ActionResult CreateUser(User user, String ConfirmPassword)
        {
            if (user.Password != ConfirmPassword)
            {
                ModelState.AddModelError("", "Password and Confirm Password do not match.");
                ViewBag.Roles = new SelectList(_db.Roles.ToList(), "RoleID", "RoleName");
                return View(user);
            }

            _db.Users.Add(user);
            _db.SaveChanges();
            return RedirectToAction("Index");

            //if (ModelState.IsValid)
            //{
                
            //}

            //ViewBag.Roles = new SelectList(_db.Roles.ToList(), "RoleID", "RoleName");
            //return View(user);
        }
    }
}