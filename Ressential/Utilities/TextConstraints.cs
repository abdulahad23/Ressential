using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Models
{
    public static class TextConstraints
    {
        public static string ProfileImagesPath { get; set; } = "~/Uploads/ProfileImages/";
        public static string ProductImagesPath { get; set; } = "~/Uploads/ProductImages/";
        public static string NoImagesPath { get; set; } = "~/Content/assets/no_image.png";
        public static string EmptyProfilePath { get; set; } = "~/Content/assets/empty_profile.jpg";

    }
}