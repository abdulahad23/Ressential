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
    }
}