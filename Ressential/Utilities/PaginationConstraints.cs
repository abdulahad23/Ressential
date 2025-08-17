﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public static class PaginationConstraints
    {
        public static int CustomerShopPageSize { get; set; } = 30;
        public static int ChefViewPageSize { get; set; } = 30;
        public static int ChefViewMaxPage { get; set; } = 10;
    }
}