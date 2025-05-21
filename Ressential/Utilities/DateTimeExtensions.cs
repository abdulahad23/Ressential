using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ressential.Utilities
{
    public static class DateTimeExtensions
    {
        public static string ToRelativeTime(this DateTime dateTime)
        {
            var timeSpan = DateTime.Now.Subtract(dateTime);

            if (timeSpan <= TimeSpan.FromSeconds(60))
                return $"{timeSpan.Seconds} sec ago";
            else if (timeSpan <= TimeSpan.FromMinutes(60))
                return timeSpan.Minutes > 1 ? $"{timeSpan.Minutes} min ago" : "1 min ago";
            else if (timeSpan <= TimeSpan.FromHours(24))
                return timeSpan.Hours > 1 ? $"{timeSpan.Hours} hr ago" : "1 hr ago";
            else if (timeSpan <= TimeSpan.FromDays(30))
                return timeSpan.Days > 1 ? $"{timeSpan.Days} days ago" : "1 day ago";
            else if (timeSpan <= TimeSpan.FromDays(365))
                return timeSpan.Days > 30 ? $"{timeSpan.Days / 30} months ago" : "1 month ago";
            else
                return timeSpan.Days > 365 ? $"{timeSpan.Days / 365} years ago" : "1 year ago";
        }
    }
}