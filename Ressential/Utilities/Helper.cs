using Ressential.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Web;

namespace Ressential.Utilities
{
    public class Helper
    {

        public static string GetUserInfo(string data)
        {
            var identity = (ClaimsPrincipal)Thread.CurrentPrincipal;
            string returnVal = string.Empty;
            switch (data)
            {
                case "userId":
                    returnVal = identity.Claims.Where(c => c.Type == ClaimTypes.Sid).Select(c => c.Value).SingleOrDefault();
                    break;
                case "userName":
                    returnVal = identity.Claims.Where(c => c.Type == ClaimTypes.Name).Select(c => c.Value).SingleOrDefault();
                    break;
                case "Email":
                    returnVal = identity.Claims.Where(c => c.Type == ClaimTypes.Email).Select(c => c.Value).SingleOrDefault();
                    break;
                case "isActive":
                    returnVal = identity.Claims.Where(c => c.Type.Equals("IsActive")).Select(c => c.Value).SingleOrDefault();
                    break;
                case "branchId":
                    returnVal = identity.Claims.Where(c => c.Type.Equals("BranchId")).Select(c => c.Value).SingleOrDefault();
                    break;
                case "profileImage":
                    returnVal = identity.Claims.Where(c => c.Type.Equals("ProfileImage")).Select(c => c.Value).SingleOrDefault();
                    break;
            }
            return returnVal;


        }

        public static List<string> GetPermissions()
        {
            var identity = (ClaimsPrincipal)Thread.CurrentPrincipal;
            string permissionsString = identity.Claims.Where(c => c.Type.Equals("Permissions")).Select(c => c.Value).SingleOrDefault();
            return permissionsString.Split(',').ToList();
        }
        public static List<int> GetBranchPermissions()
        {
            var identity = (ClaimsPrincipal)Thread.CurrentPrincipal;
            string branchPermissionsString = identity.Claims.Where(c => c.Type.Equals("BranchPermissions")).Select(c => c.Value).SingleOrDefault();

            if (string.IsNullOrEmpty(branchPermissionsString))
            {
                return new List<int>();
            }

            return branchPermissionsString.Split(',').Select(int.Parse).ToList();
        }

        public static bool HasBranchAccess(int branchId)
        {
            var branchPermissions = GetBranchPermissions();
            return branchPermissions.Contains(branchId);
        }
        public static bool HasKitchenModule()
        {
            var branchPermissions = GetBranchPermissions();
            branchPermissions = branchPermissions.Where(b => !b.Equals(0)).ToList();
            if (branchPermissions.Count > 0)
            {
                return true;
            }
            return false;
        }
    }
}