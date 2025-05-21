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
using Microsoft.Owin.Security;
using System.Security.Claims;
using System.Data.Entity.Migrations;
using System.Data.Entity.Validation;
using System.IO;
using Microsoft.AspNet.Identity;
using System.Net.Security;
using Ressential.Hub;
using Microsoft.AspNet.SignalR;
using OfficeOpenXml;

namespace Ressential.Controllers
{
    [System.Web.Mvc.Authorize]
    [HasBranchAccess]
    public class KitchenController : PermissionsController
    {
        DB_RessentialEntities _db = new DB_RessentialEntities();
        public ActionResult Index() { 
            return View();
        }

public ActionResult GetUnreadNotifications()
        {
            int currentUserId = Convert.ToInt32(Helper.GetUserInfo("userId"));
            var notifications = _db.Notifications
                .Where(n => n.UserId == currentUserId && !n.IsRead)
                .ToList();

            return PartialView("_NotificationsPartial", notifications);
        }

        [HttpPost]
        public JsonResult MarkAsRead(int notificationId)
        {
            var notification = _db.Notifications.SingleOrDefault(n => n.NotificationId == notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                _db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        public JsonResult GetUnreadNotificationCount()
        {
            int currentUserId = Convert.ToInt32(Helper.GetUserInfo("userId"));
            var count = _db.Notifications
                .Count(n => n.UserId == currentUserId && !n.IsRead);

            return Json(new { count }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult MarkAllAsRead()
        {
            int currentUserId = Convert.ToInt32(Helper.GetUserInfo("userId"));
            var notifications = _db.Notifications
                .Where(n => n.UserId == currentUserId && !n.IsRead)
                .ToList();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            _db.SaveChanges();
            return Json(new { success = true });
        }

        [HasPermission("Branch Item List")]
        public ActionResult ItemList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var branchItems = _db.BranchItems.AsQueryable().Where(c => c.BranchId == selectedBranchId);
            
            if (!string.IsNullOrEmpty(search))
            {
                branchItems = branchItems.Where(c => c.Item.ItemName.Contains(search) && c.BranchId == selectedBranchId);
            }
            return View(branchItems.ToList());
        }
        [HasPermission("Branch Item Create")]
        public ActionResult CreateItem()
        {
            BranchItem branchItem = new BranchItem {
                OpeningStockDate = DateTime.Today,
                IsActive = true,
            };
            ViewBag.items = _db.Items.ToList();

            return View(branchItem);
        }
        [HttpPost]
        [HasPermission("Branch Item Create")]
        public ActionResult CreateItem(BranchItem branchItems)
        {
            try
            {
                branchItems.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                branchItems.CreatedAt = DateTime.Now;
                branchItems.BranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                branchItems.Quantity = branchItems.OpeningStockQuantity;
                if (branchItems.Quantity == 0)
                {
                    branchItems.CostPerUnit = 0;
                }
                else
                {
                    branchItems.CostPerUnit = branchItems.OpeningStockValue / branchItems.OpeningStockQuantity;
                }
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
        [HasPermission("Branch Item Delete")]
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

        [HasPermission("Branch Item Delete")]
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
        [HasPermission("Branch Item Edit")]
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
        [HasPermission("Branch Item Edit")]
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
                _db.Entry(existingItem).Property(x => x.OpeningStockQuantity).IsModified = false;
                _db.Entry(existingItem).Property(x => x.OpeningStockValue).IsModified = false;
                _db.Entry(existingItem).Property(x => x.OpeningStockDate).IsModified = false;
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Item updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the Item.";
            }
            return RedirectToAction("ItemList");
        }
        [HasPermission("Branch Requisition List")]
        public ActionResult RequisitionList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var requisition = _db.Requisitions.Where(c => c.BranchId == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {
                requisition = requisition.Where(c => (c.RequisitionDetails.Where(i => i.Item.ItemName.Contains(search)).Count()>0 || c.RequisitionNo.Contains(search) || c.Description.Contains(search)) && c.BranchId == selectedBranchId);
            }
            return View(requisition.ToList());

        }
        [HasPermission("Branch Requisition Create")]
        public ActionResult CreateRequisition()
        {
            var requisition = new Requisition
            {
                RequisitionDetails = new List<RequisitionDetail>
                {
                    new RequisitionDetail()
                }

            };
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();

            return View(requisition);
        }
        [HttpPost]
        [HasPermission("Branch Requisition Create")]
        public ActionResult CreateRequisition(Requisition requisition)
        {
            try
            {
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                if (ModelState.IsValid)
                {
                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextRequisitionNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.Requisitions.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextRequisitionNumber = _db.Requisitions
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.RequisitionNo.Substring(11)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    requisition.RequisitionNo = $"REQ-{datePart}{nextRequisitionNumber:D4}";
                    requisition.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    requisition.CreatedAt = DateTime.Now;
                    requisition.BranchId = selectedBranchId;
                    requisition.Status = "Pending";
                    _db.Requisitions.Add(requisition);

                    var notifications = new List<Notification>();
                    var branch = _db.Branches.Find(selectedBranchId);
                    Notification notification = new Notification
                    {
                        DateTime = DateTime.Now,
                        Title = "Received Requisition",
                        Message = "Received requisition from " + branch.BranchName + " branch.",
                        RedirectUrl = "/Warehouse/RequisitionList?search=" + requisition.RequisitionNo,
                        Type = "Requisition Alert",
                        IsRead = false,
                        BranchId = 0
                    };
                    var users = _db.Users.Where(u => u.HasWarehousePermission);
                    foreach (var user in users)
                    {
                        var userNotification = new Notification
                        {
                            DateTime = notification.DateTime,
                            Title = notification.Title,
                            Message = notification.Message,
                            RedirectUrl = notification.RedirectUrl,
                            Type = notification.Type,
                            IsRead = notification.IsRead,
                            BranchId = notification.BranchId,
                            UserId = user.UserId
                        };
                        notifications.Add(userNotification);
                    }

                    _db.Notifications.AddRange(notifications);
                    _db.SaveChanges();

                    //foreach (var requisitionDetails in requisition.RequisitionDetails)
                    //{
                    //    var currentItemStock = _db.WarehouseItemStocks.Where(i => i.ItemId == purchaseDetails.ItemId).FirstOrDefault();
                    //    decimal currentQuantity = currentItemStock.Quantity;
                    //    currentItemStock.Quantity = currentQuantity + purchaseDetails.Quantity;
                    //    currentItemStock.CostPerUnit = ((currentQuantity * currentItemStock.CostPerUnit) + (purchaseDetails.Quantity * purchaseDetails.UnitPrice)) / (currentItemStock.Quantity);

                    //    var warehouseItemTransaction = new WarehouseItemTransaction
                    //    {
                    //        TransactionDate = purchase.PurchaseDate,
                    //        ItemId = purchaseDetails.ItemId,
                    //        TransactionType = "Purchase",
                    //        TransactionTypeId = purchase.PurchaseId,
                    //        Quantity = purchaseDetails.Quantity,
                    //        CostPerUnit = purchaseDetails.UnitPrice
                    //    };
                    //    _db.WarehouseItemStocks.AddOrUpdate(currentItemStock);
                    //    _db.WarehouseItemTransactions.Add(warehouseItemTransaction);
                    //}
                    //_db.SaveChanges();

                    return Json("0", JsonRequestBehavior.AllowGet);
                }
                
                ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
                return View(requisition);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error creating purchase: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
        [HasPermission("Branch Requisition Edit")]
        public ActionResult EditRequisition(int requisitionId)
        {
            Requisition requisition = _db.Requisitions.Find(requisitionId);
            if (requisition.Status == "Rejected")
            {
                TempData["ErrorMessage"] = "The requisition cannot be updated due to 'Rejected' status";
                return RedirectToAction("RequisitionList");
            }
            else if (requisition.Status == "Settled")
            {
                TempData["ErrorMessage"] = "The requisition cannot be updated due to 'Settled' status";
                return RedirectToAction("RequisitionList");
            }
            else if (requisition.Status == "Partially Settled")
            {
                TempData["ErrorMessage"] = "The requisition cannot be updated due to 'Partially Settled' status";
                return RedirectToAction("RequisitionList");
            }
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
            return View(requisition);
        }
        [HttpPost]
        [HasPermission("Branch Requisition Edit")]
        public ActionResult EditRequisition(Requisition requisition)
        {
            if (requisition == null)
            {
                foreach (var state in ModelState)
                {
                    var key = state.Key; // Property name
                    var errors = state.Value.Errors; // List of errors for this property

                    foreach (var error in errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"Key: {key}, Error: {error.ErrorMessage}");
                    }
                }

                return Json(new { status = "error", message = "Invalid data provided" }, JsonRequestBehavior.AllowGet);
            }

            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var existingRequisition = _db.Requisitions.Include(p => p.RequisitionDetails).FirstOrDefault(p => p.RequisitionId == requisition.RequisitionId);
                    if (existingRequisition == null)
                    {
                        return Json(new { status = "error", message = "Purchase not found" }, JsonRequestBehavior.AllowGet);
                    }

                    // Update purchase metadata
                    requisition.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    requisition.ModifiedAt = DateTime.Now;

                    // Update existing purchase values
                    _db.Entry(existingRequisition).CurrentValues.SetValues(requisition);
                    _db.Entry(existingRequisition).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingRequisition).Property(x => x.CreatedAt).IsModified = false;
                    _db.Entry(existingRequisition).Property(x => x.BranchId).IsModified = false;
                    _db.Entry(existingRequisition).Property(x => x.Status).IsModified = false;

                    // Update PurchaseDetails
                    var existingDetails = existingRequisition.RequisitionDetails.ToList();
                    var newDetails = requisition.RequisitionDetails;

                    // Update or Add new details
                    foreach (var newDetail in newDetails)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(d => d.RequisitionDetailId == newDetail.RequisitionDetailId);
                        //var warehouseItemStock = _db.WarehouseItemStocks.Find(newDetail.ItemId);
                        if (existingDetail != null)
                        {
                            //decimal RevertedQuantity = warehouseItemStock.Quantity - existingDetail.Quantity;
                            
                            //warehouseItemStock.Quantity = RevertedQuantity + newDetail.Quantity; //Updated Quantity
                            //if (warehouseItemStock.Quantity == 0)
                            //{
                            //    warehouseItemStock.CostPerUnit = 0;
                            //}
                            //else
                            //{
                            //    warehouseItemStock.CostPerUnit = ((warehouseItemStock.Quantity * warehouseItemStock.CostPerUnit) - (existingDetail.Quantity * existingDetail.UnitPrice) + (newDetail.Quantity * newDetail.UnitPrice)) / warehouseItemStock.Quantity; //Updated Per Unit Cost
                            //}

                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            //decimal oldQuantity = warehouseItemStock.Quantity;
                            //warehouseItemStock.Quantity = warehouseItemStock.Quantity + newDetail.Quantity; //Updated Quantity
                            //if (warehouseItemStock.Quantity == 0)
                            //{
                            //    warehouseItemStock.CostPerUnit = 0;
                            //}
                            //else
                            //{
                            //    warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) + (newDetail.Quantity * newDetail.UnitPrice)) / warehouseItemStock.Quantity; //Updated Per Unit Cost
                            //}
                            existingRequisition.RequisitionDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {
                        //foreach (var item in existingDetails)
                        //{
                            //var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                            //decimal oldQuantity = warehouseItemStock.Quantity;
                            //warehouseItemStock.Quantity = warehouseItemStock.Quantity - item.Quantity;
                            //if (warehouseItemStock.Quantity == 0)
                            //{
                            //    warehouseItemStock.CostPerUnit = 0;
                            //}
                            //else
                            //{
                            //    warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) - (item.Quantity * item.UnitPrice)) / (warehouseItemStock.Quantity == 0 ? 1 : warehouseItemStock.Quantity);
                            //}
                        //}
                        _db.RequisitionDetails.RemoveRange(existingDetails);
                    }


                    // Save changes
                    _db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Item updated successfully.";
                    return Json(new { status = "success" }, JsonRequestBehavior.AllowGet);
                }
                catch (DbEntityValidationException ex)
                {
                    transaction.Rollback();

                    // Log the validation errors for debugging
                    foreach (var validationError in ex.EntityValidationErrors)
                    {
                        foreach (var error in validationError.ValidationErrors)
                        {
                            // Log property name and error message
                            System.Diagnostics.Debug.WriteLine($"Property: {error.PropertyName}, Error: {error.ErrorMessage}");
                        }
                    }

                    TempData["ErrorMessage"] = "An error occurred while updating the Item.";
                    return Json(new { status = "error", message = "Validation error occurred. Check logs for details." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        [HttpPost]
        [HasPermission("Branch Requisition Delete")]
        public ActionResult DeleteRequisition(int requisitionId)
        {
            try
            {
                var requisition = _db.Requisitions.Find(requisitionId);
                if (requisition == null)
                {
                    TempData["ErrorMessage"] = "Requisition not found.";
                    return RedirectToAction("RequisitionList");
                }
                else if (requisition.Status == "Rejected")
                {
                    TempData["ErrorMessage"] = "The requisition cannot be deleted due to 'Rejected' status";
                    return RedirectToAction("RequisitionList");
                }
                else if (requisition.Status == "Settled")
                {
                    TempData["ErrorMessage"] = "The requisition cannot be deleted due to 'Settled' status";
                    return RedirectToAction("RequisitionList");
                }
                else if (requisition.Status == "Partially Settled")
                {
                    TempData["ErrorMessage"] = "The requisition cannot be deleted due to 'Partially Settled' status";
                    return RedirectToAction("RequisitionList");
                }
                var requisitionDetails = _db.RequisitionDetails.Select(p => p).Where(p => p.RequisitionId == requisitionId);

                //if (requisitionDetails != null)
                //{
                //    foreach (var item in requisitionDetails)
                //    {
                //        var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                //        decimal oldQuantity = warehouseItemStock.Quantity;
                //        warehouseItemStock.Quantity = warehouseItemStock.Quantity - item.Quantity;
                //        if (warehouseItemStock.Quantity == 0)
                //        {
                //            warehouseItemStock.CostPerUnit = 0;
                //        }
                //        else
                //        {
                //            warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) - (item.Quantity * item.UnitPrice)) / warehouseItemStock.Quantity;
                //        }
                //    }
                //}
                _db.RequisitionDetails.RemoveRange(requisitionDetails);
                _db.Requisitions.Remove(requisition);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Requisition deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This requisition is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the requisition.";
                }
            }
            return RedirectToAction("RequisitionList");
        }
        [HttpPost]
        [HasPermission("Branch Requisition Delete")]
        public ActionResult DeleteSelectedRequisitions(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var requisitionsToDelete = _db.Requisitions.Where(c => selectedItems.Contains(c.RequisitionId)).ToList();
                    var requisitionDetails = _db.RequisitionDetails.Where(c => selectedItems.Contains(c.RequisitionId)).ToList();

                    if (requisitionsToDelete.Where(c => c.Status == "Rejected" || c.Status == "Settled" || c.Status == "Partially Settled").Count()>0)
                    {
                        TempData["ErrorMessage"] = "The requisitions cannot be deleted due to one or more requisition have not pending status";
                        return RedirectToAction("RequisitionList");
                    }

                    //foreach (var item in requisitionsToDelete)
                    //{
                    //    var warehouseItemTransaction = _db.WarehouseItemTransactions.Where(w => w.TransactionType == "Requisition" && w.TransactionTypeId == item.RequisitionId);
                    //    _db.WarehouseItemTransactions.RemoveRange(warehouseItemTransaction);
                    //}

                    //if (requisitionDetails != null)
                    //{
                    //    foreach (var item in requisitionDetails)
                    //    {
                    //        var warehouseItemStock = _db.WarehouseItemStocks.Find(item.ItemId);
                    //        decimal oldQuantity = warehouseItemStock.Quantity;
                    //        warehouseItemStock.Quantity = warehouseItemStock.Quantity - item.Quantity;
                    //        if (warehouseItemStock.Quantity == 0)
                    //        {
                    //            warehouseItemStock.CostPerUnit = 0;
                    //        }
                    //        else
                    //        {
                    //            warehouseItemStock.CostPerUnit = ((oldQuantity * warehouseItemStock.CostPerUnit) - (item.Quantity * item.UnitPrice)) / warehouseItemStock.Quantity;
                    //        }
                    //    }
                    //}

                    _db.RequisitionDetails.RemoveRange(requisitionDetails);
                    _db.Requisitions.RemoveRange(requisitionsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Requisitions deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A requisition is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the requisition.";
                    }
                }
            }
            return RedirectToAction("RequisitionList");
        }
        [HasPermission("Branch Issue List")]
        public ActionResult ReceiveStockList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var warehouseIssues = _db.WarehouseIssues.Where(w => w.BranchID == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {
                warehouseIssues = warehouseIssues.Where(w => w.IssueNo.Contains(search) || w.Requisition.RequisitionNo.Contains(search) || w.Memo.Contains(search) || w.Status.Contains(search));
            }
            return View(warehouseIssues);
        }
        [HasPermission("Branch Issue View")]
        public ActionResult ViewIssue(int issueId)
        {
            var issue = _db.WarehouseIssues.Find(issueId);

            if (issue == null)
            {
                TempData["ErrorMessage"] = "Issue not found.";
                return RedirectToAction("ReceiveStockList");
            }

            return View(issue);
        }

        [HttpPost]
        [HasPermission("Branch Issue Status Update")]
        public ActionResult ReceiveIssue(int IssueId)
        {
            try
            {
                var warehouseIssues = _db.WarehouseIssues.Find(IssueId);
                if (warehouseIssues.Status == "Settled")
                {
                    TempData["ErrorMessage"] = "The issue is already received";
                    return RedirectToAction("ReceiveStockList");
                }

                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                foreach (var item in warehouseIssues.WarehouseIssueDetails)
                {
                    var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();

                    if (branchItem != null)
                    {
                        var currentTotalCost = branchItem.Quantity * branchItem.CostPerUnit;
                        var newTotalCost = item.Quantity * item.CostApplied;

                        branchItem.Quantity += item.Quantity;
                        branchItem.CostPerUnit = (currentTotalCost + newTotalCost) / branchItem.Quantity;

                        // Mark the branchItem as modified
                        _db.Entry(branchItem).State = EntityState.Modified;
                    }
                }

                warehouseIssues.Status = "Settled";

                // Mark warehouseIssues as modified if needed
                _db.Entry(warehouseIssues).State = EntityState.Modified;

                _db.SaveChanges();

                TempData["SuccessMessage"] = "Stock received successfully.";
                return RedirectToAction("ReceiveStockList");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while receiving the stock.";
                return RedirectToAction("ReceiveStockList");
            }
        }

        [HttpPost]
        [HasPermission("Branch Issue Status Update")]
        public ActionResult ReceiveSelectedIssues(int[] selectedItems)
        {
            try
            {
                var issuesToReceive = _db.WarehouseIssues
                                         .Where(c => selectedItems.Contains(c.IssueId))
                                         .ToList();

                var receivedIssues = issuesToReceive.Where(i => i.Status == "Settled");
                if (receivedIssues.Count() > 0)
                {
                    TempData["ErrorMessage"] = "An issue exist which is already received";
                    return RedirectToAction("ReceiveStockList");
                }
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));

                foreach (var warehouseIssue in issuesToReceive)
                {
                    foreach (var item in warehouseIssue.WarehouseIssueDetails)
                    {
                        var branchItem = _db.BranchItems.FirstOrDefault(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId);

                        if (branchItem != null)
                        {
                            var currentTotalCost = branchItem.Quantity * branchItem.CostPerUnit;
                            var newTotalCost = item.Quantity * item.CostApplied;

                            branchItem.Quantity += item.Quantity;
                            branchItem.CostPerUnit = (currentTotalCost + newTotalCost) / branchItem.Quantity;

                            // Mark the branchItem as modified
                            _db.Entry(branchItem).State = EntityState.Modified;
                        }
                    }

                    warehouseIssue.Status = "Settled";

                    // Mark warehouseIssue as modified
                    _db.Entry(warehouseIssue).State = EntityState.Modified;
                }

                _db.SaveChanges();

                TempData["SuccessMessage"] = "Stock received successfully.";
                return RedirectToAction("ReceiveStockList");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while receiving the stock.";
                return RedirectToAction("ReceiveStockList");
            }
        }

        [HasPermission("Branch Stock Return Create")]
        public ActionResult CreateStockReturn()
        {
            var returnStock = new ReturnStock
            {
                ReturnStockDetails = new List<ReturnStockDetail>
                {
                    new ReturnStockDetail()
                }

            };
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();

            return View(returnStock);
        }
        [HttpPost]
        [HasPermission("Branch Stock Return Create")]
        public ActionResult CreateStockReturn(ReturnStock returnStock)
        {
            try
            {
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                if (ModelState.IsValid)
                {
                    foreach (var item in returnStock.ReturnStockDetails)
                    {
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();
                        if (item.ItemQuantity > branchItem.Quantity)
                        {
                            return Json(new { success = false, errorMessage = $"Insufficient stock for {item.Item.ItemName}. The available quantity is {branchItem.Quantity}" });
                        }

                        item.CostPerUnit = branchItem.CostPerUnit;
                        branchItem.Quantity = branchItem.Quantity - item.ItemQuantity;
                    }

                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextReturnStockNumber = 1;

                    // Check if there are any existing return stock record first
                    if (_db.ReturnStocks.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextReturnStockNumber = _db.ReturnStocks
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.ReturnNo.Substring(11)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    returnStock.ReturnNo = $"RET-{datePart}{nextReturnStockNumber:D4}";
                    returnStock.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    returnStock.CreatedAt = DateTime.Now;
                    returnStock.BranchId = selectedBranchId;
                    returnStock.Status = "Pending";
                    _db.ReturnStocks.Add(returnStock);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Stock return record created successfully!";
                    return Json(new { success = true });
                }

                return Json(new { success = false, redirect = Url.Action("Index", "Error") });
            }
            catch (Exception)
            {
                // Log the exception if needed
                TempData["ErrorMessage"] = "An error occurred while creating the return stock.";
                return Json(new { success = false, redirect = Url.Action("StockReturnList", "Kitchen") });
            }
        }
        [HasPermission("Branch Stock Return List")]
        public ActionResult StockReturnList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var returnStock = _db.ReturnStocks.Where(c => c.BranchId == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {
                returnStock = returnStock.Where(c => (c.ReturnStockDetails.Where(i => i.Item.ItemName.Contains(search)).Count() > 0 || c.ReturnNo.Contains(search) || c.Description.Contains(search)) && c.BranchId == selectedBranchId);
            }
            return View(returnStock.ToList());
        }
        [HttpPost]

        [HasPermission("Branch Stock Return Delete")]
        public ActionResult DeleteReturnStock(int ReturnStockId)
        {
            try
            {
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                var returnStock = _db.ReturnStocks.Find(ReturnStockId);
                if (returnStock == null)
                {
                    TempData["ErrorMessage"] = "Return stock not found.";
                    return RedirectToAction("StockReturnList");
                }
                var returnStockDetails = _db.ReturnStockDetails.Select(p => p).Where(p => p.ReturnStockId == ReturnStockId);

                if (returnStockDetails != null)
                {
                    foreach (var item in returnStockDetails)
                    {
                        if (item.ReturnStock.Status == "Settled")
                        {
                            TempData["ErrorMessage"] = "Cannot delete settled stock return record.";
                            return RedirectToAction("StockReturnList");
                        }
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();
                        decimal currentQuantity = branchItem.Quantity;
                        branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                        if (branchItem.Quantity == 0)
                        {
                            branchItem.CostPerUnit = 0;
                        }
                        else if (branchItem.Quantity < 0)
                        {
                            TempData["ErrorMessage"] = "Unable to delete the stock return. Quantity of " + branchItem.Item.ItemName + " cannot be < 0.";
                            return RedirectToAction("StockReturnList");
                        }
                        else
                        {
                            branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                        }
                    }
                }

                _db.ReturnStockDetails.RemoveRange(returnStockDetails);
                _db.ReturnStocks.Remove(returnStock);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Return stock record deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Return Stock is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Return Stock.";
                }
            }
            return RedirectToAction("StockReturnList");

        }
        [HttpPost]
        [HasPermission("Branch Stock Return Delete")]
        public ActionResult DeleteSelectedStockReturns(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                    var returnStocksToDelete = _db.ReturnStocks.Where(c => selectedItems.Contains(c.ReturnStockId)).ToList();
                    var returnStockDetails = _db.ReturnStockDetails.Where(c => selectedItems.Contains(c.ReturnStockId)).ToList();

                    if (returnStockDetails != null)
                    {
                        foreach (var item in returnStockDetails)
                        {
                            if (item.ReturnStock.Status == "Settled")
                            {
                                TempData["ErrorMessage"] = "Cannot delete settled stock return record.";
                                return RedirectToAction("StockReturnList");
                            }
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to delete the stock return "+ item.ReturnStock.ReturnNo +". Quantity of " + branchItem.Item.ItemName + " cannot be < 0.";
                                return RedirectToAction("StockReturnList");
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                            }
                        }
                    }

                    _db.ReturnStockDetails.RemoveRange(returnStockDetails);
                    _db.ReturnStocks.RemoveRange(returnStocksToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Return stock records deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A Return Stock is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Return Stock.";
                    }
                }
            }
            return RedirectToAction("StockReturnList");
        }
        [HasPermission("Branch Stock Return Edit")]
        public ActionResult EditReturnStock(int ReturnStockId)
        {
            ReturnStock returnStock = _db.ReturnStocks.Find(ReturnStockId);
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
            return View(returnStock);
        }
        [HttpPost]
        [HasPermission("Branch Stock Return Edit")]
        public ActionResult EditReturnStock(ReturnStock returnStock)
        {
            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));

                    var existingReturnStock = _db.ReturnStocks.Include(p => p.ReturnStockDetails).FirstOrDefault(p => p.ReturnStockId == returnStock.ReturnStockId);
                    if (existingReturnStock == null)
                    {
                        return Json(new { status = "error", message = "Stock return record not found" }, JsonRequestBehavior.AllowGet);
                    }
                    if (existingReturnStock.Status == "Settled")
                    {
                        TempData["ErrorMessage"] = "Cannot update settled stock return record.";
                        return RedirectToAction("StockReturnList");
                    }

                    // Update purchase metadata
                    returnStock.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    returnStock.ModifiedAt = DateTime.Now;

                    // Update existing purchase values
                    _db.Entry(existingReturnStock).CurrentValues.SetValues(returnStock);
                    _db.Entry(existingReturnStock).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingReturnStock).Property(x => x.CreatedAt).IsModified = false;
                    _db.Entry(existingReturnStock).Property(x => x.BranchId).IsModified = false;
                    _db.Entry(existingReturnStock).Property(x => x.Status).IsModified = false;

                    // Update PurchaseDetails
                    var existingDetails = existingReturnStock.ReturnStockDetails.ToList();
                    var newDetails = returnStock.ReturnStockDetails;

                    // Update or Add new details
                    foreach (var newDetail in newDetails)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(d => d.ReturnStockDetailId == newDetail.ReturnStockDetailId);
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == newDetail.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();
                        if (existingDetail != null)
                        {
                            // Revert stock to previous state before applying new changes
                            var revertedQuantity = branchItem.Quantity + existingDetail.ItemQuantity;
                            var revertedTotalCost = (branchItem.Quantity * branchItem.CostPerUnit) + (existingDetail.ItemQuantity * existingDetail.CostPerUnit);
                            var revertedPerUnitCost = revertedTotalCost / revertedQuantity;

                            branchItem.Quantity = revertedQuantity;
                            branchItem.CostPerUnit = revertedPerUnitCost;

                            branchItem.Quantity -= newDetail.ItemQuantity;
                            var newTotalCost = newDetail.ItemQuantity * newDetail.CostPerUnit;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to update the stock return record. Quantity of " + branchItem.Item.ItemName + " cannot be < 0.";
                                return Json(new { success = false, message = "Unable to update the stock return record. Quantity of " + branchItem.Item.ItemName + " cannot be < 0." });
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((revertedTotalCost - newTotalCost) / branchItem.Quantity);
                            }

                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            // For new details, update stock directly
                            var totalCostBeforeChange = (branchItem.Quantity * branchItem.CostPerUnit);

                            branchItem.Quantity -= newDetail.ItemQuantity; // Subtract new quantity

                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to update the stock return record. Quantity of " + branchItem.Item.ItemName + " cannot be < 0.";
                                return Json(new { success = false, message = "Unable to update the stock return record. Quantity of " + branchItem.Item.ItemName + " cannot be < 0." });
                            }
                            else
                            {
                                var totalCostAfterChange = totalCostBeforeChange + (newDetail.ItemQuantity * newDetail.CostPerUnit);
                                branchItem.CostPerUnit = totalCostAfterChange / branchItem.Quantity;
                            }

                            existingReturnStock.ReturnStockDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {
                        foreach (var item in existingDetails)
                        {
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Unable to update the stock return record. Quantity of " + branchItem.Item.ItemName + " cannot be < 0.";
                                return Json(new { success = false, message = "Unable to update the stock return record. Quantity of " + branchItem.Item.ItemName + " cannot be < 0." });
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                            }
                        }
                        
                        _db.ReturnStockDetails.RemoveRange(existingDetails);
                    }


                    // Save changes
                    _db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Stock return record updated successfully!";
                    return Json(new { success = true });
                }
                catch (DbEntityValidationException)
                {
                    transaction.Rollback();
                    TempData["ErrorMessage"] = "An error occurred while updating the return stock record.";
                    return Json(new { success = false, redirect = Url.Action("StockReturnList", "Kitchen") });
                }
            }
        }
        [HasPermission("Consume List")]
        public ActionResult ConsumeItemList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var consumeItem = _db.ConsumeItems.Where(c => c.BranchId == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {
                consumeItem = consumeItem.Where(c => (c.ConsumeItemDetails.Where(i => i.Item.ItemName.Contains(search)).Count() > 0 || c.ReferenceNo.Contains(search) || c.Description.Contains(search)) && c.BranchId == selectedBranchId);
            }
            return View(consumeItem.ToList());
        }
        [HasPermission("Consume Create")]
        public ActionResult CreateConsumeItem()
        {
            var consumeItem = new ConsumeItem
            {
                ConsumeItemDetails = new List<ConsumeItemDetail>
                {
                    new ConsumeItemDetail()
                }

            };
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();

            return View(consumeItem);
        }
        [HttpPost]
        [HasPermission("Consume Create")]
        public ActionResult CreateConsumeItem(ConsumeItem consumeItem)
        {
            try
            {
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                if (ModelState.IsValid)
                {
                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextConsumeItemNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.ConsumeItems.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextConsumeItemNumber = _db.ConsumeItems
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.ReferenceNo.Substring(11)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    consumeItem.ReferenceNo = $"CON-{datePart}{nextConsumeItemNumber:D4}";
                    consumeItem.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    consumeItem.CreatedAt = DateTime.Now;
                    consumeItem.BranchId = selectedBranchId;

                    foreach (var item in consumeItem.ConsumeItemDetails)
                    {
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();

                        if (branchItem != null)
                        {
                            if (branchItem.Quantity >= item.ItemQuantity)
                            {
                                branchItem.Quantity -= item.ItemQuantity;
                                item.CostPerUnit = branchItem.CostPerUnit;

                                // Mark the branchItem as modified
                                _db.Entry(branchItem).State = EntityState.Modified;
                            }
                            else
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of "+ branchItem.Item.ItemName +" to create consumption";
                                return Json("0", JsonRequestBehavior.AllowGet);
                            }

                        }
                    }

                    _db.ConsumeItems.Add(consumeItem);
                    _db.SaveChanges();

                    return Json("0", JsonRequestBehavior.AllowGet);
                }

                ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
                return View(consumeItem);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error creating purchase: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
        [HasPermission("Consume Edit")]
        public ActionResult EditConsumeItem(int consumeItemId)
        {
            ConsumeItem consumeItem = _db.ConsumeItems.Find(consumeItemId);
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
            return View(consumeItem);
        }
        [HttpPost]
        [HasPermission("Consume Edit")]
        public ActionResult EditConsumeItem(ConsumeItem consumeItem)
        {
            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var existingConsumeItem = _db.ConsumeItems.Include(p => p.ConsumeItemDetails).FirstOrDefault(p => p.ConsumeItemId == consumeItem.ConsumeItemId);
                    if (existingConsumeItem == null)
                    {
                        TempData["ErrorMessage"] = "Record not found";
                        return Json(new { status = "error", message = "Record not found" }, JsonRequestBehavior.AllowGet);
                    }

                    // Update purchase metadata
                    consumeItem.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    consumeItem.ModifiedAt = DateTime.Now;

                    // Update existing purchase values
                    _db.Entry(existingConsumeItem).CurrentValues.SetValues(consumeItem);
                    _db.Entry(existingConsumeItem).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingConsumeItem).Property(x => x.CreatedAt).IsModified = false;
                    _db.Entry(existingConsumeItem).Property(x => x.BranchId).IsModified = false;

                    // Update PurchaseDetails
                    var existingDetails = existingConsumeItem.ConsumeItemDetails.ToList();
                    var newDetails = consumeItem.ConsumeItemDetails;

                    // Update or Add new details
                    foreach (var newDetail in newDetails)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(d => d.ConsumeItemDetailId == newDetail.ConsumeItemDetailId);
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == existingDetail.ItemId && b.BranchId == consumeItem.BranchId).FirstOrDefault();
                        if (existingDetail != null)
                        {
                            decimal RevertedQuantity = branchItem.Quantity + existingDetail.ItemQuantity;
                            decimal currentQuantity = branchItem.Quantity;

                            branchItem.Quantity = RevertedQuantity - newDetail.ItemQuantity; //Updated Quantity
                            if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to update the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (existingDetail.ItemQuantity * existingDetail.CostPerUnit) - (newDetail.ItemQuantity * newDetail.CostPerUnit)) / branchItem.Quantity; //Updated Per Unit Cost
                            }

                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity - newDetail.ItemQuantity; //Updated Quantity
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to update the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) - (newDetail.ItemQuantity * newDetail.CostPerUnit)) / branchItem.Quantity; //Updated Per Unit Cost
                            }
                            existingConsumeItem.ConsumeItemDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {
                        foreach (var item in existingDetails)
                        {
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == consumeItem.BranchId).FirstOrDefault();
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to update the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                            }
                        }
                        _db.ConsumeItemDetails.RemoveRange(existingDetails);
                    }


                    // Save changes
                    _db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Consume record updated successfully.";
                    return Json(new { status = "success" }, JsonRequestBehavior.AllowGet);
                }
                catch (DbEntityValidationException ex)
                {
                    transaction.Rollback();

                    // Log the validation errors for debugging
                    foreach (var validationError in ex.EntityValidationErrors)
                    {
                        foreach (var error in validationError.ValidationErrors)
                        {
                            // Log property name and error message
                            System.Diagnostics.Debug.WriteLine($"Property: {error.PropertyName}, Error: {error.ErrorMessage}");
                        }
                    }

                    TempData["ErrorMessage"] = "An error occurred while updating the consume record.";
                    return Json(new { status = "error", message = "Validation error occurred. Check logs for details." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        [HttpPost]
        [HasPermission("Consume Delete")]
        public ActionResult DeleteConsumeItem(int consumeItemId)
        {
            try
            {
                var consumeItem = _db.ConsumeItems.Find(consumeItemId);
                if (consumeItem == null)
                {
                    TempData["ErrorMessage"] = "ConsumeItem not found.";
                    return RedirectToAction("ConsumeItemList");
                }
                var consumeItemDetails = _db.ConsumeItemDetails.Select(p => p).Where(p => p.ConsumeItemId == consumeItemId);

                if (consumeItemDetails != null)
                {
                    foreach (var item in consumeItemDetails)
                    {
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == consumeItem.BranchId).FirstOrDefault();
                        decimal currentQuantity = branchItem.Quantity;
                        branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                        if (branchItem.Quantity == 0)
                        {
                            branchItem.CostPerUnit = 0;
                        }
                        else if (branchItem.Quantity < 0)
                        {
                            TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to delete the record.";
                            return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                        }
                        else
                        {
                            branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                        }
                    }
                }

                _db.ConsumeItemDetails.RemoveRange(consumeItemDetails);
                _db.ConsumeItems.Remove(consumeItem);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Consume item record deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This consume item record is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the consume item record.";
                }
            }
            return RedirectToAction("ConsumeItemList");
        }
        [HttpPost]
        [HasPermission("Consume Delete")]
        public ActionResult DeleteSelectedConsumeItems(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var consumeItemsToDelete = _db.ConsumeItems.Where(c => selectedItems.Contains(c.ConsumeItemId)).ToList();
                    var consumeItemDetails = _db.ConsumeItemDetails.Where(c => selectedItems.Contains(c.ConsumeItemId)).ToList();

                    if (consumeItemDetails != null)
                    {
                        foreach (var item in consumeItemDetails)
                        {
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == item.ConsumeItem.BranchId).FirstOrDefault();
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to delete the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                            }
                        }
                    }

                    _db.ConsumeItemDetails.RemoveRange(consumeItemDetails);
                    _db.ConsumeItems.RemoveRange(consumeItemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Consume item records deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A consume item record is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the consume item records.";
                    }
                }
            }
            return RedirectToAction("ConsumeItemList");
        }

        [HasPermission("Wastage List")]
        public ActionResult WastageItemList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var wastageItem = _db.WastageItems.Where(c => c.BranchId == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {
                wastageItem = wastageItem.Where(c => (c.WastageItemDetails.Where(i => i.Item.ItemName.Contains(search)).Count() > 0 || c.ReferenceNo.Contains(search) || c.Description.Contains(search)) && c.BranchId == selectedBranchId);
            }
            return View(wastageItem.ToList());
        }
        [HasPermission("Wastage Create")]
        public ActionResult CreateWastageItem()
        {
            var wastageItem = new WastageItem
            {
                WastageItemDetails = new List<WastageItemDetail>
                {
                    new WastageItemDetail()
                }

            };
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();

            return View(wastageItem);
        }
        [HttpPost]
        [HasPermission("Wastage Create")]
        public ActionResult CreateWastageItem(WastageItem wastageItem)
        {
            try
            {
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                if (ModelState.IsValid)
                {
                    string datePart = DateTime.Now.ToString("yyyyMM");
                    int nextWastageItemNumber = 1;

                    // Check if there are any existing purchases first
                    if (_db.WastageItems.Any())
                    {
                        // Bring the PurchaseNo values into memory, then extract the numeric part and calculate the max
                        nextWastageItemNumber = _db.WastageItems
                            .AsEnumerable()  // Forces execution in-memory
                            .Select(p => int.Parse(p.ReferenceNo.Substring(11)))  // Now we can safely use Convert.ToInt32
                            .Max() + 1;
                    }
                    wastageItem.ReferenceNo = $"WAS-{datePart}{nextWastageItemNumber:D4}";
                    wastageItem.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    wastageItem.CreatedAt = DateTime.Now;
                    wastageItem.BranchId = selectedBranchId;

                    foreach (var item in wastageItem.WastageItemDetails)
                    {
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == selectedBranchId).FirstOrDefault();

                        if (branchItem != null)
                        {
                            if (branchItem.Quantity >= item.ItemQuantity)
                            {
                                branchItem.Quantity -= item.ItemQuantity;
                                item.CostPerUnit = branchItem.CostPerUnit;

                                // Mark the branchItem as modified
                                _db.Entry(branchItem).State = EntityState.Modified;
                            }
                            else
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to create wastage record";
                                return Json("0", JsonRequestBehavior.AllowGet);
                            }
                            
                        }
                    }

                    _db.WastageItems.Add(wastageItem);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Wastage item record created successfully.";
                    return Json("0", JsonRequestBehavior.AllowGet);
                }

                ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
                return View(wastageItem);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                Console.WriteLine($"Error creating purchase: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
        [HasPermission("Wastage Edit")]
        public ActionResult EditWastageItem(int wastageItemId)
        {
            WastageItem wastageItem = _db.WastageItems.Find(wastageItemId);
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
            return View(wastageItem);
        }
        [HttpPost]
        [HasPermission("Wastage Edit")]
        public ActionResult EditWastageItem(WastageItem wastageItem)
        {
            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var existingWastageItem = _db.WastageItems.Include(p => p.WastageItemDetails).FirstOrDefault(p => p.WastageItemId == wastageItem.WastageItemId);
                    if (existingWastageItem == null)
                    {
                        TempData["ErrorMessage"] = "Record not found";
                        return Json(new { status = "error", message = "Record not found" }, JsonRequestBehavior.AllowGet);
                    }

                    // Update purchase metadata
                    wastageItem.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    wastageItem.ModifiedAt = DateTime.Now;

                    // Update existing purchase values
                    _db.Entry(existingWastageItem).CurrentValues.SetValues(wastageItem);
                    _db.Entry(existingWastageItem).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingWastageItem).Property(x => x.CreatedAt).IsModified = false;
                    _db.Entry(existingWastageItem).Property(x => x.BranchId).IsModified = false;

                    // Update PurchaseDetails
                    var existingDetails = existingWastageItem.WastageItemDetails.ToList();
                    var newDetails = wastageItem.WastageItemDetails;

                    // Update or Add new details
                    foreach (var newDetail in newDetails)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(d => d.WastageItemDetailId == newDetail.WastageItemDetailId);
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == existingDetail.ItemId && b.BranchId == wastageItem.BranchId).FirstOrDefault();
                        if (existingDetail != null)
                        {
                            decimal RevertedQuantity = branchItem.Quantity + existingDetail.ItemQuantity;
                            decimal currentQuantity = branchItem.Quantity;

                            branchItem.Quantity = RevertedQuantity - newDetail.ItemQuantity; //Updated Quantity
                            if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to update the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (existingDetail.ItemQuantity * existingDetail.CostPerUnit) - (newDetail.ItemQuantity * newDetail.CostPerUnit)) / branchItem.Quantity; //Updated Per Unit Cost
                            }

                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity - newDetail.ItemQuantity; //Updated Quantity
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to update the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) - (newDetail.ItemQuantity * newDetail.CostPerUnit)) / branchItem.Quantity; //Updated Per Unit Cost
                            }
                            existingWastageItem.WastageItemDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {
                        foreach (var item in existingDetails)
                        {
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == wastageItem.BranchId).FirstOrDefault();
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to update the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                            }
                        }
                        _db.WastageItemDetails.RemoveRange(existingDetails);
                    }


                    // Save changes
                    _db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Wastage record updated successfully.";
                    return Json(new { status = "success" }, JsonRequestBehavior.AllowGet);
                }
                catch (DbEntityValidationException ex)
                {
                    transaction.Rollback();

                    // Log the validation errors for debugging
                    foreach (var validationError in ex.EntityValidationErrors)
                    {
                        foreach (var error in validationError.ValidationErrors)
                        {
                            // Log property name and error message
                            System.Diagnostics.Debug.WriteLine($"Property: {error.PropertyName}, Error: {error.ErrorMessage}");
                        }
                    }

                    TempData["ErrorMessage"] = "An error occurred while updating the wastage record.";
                    return Json(new { status = "error", message = "Validation error occurred. Check logs for details." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        [HttpPost]
        [HasPermission("Wastage Delete")]
        public ActionResult DeleteWastageItem(int wastageItemId)
        {
            try
            {
                var wastageItem = _db.WastageItems.Find(wastageItemId);
                if (wastageItem == null)
                {
                    TempData["ErrorMessage"] = "WastageItem not found.";
                    return RedirectToAction("WastageItemList");
                }
                var wastageItemDetails = _db.WastageItemDetails.Select(p => p).Where(p => p.WastageItemId == wastageItemId);

                if (wastageItemDetails != null)
                {
                    foreach (var item in wastageItemDetails)
                    {
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == wastageItem.BranchId).FirstOrDefault();
                        decimal currentQuantity = branchItem.Quantity;
                        branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                        if (branchItem.Quantity == 0)
                        {
                            branchItem.CostPerUnit = 0;
                        }
                        else if (branchItem.Quantity < 0)
                        {
                            TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to delete the record.";
                            return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                        }
                        else
                        {
                            branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                        }
                    }
                }

                _db.WastageItemDetails.RemoveRange(wastageItemDetails);
                _db.WastageItems.Remove(wastageItem);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Wastage item record deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This wastage item record is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the wastage item record.";
                }
            }
            return RedirectToAction("WastageItemList");
        }
        [HttpPost]
        [HasPermission("Wastage Delete")]
        public ActionResult DeleteSelectedWastageItems(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var wastageItemsToDelete = _db.WastageItems.Where(c => selectedItems.Contains(c.WastageItemId)).ToList();
                    var wastageItemDetails = _db.WastageItemDetails.Where(c => selectedItems.Contains(c.WastageItemId)).ToList();

                    if (wastageItemDetails != null)
                    {
                        foreach (var item in wastageItemDetails)
                        {
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == item.WastageItem.BranchId).FirstOrDefault();
                            decimal currentQuantity = branchItem.Quantity;
                            branchItem.Quantity = branchItem.Quantity + item.ItemQuantity;
                            if (branchItem.Quantity == 0)
                            {
                                branchItem.CostPerUnit = 0;
                            }
                            else if (branchItem.Quantity < 0)
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to delete the record.";
                                return Json(new { status = "error", message = "Insufficient quantity" }, JsonRequestBehavior.AllowGet);
                            }
                            else
                            {
                                branchItem.CostPerUnit = ((currentQuantity * branchItem.CostPerUnit) + (item.ItemQuantity * item.CostPerUnit)) / branchItem.Quantity;
                            }
                        }
                    }

                    _db.WastageItemDetails.RemoveRange(wastageItemDetails);
                    _db.WastageItems.RemoveRange(wastageItemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Wastage item record deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A wastage item record is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the wastage item records.";
                    }
                }
            }
            return RedirectToAction("WastageItemList");
        }

        [HasPermission("Product Category Create")]
        public ActionResult CreateCategory()
        {
            return View();
        }
        [HttpPost]
        [HasPermission("Product Category Create")]
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

        [HasPermission("Product Category List")]
        public ActionResult CategoryList(string search)
        {
            var productCategory = _db.ProductCategories.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                productCategory = productCategory.Where(c => c.ProductCategoryName.Contains(search));
            }
            return View(productCategory.ToList());
        }

        [HasPermission("Product Category Edit")]
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
        [HasPermission("Product Category Edit")]
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
        [HasPermission("Product Category Delete")]
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

        [HasPermission("Product Category Delete")]
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
        [HasPermission("Product Create")]
        public ActionResult CreateProduct()
        {
            var product = new Product
            {
                IsActive = true,
                ProductItemDetails = new List<ProductItemDetail>
                {
                    new ProductItemDetail()
                }

            };
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
            ViewBag.ProductCategories = _db.ProductCategories.ToList();
            ViewBag.Chefs = _db.Users.Where(u => u.IsChef).ToList();

            return View(product);
        }

        [HttpPost]
        [HasPermission("Product Create")]
        public ActionResult CreateProduct(Product product, HttpPostedFileBase imageFile)
        {
            try
            {
                var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                if (ModelState.IsValid)
                {
                    string uploadFolder = Server.MapPath("~/Uploads/ProductImages");
                    if (!Directory.Exists(uploadFolder))
                    {
                        Directory.CreateDirectory(uploadFolder);
                    }

                    if (imageFile != null && imageFile.ContentLength > 0)
                    {
                        string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Path.GetFileName(imageFile.FileName);
                        string filePath = Path.Combine(uploadFolder, fileName);

                        try
                        {
                            imageFile.SaveAs(filePath);
                            product.ProductImage = fileName; // Save relative path
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error saving file: {ex.Message}");
                            product.ProductImage = "/Content/assets/no_image.png"; // Use default image
                        }
                    }
                    else
                    {
                        product.ProductImage = "/Content/assets/no_image.png"; // Default image path
                    }

                    product.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    product.CreatedAt = DateTime.Now;
                    product.BranchId = selectedBranchId;

                    _db.Products.Add(product);
                    _db.SaveChanges();

                    return Json("0", JsonRequestBehavior.AllowGet);
                }

                ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
                ViewBag.ProductCategories = _db.ProductCategories.ToList();
                ViewBag.Chefs = _db.Users.Where(u => u.IsChef).ToList();

                return View(product);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating product: {ex.Message}");
                return RedirectToAction("Index", "Error");
            }
        }
        [HasPermission("Product List")]
        public ActionResult ProductList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var product = _db.Products.AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {

                product = product.Where(c => (c.ProductItemDetails.Where(i => i.Product.ProductName.Contains(search)).Count() > 0 || c.ProductCategory.ProductCategoryName.Contains(search)));
            }
            return View(product.ToList());
        }
        [HttpPost]

        [HasPermission("Product Delete")]
        public ActionResult DeleteProduct(int ProductId)
        {
            try
            {
                var product = _db.Products.Find(ProductId);
                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToAction("ProductList");
                }
                var productItemDetails = _db.ProductItemDetails.Select(p => p).Where(p => p.ProductId == ProductId);
                _db.ProductItemDetails.RemoveRange(productItemDetails);
                _db.Products.Remove(product);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Product deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Product is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Product.";
                }
            }
            return RedirectToAction("ProductList");

        }
        [HttpPost]
        [HasPermission("Product Delete")]
        public ActionResult DeleteSelectedProducts(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var productToDelete = _db.Products.Where(c => selectedItems.Contains(c.ProductId)).ToList();
                    var productItemDetails = _db.ProductItemDetails.Where(c => selectedItems.Contains(c.ProductId)).ToList();

                    _db.ProductItemDetails.RemoveRange(productItemDetails);
                    _db.Products.RemoveRange(productToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Product deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A Product is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Product.";
                    }
                }
            }
            return RedirectToAction("ProductList");
        }

        [HasPermission("Product Edit")]
        public ActionResult EditProduct(int ProductId)
        {
            Product product = _db.Products.Find(ProductId);
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            ViewBag.Items = _db.BranchItems.Where(i => i.IsActive == true && i.BranchId == selectedBranchId).ToList();
            ViewBag.ProductCategories = _db.ProductCategories.ToList();
            ViewBag.Chefs = _db.Users.Where(u => u.IsChef).ToList();

            return View(product);
        }
        [HttpPost]
        [HasPermission("Product Edit")]
        public ActionResult EditProduct(Product product, HttpPostedFileBase imageFile)
        {
            if (product == null)
            {
                foreach (var state in ModelState)
                {
                    var key = state.Key; // Property name
                    var errors = state.Value.Errors; // List of errors for this property

                    foreach (var error in errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"Key: {key}, Error: {error.ErrorMessage}");
                    }
                }

                return Json(new { status = "error", message = "Invalid data provided" }, JsonRequestBehavior.AllowGet);
            }

            using (var transaction = _db.Database.BeginTransaction())
            {
                try
                {
                    var existingProduct = _db.Products.Include(p => p.ProductItemDetails)
                                                      .FirstOrDefault(p => p.ProductId == product.ProductId);
                    if (existingProduct == null)
                    {
                        return Json(new { status = "error", message = "Product not found" }, JsonRequestBehavior.AllowGet);
                    }

                    // Update metadata
                    product.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    product.ModifiedAt = DateTime.Now;

                    // Update existing product values
                    _db.Entry(existingProduct).CurrentValues.SetValues(product);
                    _db.Entry(existingProduct).Property(x => x.CreatedBy).IsModified = false;
                    _db.Entry(existingProduct).Property(x => x.CreatedAt).IsModified = false;
                    _db.Entry(existingProduct).Property(x => x.ProductImage).IsModified = false;
                    _db.Entry(existingProduct).Property(x => x.BranchId).IsModified = false;

                    // Update ProductItemDetails
                    var existingDetails = existingProduct.ProductItemDetails.ToList();
                    var newDetails = product.ProductItemDetails;

                    // Update or Add new details
                    foreach (var newDetail in newDetails)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(d => d.ProductItemDetailId == newDetail.ProductItemDetailId);
                        if (existingDetail != null)
                        {
                            _db.Entry(existingDetail).CurrentValues.SetValues(newDetail);
                            existingDetails.Remove(existingDetail); // Remove from existing list once matched
                        }
                        else
                        {
                            existingProduct.ProductItemDetails.Add(newDetail); // Add new detail
                        }
                    }

                    // Delete unmatched details
                    if (existingDetails != null)
                    {
                        _db.ProductItemDetails.RemoveRange(existingDetails);
                    }

                    // Handle image update
                    if (imageFile != null && imageFile.ContentLength > 0)
                    {
                        string uploadFolder = Server.MapPath("~/Uploads/ProductImages");
                        if (!Directory.Exists(uploadFolder))
                        {
                            Directory.CreateDirectory(uploadFolder);
                        }

                        string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Path.GetFileName(imageFile.FileName);
                        string filePath = Path.Combine(uploadFolder, fileName);

                        try
                        {
                            // Save the new image
                            imageFile.SaveAs(filePath);
                            existingProduct.ProductImage = fileName; // Save relative path
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error saving file: {ex.Message}");
                        }
                    }

                    // Save changes
                    _db.SaveChanges();
                    transaction.Commit();

                    TempData["SuccessMessage"] = "Product updated successfully.";
                    return Json(new { status = "success" }, JsonRequestBehavior.AllowGet);
                }
                catch (DbEntityValidationException ex)
                {
                    transaction.Rollback();

                    // Log the validation errors for debugging
                    foreach (var validationError in ex.EntityValidationErrors)
                    {
                        foreach (var error in validationError.ValidationErrors)
                        {
                            // Log property name and error message
                            System.Diagnostics.Debug.WriteLine($"Property: {error.PropertyName}, Error: {error.ErrorMessage}");
                        }
                    }

                    TempData["ErrorMessage"] = "An error occurred while updating the product.";
                    return Json(new { status = "error", message = "Validation error occurred. Check logs for details." }, JsonRequestBehavior.AllowGet);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine($"Error updating product: {ex.Message}");
                    TempData["ErrorMessage"] = "An unexpected error occurred.";
                    return Json(new { status = "error", message = "Unexpected error occurred." }, JsonRequestBehavior.AllowGet);
                }
            }
        }
        [HasPermission("Order List")]
        public ActionResult OrderList(string search)
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var orders = _db.Orders.Where(o => o.BranchId == selectedBranchId);
            if (!string.IsNullOrEmpty(search))
            {
                orders = orders.Where(c => c.OrderNo.Contains(search) ||
                                           c.OrderTotal.ToString().Equals(search) ||
                                           c.Status.Contains(search));
            }
            orders = orders.OrderByDescending(o => o.OrderId);
            return View(orders.ToList());
        }

        [HasPermission("Order Create")]
        public ActionResult CreateOrder()
        {
            //var branchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var order = new Order
            {
                OrderDetails = new List<OrderDetail>
                {
                    new OrderDetail()
                }

            };
            //ViewBag.Products = _db.Products.Where(i => i.IsActive == true && i.BranchId == branchId).ToList();
            ViewBag.Products = _db.Products.Where(i => i.IsActive == true).ToList();

            return View(order);
        }
        [HttpPost]
        [HasPermission("Order Create")]
        public ActionResult CreateOrder(Order order, decimal grandTotal)
        {
            var branchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            try
            {
                if (ModelState.IsValid)
                {
                    order.OrderTotalCost = 0;
                    foreach (var orderDetail in order.OrderDetails)
                    {
                        orderDetail.ProductStatus = "Pending";
                        orderDetail.ProductCost = 0;
                        var orderDetailProduct = _db.Products.Find(orderDetail.ProductId);
                        foreach (var item in orderDetailProduct.ProductItemDetails)
                        {
                            var itemQuantityToLess = item.ItemQuantity * orderDetail.ProductQuantity;
                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == order.BranchId).FirstOrDefault();
                            if (branchItem != null)
                            {
                                if (branchItem.Quantity >= itemQuantityToLess)
                                {
                                    branchItem.Quantity -= itemQuantityToLess;
                                    orderDetail.ProductCost += (branchItem.CostPerUnit * itemQuantityToLess);
                                    _db.Entry(branchItem).State = EntityState.Modified;
                                }
                                else
                                {
                                    return Json(new { success = false, message = "Insufficient quantity of " + branchItem.Item.ItemName + " to prepare " + orderDetailProduct.ProductName });
                                }
                            }
                        }
                        order.OrderTotalCost += orderDetail.ProductCost;
                    }

                    int nextOrderNumber = 1;
                    if (_db.Orders.Any(o => o.BranchId == branchId))
                    {
                        nextOrderNumber = _db.Orders
                            .Where(o => o.BranchId == branchId)
                            .AsEnumerable()
                            .Select(o =>
                            {
                                int orderNo;
                                return int.TryParse(o.OrderNo, out orderNo) ? orderNo : 0;
                            })
                            .Max() + 1;
                    }
                    order.OrderNo = nextOrderNumber.ToString("D5");
                    order.BranchId = branchId;
                    order.OrderTotal = grandTotal;
                    order.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                    order.OrderDate = DateTime.Now;
                    order.CreatedAt = DateTime.Now;
                    order.Status = "Pending";

                    _db.Orders.Add(order);
                    _db.SaveChanges();



                    //var branch = _db.Branches.Find(order.BranchId);


                    //     List<string> Connections = _db.Users.Select(u => u.ConnectionId).ToList();
                    //var context = GlobalHost.ConnectionManager.GetHubContext<RessentialHub>();
                    //foreach (var connection in Connections)
                    //{
                    //    context.Clients.Client(connection).UpdateChefView();
                    //}

                    //TempData["SuccessMessage"] = "Order placed successfully.";
                    //return RedirectToAction("CreateOrder", "Kitchen");
                    //}

                    var branch = _db.Branches.Find(order.BranchId);
                    var user = _db.Users.Find(order.CreatedBy);

                    string formattedPhone = FormatPhoneNumber(branch.BranchContact);
                    return Json(new {
                        success = true,
                        orderNo = order.OrderNo,
                        orderDate = order.OrderDate.ToString("yyyy-MM-dd"),
                        branchName = branch.BranchName,
                        branchAddress = branch.Address,
                        branchPhone = formattedPhone,
                        staffName = user.UserName,
                    });
                }
                return Json(new { success = false, message = "Invalid order data." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error creating order: " + ex.Message });
            }
        }
        string FormatPhoneNumber(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return phone;

            phone = phone.Trim();

            if (phone.StartsWith("+92") && phone.Length == 13)
            {
                // Format: +923163033321 -> +92-316-3033321
                return $"+92-{phone.Substring(3, 3)}-{phone.Substring(6)}";
            }
            else if (phone.StartsWith("03") && phone.Length == 11)
            {
                // Format: 03112824123 -> +92-311-2824123
                return $"+92-{phone.Substring(1, 3)}-{phone.Substring(4)}";
            }

            // If format doesn't match, return as is
            return phone;
        }
        [HttpPost]
        [HasPermission("Order Status Update")]
        public ActionResult CancelOrder(int orderId)
        {
            try
            {
                var order = _db.Orders.Find(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    //return RedirectToAction("OrderList");
                    return Json(new {success = false});
                }

                if (order.Status == "Cancelled")
                {
                    TempData["ErrorMessage"] = "Order is already cancelled.";
                }
                else if (order.Status == "Returned")
                {
                    TempData["ErrorMessage"] = "Returned Order cannot be cancelled.";
                }
                else if (order.Status == "Pending")
                {
                    order.Status = "Cancelled";
                    TempData["SuccessMessage"] = "Order cancelled successfully.";
                }
                else if (order.Status == "Preparing" || order.Status == "Ready" || order.Status == "Out for Delivery" || order.Status == "Confirmed")
                {
                    //Revert the stock

                    foreach (var orderDetail in order.OrderDetails)
                    {
                        orderDetail.ProductStatus = "Cancelled";
                        foreach (var item in orderDetail.Product.ProductItemDetails)
                        {
                            var itemQuantityToAdd = item.ItemQuantity * orderDetail.ProductQuantity;

                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == order.BranchId).FirstOrDefault();
                            if (branchItem != null)
                            {
                                branchItem.Quantity += itemQuantityToAdd;
                                // Mark the branchItem as modified
                                _db.Entry(branchItem).State = EntityState.Modified;
                            }
                        }
                    }

                    order.Status = "Cancelled";
                    TempData["SuccessMessage"] = "Order cancelled successfully.";
                }
                else if (order.Status == "Completed")
                {
                    TempData["ErrorMessage"] = "Completed order can not be cancelled.";
                }

                _db.SaveChanges();
                
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while cancelling the order.";
                Console.WriteLine($"Error cancelling order: {ex.Message}");
            }

            //return RedirectToAction("OrderList");
            return Json(new { success = true });
        }

        [HttpPost]
        [HasPermission("Order Status Update")]
        public ActionResult OrderReturn(int orderId)
        {
            try
            {
                var order = _db.Orders.Find(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    //return RedirectToAction("OrderList");
                    return Json(new { success = false });
                }

                if (order.Status == "Preparing" || order.Status == "Cancelled" || order.Status == "Pending" || order.Status == "Ready" || order.Status == "Out for Delivery" || order.Status == "Confirmed")
                {
                    TempData["ErrorMessage"] = "Order can be returned only for Completed orders";
                }
                else if (order.Status == "Returned")
                {
                    TempData["ErrorMessage"] = "Order status is already Returned";
                }
                else if (order.Status == "Completed")
                {
                    //Revert the stock

                    foreach (var orderDetail in order.OrderDetails)
                    {
                        orderDetail.ProductStatus = "Cancelled";
                        foreach (var item in orderDetail.Product.ProductItemDetails)
                        {
                            var itemQuantityToAdd = item.ItemQuantity * orderDetail.ProductQuantity;

                            var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == order.BranchId).FirstOrDefault();
                            if (branchItem != null)
                            {
                                branchItem.Quantity += itemQuantityToAdd;
                                // Mark the branchItem as modified
                                _db.Entry(branchItem).State = EntityState.Modified;
                            }
                        }
                    }

                    order.Status = "Returned";
                    TempData["SuccessMessage"] = "Order returned successfully.";
                }

                _db.SaveChanges();

            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while cancelling the order.";
                Console.WriteLine($"Error cancelling order: {ex.Message}");
            }

            //return RedirectToAction("OrderList");
            return Json(new { success = true });
        }


        [HttpPost]
        [HasPermission("Order Status Update")]
        public ActionResult ConfirmOrder(int orderId)
        {
            try
            {
                var order = _db.Orders.Find(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    //return RedirectToAction("OrderList");
                    return Json(new { success = false });
                }
                order.OrderTotalCost = 0;
                if (order.Status == "Cancelled")
                {
                    TempData["ErrorMessage"] = "Can not confirm the cancelled order.";
                    //return RedirectToAction("OrderList");
                    return Json(new { success = false });
                }
                else if (order.Status == "Returned")
                {
                    TempData["ErrorMessage"] = "Can not confirm the returned order.";
                    return Json(new { success = false });
                }
                foreach (var orderDetail in order.OrderDetails)
                {
                    orderDetail.ProductCost = 0;
                    foreach (var item in orderDetail.Product.ProductItemDetails)
                    {
                        var itemQuantityToLess = item.ItemQuantity * orderDetail.ProductQuantity;
                        var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == order.BranchId).FirstOrDefault();
                        if (branchItem != null)
                        {
                            if (branchItem.Quantity >= itemQuantityToLess)
                            {
                                branchItem.Quantity -= itemQuantityToLess;
                                orderDetail.ProductCost += (branchItem.CostPerUnit * itemQuantityToLess);
                                // Mark the branchItem as modified
                                _db.Entry(branchItem).State = EntityState.Modified;
                            }
                            else
                            {
                                TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to prepare " + orderDetail.Product.ProductName;
                                //return RedirectToAction("OrderList");
                                return Json(new { success = false });
                            }
                        }
                    }
                    order.OrderTotalCost += orderDetail.ProductCost;
                }
                order.Status = "Preparing";
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Order confirmed successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while confirming the order.";
                Console.WriteLine($"Error confirming order: {ex.Message}");
            }

            //return RedirectToAction("OrderList");
            return Json(new { success = true });
        }

        [HttpPost]
        [HasPermission("Order Status Update")]
        public ActionResult CompleteOrder(int orderId)
        {
            try
            {
                var order = _db.Orders.Find(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    //return RedirectToAction("OrderList");
                    return Json(new { success = false });
                }

                if (order.Status == "Cancelled")
                {
                    TempData["ErrorMessage"] = "Unable to update the status for cancelled order.";
                }
                else if (order.Status == "Returned")
                {
                    TempData["ErrorMessage"] = "Unable to update the status for returned order.";
                }
                else if (order.Status == "Pending")
                {
                    TempData["ErrorMessage"] = "Unable to update the status for Pending order.";
                }
                else if (order.Status == "Preparing" || order.Status == "Ready" || order.Status == "Out for Delivery")
                {
                    order.Status = "Completed";
                    TempData["SuccessMessage"] = "Order completed successfully.";
                }
                else if (order.Status == "Completed")
                {
                    TempData["ErrorMessage"] = "The order is already completed.";
                }

                _db.SaveChanges();

            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the order status.";
                Console.WriteLine($"Error updating order status: {ex.Message}");
            }

            //return RedirectToAction("OrderList");
            return Json(new { success = true });
        }

        [HttpPost]
        [HasPermission("Order Status Update")]
        public ActionResult OutForDelivery(int orderId)
        {
            try
            {
                var order = _db.Orders.Find(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    //return RedirectToAction("OrderList");
                    return Json(new { success = false });
                }

                if (order.Status == "Cancelled")
                {
                    TempData["ErrorMessage"] = "Unable to update the status for cancelled order.";
                }
                else if (order.Status == "Returned")
                {
                    TempData["ErrorMessage"] = "Unable to update the status for returned order.";
                }
                else if (order.Status == "Pending")
                {
                    TempData["ErrorMessage"] = "Unable to update the status for Pending order.";
                }
                else if (order.Status == "Preparing" || order.Status == "Ready")
                {
                    order.Status = "Out For Delivery";
                    TempData["SuccessMessage"] = "Order marked as Out for Delivery.";
                }
                else if (order.Status == "Completed")
                {
                    TempData["ErrorMessage"] = "Unable to update the status for completed order.";
                }

                _db.SaveChanges();

            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the order status.";
                Console.WriteLine($"Error updating order status: {ex.Message}");
            }

            //return RedirectToAction("OrderList");
            return Json(new { success = true });
        }

        [HttpPost]
        [HasPermission("Order Status Update")]
        public ActionResult CancelSelectedOrders(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var ordersToCancel = _db.Orders.Where(o => selectedItems.Contains(o.OrderId)).ToList();

                    foreach (var order in ordersToCancel)
                    {
                        if (order.Status == "Cancelled")
                        {
                            TempData["ErrorMessage"] = "An order exist which is already cancelled.";
                            return RedirectToAction("OrderList");

                        }
                        else if (order.Status == "Pending")
                        {
                            order.Status = "Cancelled";
                        }
                        else if (order.Status == "Preparing" || order.Status == "Ready" || order.Status == "Out for Delivery" || order.Status == "Confirmed")
                        {
                            //Revert the stock

                            foreach (var orderDetail in order.OrderDetails)
                            {
                                orderDetail.ProductStatus = "Cancelled";
                                foreach (var item in orderDetail.Product.ProductItemDetails)
                                {
                                    var itemQuantityToAdd = item.ItemQuantity * orderDetail.ProductQuantity;

                                    var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == order.BranchId).FirstOrDefault();
                                    if (branchItem != null)
                                    {
                                        branchItem.Quantity += itemQuantityToAdd;
                                        // Mark the branchItem as modified
                                        _db.Entry(branchItem).State = EntityState.Modified;
                                    }
                                }
                            }

                            order.Status = "Cancelled";
                        }
                        else if (order.Status == "Completed")
                        {
                            TempData["ErrorMessage"] = "An order exist which is completed and can not be cancelled.";
                            return RedirectToAction("OrderList");
                        }
                    }

                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Selected orders have been cancelled successfully.";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while cancelling the selected orders.";
                    Console.WriteLine($"Error cancelling selected orders: {ex.Message}");
                }
            }
            else
            {
                TempData["ErrorMessage"] = "No orders selected for cancellation.";
            }

            return RedirectToAction("OrderList");
        }

        [HttpPost]
        [HasPermission("Order Status Update")]
        public ActionResult ConfirmSelectedOrders(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var ordersToConfirm = _db.Orders.Where(o => selectedItems.Contains(o.OrderId)).ToList();

                    foreach (var order in ordersToConfirm)
                    {
                        if (order.Status == "Cancelled")
                        {
                            TempData["ErrorMessage"] = "Can not confirm the order. An order exist which is cancelled.";
                            return RedirectToAction("OrderList");
                        }
                        order.OrderTotalCost = 0;
                        foreach (var orderDetail in order.OrderDetails)
                        {
                            orderDetail.ProductCost = 0;
                            foreach (var item in orderDetail.Product.ProductItemDetails)
                            {
                                var itemQuantityToLess = item.ItemQuantity * orderDetail.ProductQuantity;
                                var branchItem = _db.BranchItems.Where(b => b.ItemId == item.ItemId && b.BranchId == order.BranchId).FirstOrDefault();
                                if (branchItem != null)
                                {
                                    if (branchItem.Quantity >= itemQuantityToLess)
                                    {
                                        branchItem.Quantity -= itemQuantityToLess;
                                        orderDetail.ProductCost += (branchItem.CostPerUnit * itemQuantityToLess);
                                        // Mark the branchItem as modified
                                        _db.Entry(branchItem).State = EntityState.Modified;
                                    }
                                    else
                                    {
                                        TempData["ErrorMessage"] = "Insufficient quantity of " + branchItem.Item.ItemName + " to prepare " + orderDetail.Product.ProductName;
                                        return RedirectToAction("OrderList");
                                    }
                                }
                            }
                            order.OrderTotalCost += orderDetail.ProductCost;
                        }
                        order.Status = "Preparing";
                    }

                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Selected orders have been confirmed successfully.";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while confirming the selected orders.";
                    Console.WriteLine($"Error confirming selected orders: {ex.Message}");
                }
            }
            else
            {
                TempData["ErrorMessage"] = "No orders selected for confirmation.";
            }

            return RedirectToAction("OrderList");
        }

        [HasPermission("Order View")]
        public ActionResult OrderDetail(int orderId)
        {
            var order = _db.Orders.Include(o => o.OrderDetails.Select(od => od.Product)).FirstOrDefault(o => o.OrderId == orderId);
            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction("OrderList");
            }
            return View(order);
        }

        public ActionResult OrderReturnList()
        {
            return View();
        }
        public ActionResult CreateOrderReturn()
        {
            return View();
        }
        [HasPermission("Order View View")]
        public ActionResult OrderView()
        {
            return View();
        }

        [HttpGet]
        public ActionResult GetOrders()
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var orders = _db.Orders
                            .Where(o => o.BranchId == selectedBranchId)
                            .Select(o => new
                            {
                                o.OrderId,
                                o.OrderNo,
                                o.OrderType,
                                o.TableNo,
                                o.Status,
                                OrderDetails = o.OrderDetails.Select(od => new
                                {
                                    od.Product.ProductName,
                                    od.ProductQuantity,
                                    od.ProductStatus
                                }).ToList()
                            }).ToList();

            return Json(new { orders }, JsonRequestBehavior.AllowGet);
        }

        [HasPermission("Chef View View")]
        public ActionResult ChefView()
        {
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var branchChefs = _db.UserBranchPermissions
                     .Where(ubp => ubp.BranchId == selectedBranchId)
                     .Select(ubp => ubp.UserId)
                     .ToList();

            var chefs = _db.Users
                           .Where(u => branchChefs.Contains(u.UserId) && u.IsChef)
                           .ToList();

            ViewBag.Chefs = new SelectList(chefs, "UserId", "UserName");
            ViewBag.PageSize = PaginationConstraints.ChefViewPageSize;
            ViewBag.MaxPage = PaginationConstraints.ChefViewMaxPage;
            return View();
        }


        [HttpPost]
        [HasPermission("Chef View View")]
        public ActionResult GetChefProducts(int? chefId, List<string> statuses, int page = 1)
        {
            int pageSize = PaginationConstraints.ChefViewPageSize;
            var selectedBranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));

            var productsQuery = _db.OrderDetails.Include(od => od.Product)
                                                .Include(od => od.Order)
                                                .Where(od => od.Order.BranchId == selectedBranchId)
                                                .AsQueryable();

            if (chefId.HasValue)
            {
                productsQuery = productsQuery.Where(od => od.Product.ChefID == chefId);
            }

            if (statuses != null && statuses.Count > 0)
            {
                productsQuery = productsQuery.Where(od => statuses.Contains(od.ProductStatus));
            }

            var totalProducts = productsQuery.Count();
            var products = productsQuery.OrderBy(od => od.OrderDetailId)
                                        .Skip((page - 1) * pageSize)
                                        .Take(pageSize)
                                        .Select(od => new
                                        {
                                            od.Order.OrderNo,
                                            od.Product.ProductImage,
                                            od.Product.ProductName,
                                            od.ProductQuantity,
                                            od.OrderId,
                                            od.OrderDetailId,
                                            od.ProductStatus
                                        }).ToList();

            return Json(new { totalProducts, products }, JsonRequestBehavior.AllowGet);
        }

        
        [HttpPost]
        [HasPermission("Order Status Update")]
        public ActionResult UpdateOrderStatus(int orderId, string status)
        {
            if (status == "Confirm")
            {
                ConfirmOrder(orderId);
            }

            var orderDetail = _db.OrderDetails.Find(orderId);
            if (orderDetail == null)
            {
                return Json(new { success = false, message = "Order not found" });
            }
            if (status == "Preparing")
            {
                orderDetail.Order.Status = "Preparing";
            }
            orderDetail.ProductStatus = status;
            _db.SaveChanges();

            return Json(new { success = true, message = "Order status updated successfully" });
        }

        [HasPermission("Kitchen User List")]
        public ActionResult UserList(String search)
        {
            var selectedBranch = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var users = _db.Users
            .Where(u => _db.UserBranchPermissions
                .Any(ubp => ubp.UserId == u.UserId && ubp.BranchId == selectedBranch) && !u.HasWarehousePermission)
            .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                users = users.Where(c => c.UserName.Contains(search));
            }
            return View(users.ToList());
        }
        [HasPermission("Kitchen User Create")]
        public ActionResult CreateUser()
        {
            ViewBag.Roles = _db.Roles.Where(r => r.IsBranchRole);
            return View();
        }

        [HttpPost]
        [HasPermission("Kitchen User Create")]
        public ActionResult CreateUser(User user, String ConfirmPassword, HttpPostedFileBase ProfileImage)
        {
            try
            {
                if (user.Password != ConfirmPassword)
                {
                    ModelState.AddModelError("", "Password and Confirm Password do not match.");
                    TempData["ErrorMessage"] = "Password and Confirm Password do not match.";
                    return View(user);
                }
                string fileName = null;

                if (ProfileImage != null && ProfileImage.ContentLength > 0)
                {
                    // Generate a unique file name to prevent overwriting
                    fileName = Guid.NewGuid() + Path.GetExtension(ProfileImage.FileName);

                    // Define the path to save the file
                    string uploadsFolder = Server.MapPath("~/Uploads/ProfileImages");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string filePath = Path.Combine(uploadsFolder, fileName);

                    // Save the file to the server
                    ProfileImage.SaveAs(filePath);
                }
                user.ProfileImage = fileName;
                user.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                user.CreatedAt = DateTime.Now;
                _db.Users.Add(user);
                _db.SaveChanges();

                var userBranchPermission = new UserBranchPermission
                {
                    UserId = user.UserId,
                    BranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"))
                };
                _db.UserBranchPermissions.Add(userBranchPermission);
                _db.SaveChanges();
                

                TempData["SuccessMessage"] = "User created successfully!";
                return RedirectToAction("UserList");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while creating the user: " + ex.Message);
            }

            TempData["ErrorMessage"] = "An error occurred while creating the user";
            return View(user);
        }

        [HasPermission("Kitchen User Edit")]
        public ActionResult EditUser(int userId)
        {
            var user = _db.Users.Find(userId);
            if (user == null)
            {
                return HttpNotFound();
            }
            ViewBag.Roles = _db.Roles.Where(r => r.IsBranchRole).ToList();
            return View(user);
        }
        [HttpPost]
        [HasPermission("Kitchen User Edit")]
        public ActionResult EditUser(User user, String ConfirmPassword, HttpPostedFileBase ProfileImage)
        {
            try
            {
                var existingUser = _db.Users.Find(user.UserId);
                if (existingUser == null)
                {
                    return HttpNotFound();
                }
                var existingImageName = existingUser.ProfileImage;
                ConfirmPassword = ConfirmPassword == "" ? null : ConfirmPassword;

                if (user.Password != ConfirmPassword)
                {
                    ModelState.AddModelError("", "Password and Confirm Password do not match.");
                    TempData["ErrorMessage"] = "Password and Confirm Password do not match.";
                    return View(user);
                }

                user.Email = existingUser.Email;
                user.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                user.ModifiedAt = DateTime.Now;
                _db.Entry(existingUser).CurrentValues.SetValues(user);
                _db.Entry(existingUser).Property(x => x.CreatedBy).IsModified = false;
                _db.Entry(existingUser).Property(x => x.CreatedAt).IsModified = false;

                if (ProfileImage != null && ProfileImage.ContentLength > 0)
                {
                    string uploadsFolder = Server.MapPath("~/Uploads/ProfileImages");

                    if (!string.IsNullOrEmpty(existingImageName))
                    {
                        string existingFilePath = Path.Combine(uploadsFolder, existingImageName);

                        // Delete the existing file if it exists
                        if (System.IO.File.Exists(existingFilePath))
                        {
                            System.IO.File.Delete(existingFilePath);
                        }
                    }
                    string fileName = Guid.NewGuid() + Path.GetExtension(ProfileImage.FileName);

                    // Define the path to save the file
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string filePath = Path.Combine(uploadsFolder, fileName);

                    // Save the file to the server
                    ProfileImage.SaveAs(filePath);
                    existingUser.ProfileImage = fileName;

                    //Checking if the user is current user and updating the profile image in session
                    if (user.UserId == Convert.ToInt32(Helper.GetUserInfo("userId")))
                    {
                        AccountController.UpdateProfileImageClaim(this.HttpContext, fileName);
                    }
                }
                else
                {
                    _db.Entry(existingUser).Property(x => x.ProfileImage).IsModified = false;
                }

                if (user.Password == null)
                {
                    _db.Entry(existingUser).Property(x => x.Password).IsModified = false;
                }
                _db.Entry(existingUser).State = EntityState.Modified;
                _db.SaveChanges();

                TempData["SuccessMessage"] = "User updated successfully!";
                return RedirectToAction("UserList");
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var validationErrors in ex.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        System.Diagnostics.Debug.WriteLine($"Property: {validationError.PropertyName} Error: {validationError.ErrorMessage}");
                    }
                }
                ModelState.AddModelError("", "An error occurred while updating the user: " + ex.Message);
            }
            TempData["ErrorMessage"] = "An error occurred while updating the user";
            return View(user);
        }
        [HttpPost]
        [HasPermission("Kitchen User Delete")]
        public ActionResult DeleteUser(int userId)
        {
            var user = _db.Users.Find(userId);
            _db.Users.Remove(user);
            _db.SaveChanges();
            return RedirectToAction("UserList");
        }
        [HttpPost]
        [HasPermission("Kitchen User Delete")]
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
        public ActionResult AccountSetting()
        {
            // Retrieve the logged-in user ID from the session
            int loggedInUserId = Convert.ToInt32(Helper.GetUserInfo("userId"));

            // Fetch the user details from the database
            var user = _db.Users.SingleOrDefault(u => u.UserId == loggedInUserId);
            if (user == null)
            {
                return HttpNotFound("User not found");
            }

            return View(user);
        }
        [HttpPost]
        public ActionResult AccountSetting(User model, string NewPassword, string ConfirmPassword)
        {
            // Retrieve the logged-in user ID from the session
            int loggedInUserId = Convert.ToInt32(Helper.GetUserInfo("userId"));

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid data submitted.";
                return View(model);
            }

            // Fetch the user from the database
            var user = _db.Users.SingleOrDefault(u => u.UserId == loggedInUserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("AccountSetting");
            }

            // Verify current password
            if (user.Password != model.Password) // Ensure to hash passwords in production
            {
                TempData["ErrorMessage"] = "Current password is incorrect.";
                return View(model);
            }

            // Update password if new password and confirmation match
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                if (NewPassword == ConfirmPassword)
                {
                    user.Password = NewPassword; // Hash the password in production
                }
                else
                {
                    TempData["ErrorMessage"] = "New Password and Confirm New Password do not match.";
                    return View(model);
                }
            }

            // Update other fields
            user.UserName = model.UserName;
            user.ModifiedBy = loggedInUserId;
            user.ModifiedAt = DateTime.UtcNow;

            _db.SaveChanges();

            TempData["SuccessMessage"] = "Account settings updated successfully.";
            return RedirectToAction("Index");
        }
        [HasPermission("Kitchen Role List")]
        public ActionResult RoleList(String search)
        {
            var roles = _db.Roles.Where(r => r.IsBranchRole).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                roles = roles.Where(c => c.RoleName.Contains(search));
            }
            return View(roles.ToList());
        }
        [HasPermission("Kitchen Role Create")]
        public ActionResult CreateRole()
        {
            var model = new RoleViewModel
            {
                Role = new Role(),
                PermissionsCategories = _db.PermissionsCategories.Where(p => p.PermissionModule == "Kitchen").Include("Permissions").ToList()
            };
            return View(model);
        }
        [HttpPost]
        [HasPermission("Kitchen Role Create")]
        public ActionResult CreateRole(RoleViewModel model)
        {
            if (ModelState.IsValid)
            {
                model.Role.CreatedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                model.Role.CreatedAt = DateTime.Now;
                model.Role.IsBranchRole = true;
                _db.Roles.Add(model.Role);
                _db.SaveChanges();

                foreach (var permissionId in model.SelectedPermissions)
                {
                    var rolePermission = new RolePermission
                    {
                        RoleId = model.Role.RoleId,
                        PermissionId = permissionId
                    };
                    _db.RolePermissions.Add(rolePermission);
                }
                _db.SaveChanges();

                TempData["SuccessMessage"] = "Role created successfully.";
                return RedirectToAction("RoleList");
            }

            model.PermissionsCategories = _db.PermissionsCategories.Where(p => p.PermissionModule == "Kitchen").Include("Permissions").ToList(); // Re-populate permissions in case of error
            return View(model);
        }

        [HasPermission("Kitchen Role Edit")]
        public ActionResult EditRole(int roleId)
        {
            var role = _db.Roles.Include(r => r.RolePermissions).FirstOrDefault(r => r.RoleId == roleId);
            if (role == null)
            {
                return HttpNotFound();
            }

            var model = new RoleViewModel
            {
                Role = role,
                PermissionsCategories = _db.PermissionsCategories.Where(p => p.PermissionModule == "Kitchen").Include("Permissions").ToList(),
                SelectedPermissions = role.RolePermissions.Select(rp => rp.PermissionId).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [HasPermission("Kitchen Role Edit")]
        public ActionResult EditRole(RoleViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existingRole = _db.Roles.Include(r => r.RolePermissions).FirstOrDefault(r => r.RoleId == model.Role.RoleId);
                if (existingRole == null)
                {
                    return HttpNotFound();
                }

                existingRole.RoleName = model.Role.RoleName;
                existingRole.ModifiedBy = Convert.ToInt32(Helper.GetUserInfo("userId"));
                existingRole.ModifiedAt = DateTime.Now;

                // Update role permissions
                var existingPermissions = existingRole.RolePermissions.ToList();
                foreach (var permission in existingPermissions)
                {
                    _db.RolePermissions.Remove(permission);
                }

                foreach (var permissionId in model.SelectedPermissions)
                {
                    var rolePermission = new RolePermission
                    {
                        RoleId = existingRole.RoleId,
                        PermissionId = permissionId
                    };
                    _db.RolePermissions.Add(rolePermission);
                }

                _db.SaveChanges();
                TempData["SuccessMessage"] = "Role updated successfully.";
                return RedirectToAction("RoleList");
            }

            model.PermissionsCategories = _db.PermissionsCategories.Where(p => p.PermissionModule == "Kitchen").Include("Permissions").ToList(); // Re-populate permissions in case of error
            return View(model);
        }

        [HttpPost]
        [HasPermission("Kitchen Role Delete")]
        public ActionResult DeleteRole(int RoleId)
        {
            try
            {
                var Role = _db.Roles.Find(RoleId);
                if (Role == null)
                {
                    TempData["ErrorMessage"] = "Role not found.";
                    return RedirectToAction("RoleList");
                }

                _db.Roles.Remove(Role);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Role deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                {
                    TempData["ErrorMessage"] = "This Role is already in use and cannot be deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the Role.";
                }
            }
            return RedirectToAction("RoleList");
        }
        [HttpPost]
        [HasPermission("Kitchen Role Delete")]
        public ActionResult DeleteSelectedRoles(int[] selectedItems)
        {
            if (selectedItems != null && selectedItems.Length > 0)
            {
                try
                {
                    var itemsToDelete = _db.Roles.Where(c => selectedItems.Contains(c.RoleId)).ToList();
                    _db.Roles.RemoveRange(itemsToDelete);
                    _db.SaveChanges();
                    TempData["SuccessMessage"] = "Role deleted successfully.";
                }
                catch (DbUpdateException ex)
                {
                    if (ex.InnerException?.InnerException is SqlException sqlEx && sqlEx.Number == 547) // SQL error code for foreign key constraint
                    {
                        TempData["ErrorMessage"] = "A Role is already in use and cannot be deleted.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "An error occurred while deleting the Role.";
                    }
                }
            }
            return RedirectToAction("RoleList");
        }



        [ChildActionOnly]
        public ActionResult BranchesDropdown()
        {
            var branches = _db.Branches.Where(b => b.IsActive).ToList();

            // Filter branches based on user access
            var accessibleBranches = branches.Where(b => Helper.HasBranchAccess(b.BranchId)).ToList();

            // Get the selected branchId from claims
            var branchIdClaim = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            if (Helper.HasBranchAccess(branchIdClaim))
            {
                ViewBag.SelectedBranchId = branchIdClaim;
            }
            else if (accessibleBranches.Count > 0)
            {
                SetBranchClaim(accessibleBranches[0].BranchId);
                return PartialView("_RedirectToIndex");
            }
            else
            {
                return RedirectToAction("Unauthorized", "Accounts");
            }
            return PartialView("_BranchesDropdown", accessibleBranches);
        }

        [HttpPost]
        public ActionResult SetBranchClaim(int branchId)
        {
            var claimsIdentity = User.Identity as ClaimsIdentity;

            // Remove existing branch claim if exists
            var existingClaim = claimsIdentity?.FindFirst("BranchId");
            if (existingClaim != null)
            {
                claimsIdentity.RemoveClaim(existingClaim);
            }

            // Add the new branch claim
            claimsIdentity?.AddClaim(new Claim("BranchId", branchId.ToString()));
            Session["LastSelectedBranchId"] = branchId;

            // Update the authentication cookie
            var ctx = Request.GetOwinContext();
            var authenticationManager = ctx.Authentication;
            authenticationManager.AuthenticationResponseGrant = new AuthenticationResponseGrant(
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties() { IsPersistent = true }
            );

            return Json(new { success = true });
        }

        public ActionResult ItemStockReport()
        {
            var BranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
            var Branch = _db.Branches.Find(BranchId);
            ViewBag.BranchName = Branch.BranchName;
            ViewBag.Categories = new SelectList(_db.ItemCategories, "ItemCategoryId", "ItemCategoryName");
            return View();
        }

        [HttpGet]
        public ActionResult GenerateBranchItemStockReport(int categoryId, string status, string searchTerm, string sortBy)
        {
            try
            {
                var data = GetBranchItemStockReportData(categoryId, status, searchTerm, sortBy);
                var BranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                var Branch = _db.Branches.Find(BranchId);

                ViewBag.CategoryName = categoryId > 0 
                    ? _db.ItemCategories.FirstOrDefault(c => c.ItemCategoryId == categoryId)?.ItemCategoryName 
                    : "All Categories";

                ViewBag.BranchName = Branch.BranchName;
                ViewBag.Status = status == "active" ? "Active" : status == "inactive" ? "Inactive" : "All";
                ViewBag.SearchTerm = searchTerm;
                ViewBag.SortBy = sortBy;
                ViewBag.CategoryId = categoryId;
                ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                
                return View("BranchItemStockReportDisplay", data);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while generating the report: " + ex.Message;
                return RedirectToAction("ItemStockReport");
            }
        }

        [HttpGet]
        public ActionResult ExportBranchItemStockReportToPDF(int categoryId, string status, string searchTerm, string sortBy)
        {
            try
            {
                var data = GetBranchItemStockReportData(categoryId, status, searchTerm, sortBy);
                var BranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                var Branch = _db.Branches.Find(BranchId);

                ViewBag.CategoryName = categoryId > 0 
                    ? _db.ItemCategories.FirstOrDefault(c => c.ItemCategoryId == categoryId)?.ItemCategoryName 
                    : "All Categories";

                ViewBag.BranchName = Branch.BranchName;
                ViewBag.Status = status == "active" ? "Active" : status == "inactive" ? "Inactive" : "All";
                ViewBag.SearchTerm = searchTerm;
                ViewBag.SortBy = sortBy;
                ViewBag.CategoryId = categoryId;
                ViewBag.GeneratedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ViewBag.IsPdfExport = true;
                
                // Convert view to PDF
                var fileName = $"Branch_Item_Stock_Report_{DateTime.Now:yyyyMMdd}";
                return new Rotativa.ViewAsPdf("BranchItemStockReportDisplay", data)
                {
                    FileName = fileName + ".pdf",
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Landscape,
                    PageMargins = new Rotativa.Options.Margins(5, 5, 5, 5)
                };
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while exporting the report to PDF: " + ex.Message;
                return RedirectToAction("ItemStockReport");
            }
        }

        [HttpGet]
        public ActionResult ExportBranchItemStockReportToExcel(int categoryId, string status, string searchTerm, string sortBy)
        {
            try
            {
                // Get report data
                var data = GetBranchItemStockReportData(categoryId, status, searchTerm, sortBy);
                var BranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                var Branch = _db.Branches.Find(BranchId);
                
                // Set category name
                string categoryName = "All Categories";
                string fileName = "All_Categories";

                if (categoryId > 0)
                {
                    var category = _db.ItemCategories.Find(categoryId);
                    if (category == null)
                    {
                        TempData["ErrorMessage"] = "Category not found.";
                        return RedirectToAction("ItemStockReport");
                    }
                    categoryName = category.ItemCategoryName;
                    fileName = category.ItemCategoryName.Replace(" ", "_");
                }
                
                // Create Excel package
                ExcelPackage.License.SetNonCommercialOrganization("Ressential");
                using (var package = new ExcelPackage())
                {
                    // Add a worksheet
                    var worksheet = package.Workbook.Worksheets.Add("Branch Item Stock Report");
                    
                    // Set document properties
                    package.Workbook.Properties.Title = "Branch Item Stock Report";
                    package.Workbook.Properties.Author = "Ressential";
                    package.Workbook.Properties.Company = "Ressential";
                    package.Workbook.Properties.Created = DateTime.Now;
                    
                    // Professional color scheme
                    var headerBackground = System.Drawing.Color.FromArgb(31, 78, 120);   // Dark blue
                    var titleBackground = System.Drawing.Color.FromArgb(55, 125, 185);   // Medium blue
                    var alternateRowColor = System.Drawing.Color.FromArgb(240, 244, 248); // Very light blue
                    var totalRowColor = System.Drawing.Color.FromArgb(217, 225, 242);    // Light blue-gray
                    var borderColor = System.Drawing.Color.FromArgb(180, 199, 231);      // Light blue for borders
                    
                    // Set column widths
                    worksheet.Column(1).Width = 6;      // S.No
                    worksheet.Column(2).Width = 15;     // Item Code
                    worksheet.Column(3).Width = 30;     // Item Name
                    worksheet.Column(4).Width = 20;     // Category
                    worksheet.Column(5).Width = 10;     // Unit
                    worksheet.Column(6).Width = 15;     // Current Stock
                    worksheet.Column(7).Width = 15;     // Min Stock
                    worksheet.Column(8).Width = 15;     // Price
                    worksheet.Column(9).Width = 15;     // Stock Value
                    worksheet.Column(10).Width = 15;    // Stock Status
                    worksheet.Column(11).Width = 15;    // Item Status
                    
                    // Add company logo placeholder and title (row 1)
                    worksheet.Cells[1, 1, 1, 11].Merge = true;
                    worksheet.Cells[1, 1].Value = "RESSENTIAL";
                    worksheet.Cells[1, 1].Style.Font.Size = 20;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[1, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    worksheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(headerBackground);
                    worksheet.Row(1).Height = 30; // Increase row height
                    
                    // Add report title (row 2)
                    worksheet.Cells[2, 1, 2, 11].Merge = true;
                    worksheet.Cells[2, 1].Value = "Branch Item Stock Report";
                    worksheet.Cells[2, 1].Style.Font.Size = 16;
                    worksheet.Cells[2, 1].Style.Font.Bold = true;
                    worksheet.Cells[2, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[2, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[2, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    worksheet.Cells[2, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[2, 1].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Row(2).Height = 25; // Increase row height
                    
                    // Empty row for spacing
                    worksheet.Row(3).Height = 10;
                    
                    // Report information section with styled boxes (rows 4-5)
                    // Branch info box
                    worksheet.Cells[4, 1, 4, 2].Merge = true;
                    worksheet.Cells[4, 1].Value = "BRANCH";
                    worksheet.Cells[4, 1].Style.Font.Bold = true;
                    worksheet.Cells[4, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 1].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, 1, 5, 2].Merge = true;
                    worksheet.Cells[5, 1].Value = Branch.BranchName;
                    worksheet.Cells[5, 1].Style.Font.Size = 11;
                    worksheet.Cells[5, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 1].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Category info box
                    worksheet.Cells[4, 3, 4, 5].Merge = true;
                    worksheet.Cells[4, 3].Value = "CATEGORY";
                    worksheet.Cells[4, 3].Style.Font.Bold = true;
                    worksheet.Cells[4, 3].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 3].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 3].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 3].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, 3, 5, 5].Merge = true;
                    worksheet.Cells[5, 3].Value = categoryName;
                    worksheet.Cells[5, 3].Style.Font.Size = 11;
                    worksheet.Cells[5, 3].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 3].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 3].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Status and search term box
                    worksheet.Cells[4, 6, 4, 8].Merge = true;
                    worksheet.Cells[4, 6].Value = status == "active" ? "ACTIVE ITEMS" : status == "inactive" ? "INACTIVE ITEMS" : "ALL ITEMS";
                    worksheet.Cells[4, 6].Style.Font.Bold = true;
                    worksheet.Cells[4, 6].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 6].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 6].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 6].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, 6, 5, 8].Merge = true;
                    worksheet.Cells[5, 6].Value = string.IsNullOrEmpty(searchTerm) ? "No Search Term" : searchTerm;
                    worksheet.Cells[5, 6].Style.Font.Size = 11;
                    worksheet.Cells[5, 6].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 6].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 6].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Date generated box
                    worksheet.Cells[4, 9, 4, 11].Merge = true;
                    worksheet.Cells[4, 9].Value = "GENERATED ON";
                    worksheet.Cells[4, 9].Style.Font.Bold = true;
                    worksheet.Cells[4, 9].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[4, 9].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[4, 9].Style.Fill.BackgroundColor.SetColor(titleBackground);
                    worksheet.Cells[4, 9].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    
                    worksheet.Cells[5, 9, 5, 11].Merge = true;
                    worksheet.Cells[5, 9].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[5, 9].Style.Font.Size = 11;
                    worksheet.Cells[5, 9].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    worksheet.Cells[5, 9].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Cells[5, 9].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                    
                    // Space before data
                    worksheet.Row(6).Height = 15;
                    
                    // Add table headers (row 7)
                    string[] headers = new string[] { 
                        "S.No", "Item Code", "Item Name", "Category", "Unit", 
                        "Current Stock", "Reorder Level", "Cost Per Unit", "Stock Value", "Stock Status", "Item Status" 
                    };
                    
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cells[7, i + 1];
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(headerBackground);
                        cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                        cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                    }
                    worksheet.Row(7).Height = 22; // Increase header row height
                    
                    // Freeze panes for better navigation (fix header row)
                    worksheet.View.FreezePanes(8, 1);
                    
                    // Add data rows
                    int currentRow = 8;
                        int serialNo = 1;
                        decimal totalStockValue = 0;
                        
                        foreach (var item in data)
                        {
                        // Apply alternating row background for better readability
                        if (serialNo % 2 == 0)
                        {
                            var range = worksheet.Cells[currentRow, 1, currentRow, 11];
                            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            range.Style.Fill.BackgroundColor.SetColor(alternateRowColor);
                        }
                        
                        // Calculate stock value
                            decimal stockValue = item.Stock * item.Price;
                            totalStockValue += stockValue;
                            
                        // Determine stock status
                            string stockStatus = "";
                            
                            if (item.Stock <= 0)
                            {
                                stockStatus = "Out of Stock";
                            }
                            else if (item.Stock < item.MinStock)
                            {
                                stockStatus = "Low Stock";
                            }
                            else
                            {
                                stockStatus = "Good";
                            }
                            
                        // Add row data
                        worksheet.Cells[currentRow, 1].Value = serialNo++;
                        worksheet.Cells[currentRow, 2].Value = item.ItemCode;
                        worksheet.Cells[currentRow, 3].Value = item.ItemName;
                        worksheet.Cells[currentRow, 4].Value = item.CategoryName;
                        worksheet.Cells[currentRow, 5].Value = item.Unit;
                        worksheet.Cells[currentRow, 6].Value = item.Stock;
                        worksheet.Cells[currentRow, 7].Value = item.MinStock;
                        worksheet.Cells[currentRow, 8].Value = item.Price;
                        worksheet.Cells[currentRow, 9].Value = stockValue;
                        worksheet.Cells[currentRow, 10].Value = stockStatus;
                        worksheet.Cells[currentRow, 11].Value = item.IsActive ? "Active" : "Inactive";
                        
                        // Format cells
                        for (int i = 1; i <= 11; i++)
                        {
                            worksheet.Cells[currentRow, i].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                        }
                        
                        // Numeric format for quantities and price
                        worksheet.Cells[currentRow, 6].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[currentRow, 7].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[currentRow, 8].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[currentRow, 9].Style.Numberformat.Format = "#,##0.00";
                        
                        // Apply color to stock status cell based on status
                            if (stockStatus == "Out of Stock")
                            {
                            worksheet.Cells[currentRow, 10].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[currentRow, 10].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(231, 76, 60));
                            worksheet.Cells[currentRow, 10].Style.Font.Color.SetColor(System.Drawing.Color.White);
                            }
                            else if (stockStatus == "Low Stock")
                            {
                            worksheet.Cells[currentRow, 10].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[currentRow, 10].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(243, 156, 18));
                            worksheet.Cells[currentRow, 10].Style.Font.Color.SetColor(System.Drawing.Color.White);
                            }
                            else
                            {
                            worksheet.Cells[currentRow, 10].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[currentRow, 10].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(46, 204, 113));
                            worksheet.Cells[currentRow, 10].Style.Font.Color.SetColor(System.Drawing.Color.White);
                            }
                            
                            // Apply color to item status cell
                            if (item.IsActive)
                            {
                            worksheet.Cells[currentRow, 11].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[currentRow, 11].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(39, 174, 96));
                            worksheet.Cells[currentRow, 11].Style.Font.Color.SetColor(System.Drawing.Color.White);
                            }
                            else
                            {
                            worksheet.Cells[currentRow, 11].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[currentRow, 11].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(192, 57, 43));
                            worksheet.Cells[currentRow, 11].Style.Font.Color.SetColor(System.Drawing.Color.White);
                        }
                        
                        currentRow++;
                    }
                    
                    // Add summary row
                    if (data.Any())
                    {
                        // Total row style
                        var totalRange = worksheet.Cells[currentRow, 1, currentRow, 11];
                        totalRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        totalRange.Style.Fill.BackgroundColor.SetColor(totalRowColor);
                        totalRange.Style.Font.Bold = true;
                        totalRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                        
                        // Total row content
                        worksheet.Cells[currentRow, 1, currentRow, 8].Merge = true;
                        worksheet.Cells[currentRow, 1].Value = "TOTAL STOCK VALUE:";
                        worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                        
                        worksheet.Cells[currentRow, 9].Value = totalStockValue;
                        worksheet.Cells[currentRow, 9].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[currentRow, 9, currentRow, 11].Merge = true;
                        worksheet.Cells[currentRow, 9].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                        
                        // Add another row with item count
                        currentRow++;
                        var countRange = worksheet.Cells[currentRow, 1, currentRow, 11];
                        countRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        countRange.Style.Fill.BackgroundColor.SetColor(alternateRowColor);
                        countRange.Style.Font.Bold = true;
                        countRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin, borderColor);
                        
                        worksheet.Cells[currentRow, 1, currentRow, 11].Merge = true;
                        worksheet.Cells[currentRow, 1].Value = $"Total Items: {data.Count()}";
                        worksheet.Cells[currentRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    }
                    
                    // Return the Excel file
                    byte[] excelBytes = package.GetAsByteArray();
                    return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Branch_Item_Stock_Report_{Branch.BranchName.Replace(" ", "_")}_{fileName}_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating Excel: {ex.Message}";
                return RedirectToAction("ItemStockReport");
            }
        }

        private List<BranchItemStockReportViewModel> GetBranchItemStockReportData(int categoryId, string status, string searchTerm, string sortBy)
        {
            try
            {
                var BranchId = Convert.ToInt32(Helper.GetUserInfo("branchId"));
                
                // First, query the branch items with status filtering
                var branchItems = _db.BranchItems.Where(bi => bi.BranchId == BranchId);
                
                // Apply status filter
                if (status == "active")
                {
                    branchItems = branchItems.Where(bi => bi.IsActive);
                }
                else if (status == "inactive")
                {
                    branchItems = branchItems.Where(bi => !bi.IsActive);
                }
                
                // Now project to view model
                var query = branchItems.Select(bi => new BranchItemStockReportViewModel
                {
                    BranchItemId = bi.BranchItemId,
                    ItemId = bi.ItemId,
                    ItemCode = bi.Item.Sku,
                    ItemName = bi.Item.ItemName,
                    CategoryId = bi.Item.ItemCategoryId,
                    CategoryName = bi.Item.ItemCategory.ItemCategoryName,
                    BranchId = bi.BranchId,
                    Unit = bi.Item.UnitOfMeasure.Symbol,
                    Stock = bi.Quantity,
                    MinStock = bi.MinimumStockLevel,
                    Price = bi.CostPerUnit,
                    StockValue = bi.Quantity * bi.CostPerUnit,
                    IsActive = bi.IsActive
                });

                // Apply category filter
                if (categoryId > 0)
                {
                    query = query.Where(i => i.CategoryId == categoryId);
                }

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(i => i.ItemName.ToLower().Contains(searchTerm) || 
                                         i.ItemCode.ToLower().Contains(searchTerm) ||
                                         i.CategoryName.ToLower().Contains(searchTerm));
                }

                // Apply sorting
                switch (sortBy)
                {
                    case "name":
                        query = query.OrderBy(i => i.ItemName);
                        break;
                    case "code":
                        query = query.OrderBy(i => i.ItemCode);
                        break;
                    case "category":
                        query = query.OrderBy(i => i.CategoryName).ThenBy(i => i.ItemName);
                        break;
                    case "stock":
                        query = query.OrderByDescending(i => i.Stock);
                        break;
                    case "price":
                        query = query.OrderByDescending(i => i.Price);
                        break;
                    default:
                        query = query.OrderBy(i => i.ItemName);
                        break;
                }

                return query.ToList();
            }
            catch (Exception ex)
            {
                throw new Exception("Error retrieving branch item stock data: " + ex.Message);
            }
        }

        public JsonResult GetOrderReceiptData(int orderId)
        {
            try
            {
                var order = _db.Orders.Find(orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found" });
                }

                var items = order.OrderDetails.Select(od => new
                {
                    name = od.Product.ProductName,
                    quantity = od.ProductQuantity,
                    price = od.ProductPrice,
                    total = od.ProductQuantity * od.ProductPrice
                }).ToList();

                var subtotal = items.Sum(i => i.total);
                var deliveryCharges = order.OnlineOrderDetails.FirstOrDefault()?.DeliveryCharges??0;
                var grandTotal = subtotal + deliveryCharges;

                var contactNo = FormatPhoneNumber(order.Branch.BranchContact);

                var receiptData = new
                {
                    success = true,
                    branchLogo = Url.Content("~/Content/assets/images/logo/ressential-logo-small.png"),
                    branchName = order.Branch.BranchName,
                    branchAddress = order.Branch.Address,
                    branchPhone = contactNo,
                    orderNo = order.OrderNo,
                    orderDate = order.OrderDate.ToString("dd-MMM-yyyy"),
                    orderTime = order.OrderDate.ToString("HH:mm:ss"),
                    orderType = order.OrderType,
                    tableNo = order.TableNo,
                    staffName = order.CreatedBy != null ? _db.Users.Find(order.CreatedBy).UserName : "System",
                    customerName = order.OrderType == "Online" ? order.Customer.CustomerName : null,
                    customerContact = order.OrderType == "Online" ? order.Customer.ContactNo : null,
                    customerAddress = order.OrderType == "Online" ? order.OnlineOrderDetails.FirstOrDefault().Address : null,
                    items = items,
                    subtotal = subtotal,
                    deliveryCharges = deliveryCharges,
                    grandTotal = grandTotal,
                    paymentMethod = order.PaymentMethod,
                    qrCode = Url.Content("~/Content/assets/images/qr-code.png")
                };

                return Json(receiptData, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error generating receipt: " + ex.Message });
            }
        }
        
    }
}