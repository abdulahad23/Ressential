using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Ressential.Controllers
{
    public class WarehouseController : Controller
    {
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
    }
}