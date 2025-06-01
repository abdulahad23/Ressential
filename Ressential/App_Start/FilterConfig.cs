using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Helpers;

namespace Ressential
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class CustomAntiForgeryAttribute : FilterAttribute, IAuthorizationFilter
    {
        private const string TokenFieldName = "__RequestVerificationToken";

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException("filterContext");
            }

            var httpContext = filterContext.HttpContext;
            var cookie = httpContext.Request.Cookies[System.Web.Security.FormsAuthentication.FormsCookieName];

            if (cookie != null)
            {
                var ticket = System.Web.Security.FormsAuthentication.Decrypt(cookie.Value);
                if (ticket != null)
                {
                    httpContext.User = new System.Security.Principal.GenericPrincipal(
                        new System.Security.Principal.GenericIdentity(ticket.Name, "Forms"),
                        new string[0]
                    );
                }
            }

            var request = filterContext.HttpContext.Request;

            // Only validate POST requests
            if (request.HttpMethod.ToUpperInvariant() == "POST")
            {
                // Get the tokens from the request
                var antiForgeryCookie = request.Cookies[AntiForgeryConfig.CookieName];
                var formToken = request.Form[TokenFieldName];

                // Validate the tokens
                if (antiForgeryCookie == null || string.IsNullOrEmpty(formToken))
                {
                    throw new HttpAntiForgeryException("Anti-forgery token validation failed: Missing token.");
                }

                try
                {
                    AntiForgery.Validate(antiForgeryCookie.Value, formToken);
                }
                catch (HttpAntiForgeryException)
                {
                    throw new HttpAntiForgeryException("Anti-forgery token validation failed: Invalid token.");
                }
            }
        }
    }
}
