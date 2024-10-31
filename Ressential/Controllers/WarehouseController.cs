using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using Ressential.Models;
using Ressential.Utilities;

namespace Ressential.Controllers
{
    public class WarehouseController : Controller
    {
        DB_RessentialEntities _db = new DB_RessentialEntities();
        // GET: Warehouse
        public ActionResult Index()
        {
            string userName = Helper.GetUserInfo("userName");
            return View();
        }
        public ActionResult CreateItemCategory()
        {
            return View();
        }
        [HttpPost]
        public ActionResult CreateItemCategory(ItemCategory itemCategory)
        {
            _db.ItemCategories.Add(itemCategory);
            _db.SaveChanges();
            return RedirectToAction("ItemCategoryList");
        }
        public ActionResult ItemCategoryList(string search)
        {
            var itemCategories = _db.ItemCategories.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                itemCategories = itemCategories.Where(c => c.ItemCategoryName.Contains(search) || c.Description.Contains(search));
            }
            return View(itemCategories.ToList());
        }
        public ActionResult EditItemCategory(int itemCategoryId)
        {
            var ItemCategory = _db.ItemCategories.Find(itemCategoryId);
            if (ItemCategory == null)
            {
                return HttpNotFound();
            }
            return View(ItemCategory);
        }
        [HttpPost]
        public ActionResult EditItemCategory(ItemCategory itemCategory)
        {
            _db.Entry(itemCategory).State = EntityState.Modified;
            _db.SaveChanges();
            return RedirectToAction("ItemCategoryList");
        }
        [HttpPost]
        public ActionResult DeleteItemCategory(int itemCategoryId)
        {
            var itemCategory = _db.ItemCategories.Find(itemCategoryId);
            _db.ItemCategories.Remove(itemCategory);
            _db.SaveChanges();
            return RedirectToAction("ItemCategoryList");
        }
        [HttpPost]
        public ActionResult DeleteSelectedItemCategories(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                var itemsToDelete = _db.ItemCategories.Where(c => selectedItems.Contains(c.ItemCategoryId)).ToList();
                _db.ItemCategories.RemoveRange(itemsToDelete);
                _db.SaveChanges();
            }
            return RedirectToAction("ItemCategoryList");
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
        public ActionResult UserList(String search)
        {
            var users = _db.Users.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                users = users.Where(c => c.UserName.Contains(search));
            }
            return View(users.ToList());
        }
        public ActionResult CreateUser()
        {
            return View();
        }

        [HttpPost]
        public ActionResult CreateUser(User user, String ConfirmPassword)
        {
            if (user.Password != ConfirmPassword)
            {
                ModelState.AddModelError("", "Password and Confirm Password do not match.");
                return View(user);
            }
            _db.Users.Add(user);
            _db.SaveChanges();
            return RedirectToAction("UserList");
        }
        public ActionResult EditUser(int userId)
        {
            var User = _db.Users.Find(userId);
            if (User == null)
            {
                return HttpNotFound();
            }
            return View(User);
        }
        [HttpPost]
        public ActionResult EditUser(User user)
        {
            var existingUser = _db.Users.Find(user.UserId);
            if (existingUser == null)
            {
                return HttpNotFound();
            }
            user.Email = existingUser.Email;
            _db.Entry(existingUser).CurrentValues.SetValues(user);
            _db.Entry(existingUser).State = EntityState.Modified;
            _db.SaveChanges();
            return RedirectToAction("UserList");
        }
        [HttpPost]
        public ActionResult DeleteUser(int userId)
        {
            var user = _db.Users.Find(userId);
            _db.Users.Remove(user);
            _db.SaveChanges();
            return RedirectToAction("UserList");
        }
        [HttpPost]
        public ActionResult DeleteSelectedUsers(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                var usersToDelete = _db.Users.Where(c => selectedItems.Contains(c.UserId)).ToList();
                _db.Users.RemoveRange(usersToDelete);
                _db.SaveChanges();
            }
            return RedirectToAction("UserList");
        }
    }
}