﻿using Ressential.Models;
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
    }
}