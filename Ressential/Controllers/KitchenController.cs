using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using Ressential.Models;

namespace Ressential.Controllers
{
    public class KitchenController : Controller
    {
        DB_RessentialEntities _db = new DB_RessentialEntities();
        public ActionResult Index() { 
            return View();
        }
        public ActionResult ItemList()
        {
            return View();
        }
        public ActionResult CreateItem()
        {
            return View();
        }
        public ActionResult RequisitionList()
        {
            return View();
        }
        public ActionResult CreateRequisition()
        {
            return View();
        }
        public ActionResult ReceiveStockList()
        {
            return View();
        }

        public ActionResult StockReturnList()
        {
            return View();
        }
        public ActionResult CreateStockReturn()
        {
            return View();
        }
        public ActionResult ConsumeList()
        {
            return View();
        }
        public ActionResult CreateConsume()
        {
            return View();
        }
        public ActionResult WastageList()
        {
            return View();
        }
        public ActionResult CreateWastage()
        {
            return View();
        }
        public ActionResult CategoryList()
        {
            return View();
        }
        public ActionResult CreateCategory()
        {
            return View();
        }
        public ActionResult ProductList()
        {
            return View();
        }
        public ActionResult CreateProduct()
        {
            return View();
        }
        public ActionResult OrderList()
        {
            return View();
        }
        public ActionResult CreateOrder()
        {
            return View();
        }
        public ActionResult OrderReturnList()
        {
            return View();
        }
        public ActionResult CreateOrderReturn()
        {
            return View();
        }
        public ActionResult OrderView()
        {
            return View();
        }
        public ActionResult ChefView()
        {
            return View();
        }

        public ActionResult UserList()
        {
            var users = _db.Users.Include(u => u.Roles).ToList();
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