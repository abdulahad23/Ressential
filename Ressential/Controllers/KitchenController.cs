using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using Ressential.Models;
using Ressential.Utilities;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;

namespace Ressential.Controllers
{
    public class KitchenController : Controller
    {
        DB_RessentialEntities _db = new DB_RessentialEntities();
        public ActionResult Index() { 
            return View();
        }
        public ActionResult ItemList(string search)
        {
            var branchItems = _db.BranchItems.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                branchItems = branchItems.Where(c => c.Item.ItemName.Contains(search));
            }
            return View(branchItems.ToList());
        }
        public ActionResult CreateItem()
        {
            ViewBag.items = _db.Items.ToList();

            return View();
        }
        [HttpPost]
        public ActionResult CreateItem(BranchItem branchItems)
        {
            try
            {
                branchItems.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                branchItems.CreatedAt = DateTime.Now;
                branchItems.BranchId = 1;
                _db.BranchItems.Add(branchItems);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item created successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while creating the Item.";
                return RedirectToAction("Index", "Error");

            }
            ViewBag.items = _db.Items.ToList();

            return RedirectToAction("ItemList");
        }
        [HttpPost]
        public ActionResult DeleteItem(int BranchItemId)
        {
            try
            {
                var BranchItem = _db.BranchItems.Find(BranchItemId);
                if (BranchItem == null)
                {
                    TempData["ErrorMessage"] = "Item not found.";
                    return RedirectToAction("ItemList");
                }
                _db.BranchItems.Remove(BranchItem);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Item is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Item.";
                }
            }
            return RedirectToAction("ItemList");
        }
        [HttpPost]

        public ActionResult DeleteSelectedItems(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.BranchItems.Where(c => selectedItems.Contains(c.BranchItemId)).ToList();

                    _db.BranchItems.RemoveRange(itemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Items deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "An Item is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Item.";
                    }
                }
            }
            return RedirectToAction("ItemList");

        }
        public ActionResult EditItem(int BranchItemId)
        {
            var branchItems = _db.BranchItems.Find(BranchItemId);
            if (branchItems == null)
            {
                return HttpNotFound();
            }
            ViewBag.items = _db.Items.ToList();

            return View(branchItems);
        }

        [HttpPost]
        public ActionResult EditItem(BranchItem branchItem)
        {
            try
            {
                var existingItem = _db.BranchItems.Find(branchItem.BranchItemId);
                if (existingItem == null)
                {
                    return HttpNotFound();
                }
                branchItem.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                branchItem.ModifiedAt = DateTime.Now;
                _db.Entry(existingItem).CurrentValues.SetValues(branchItem);
                _db.Entry(existingItem).State = EntityState.Modified;
                _db.Entry(existingItem).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingItem).Property(x => x.CreatedAt).IsModified = false;
                _db.Entry(existingItem).Property(x => x.BranchId).IsModified = false;
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the Item.";
            }
            return RedirectToAction("ItemList");
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
        public ActionResult CreateCategory()
        {
            return View();
        }
        [HttpPost]
        public ActionResult CreateCategory(ProductCategory productCategory)
        {
            try
            {

                productCategory.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                productCategory.CreatedAt = DateTime.Now;
                _db.ProductCategories.Add(productCategory);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Product Category created successfully.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while creating the Product Category.";
            }
            return RedirectToAction("CategoryList");
        }

        public ActionResult CategoryList(string search)
        {
            var productCategory = _db.ProductCategories.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                productCategory = productCategory.Where(c => c.ProductCategoryName.Contains(search));
            }
            return View(productCategory.ToList());
        }

        public ActionResult EditCategory(int ProductCategoryId)
        {
            var ProductCategory = _db.ProductCategories.Find(ProductCategoryId);
            if (ProductCategory == null)
            {
                return HttpNotFound();
            }
            return View(ProductCategory);
        }
        [HttpPost]
        public ActionResult EditCategory(ProductCategory productCategory)
        {
            try
            {
                var existingItem = _db.ProductCategories.Find(productCategory.ProductCategoryId);
                if (existingItem == null)
                {
                    return HttpNotFound();
                }
                productCategory.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                productCategory.ModifiedAt = DateTime.Now;
                _db.Entry(existingItem).CurrentValues.SetValues(productCategory);
                _db.Entry(existingItem).State = EntityState.Modified;
                _db.Entry(existingItem).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingItem).Property(x => x.CreatedAt).IsModified = false;
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Product Category updated successfully.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the Product Category.";
            }

            return RedirectToAction("CategoryList");
        }



        [HttpPost]
        public ActionResult DeleteCategory(int ProductCategoryId)
        {
            try
            {
                var ProductCategory = _db.ProductCategories.Find(ProductCategoryId);
                if (ProductCategory == null)
                {
                    TempData["ErrorMessage"] = "Product Category not found.";
                    return RedirectToAction("CategoryList");
                }
                _db.ProductCategories.Remove(ProductCategory);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Product Category  deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Product Category  is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Product Category .";
                }
            }
            return RedirectToAction("CategoryList");
        }
        [HttpPost]

        public ActionResult DeleteSelectedCategory(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.ProductCategories.Where(c => selectedItems.Contains(c.ProductCategoryId)).ToList();

                    _db.ProductCategories.RemoveRange(itemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Product Category  deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "An Product Category  is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Product Category .";
                    }
                }
            }
            return RedirectToAction("CategoryList");

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

        [ChildActionOnly]
        public ActionResult BranchesDropdown()
        {
            var branches = _db.Branches.Where(b => b.IsActive == true).ToList();
            return PartialView("_BranchesDropdown", branches);
        }
    }
}