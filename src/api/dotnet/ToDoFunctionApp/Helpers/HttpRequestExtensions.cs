using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Security.Claims;

namespace AzninjaTodoFn.Helpers
{
     public static class HttpRequestExtensions
     {
          public static void Authorize(this HttpRequest http, string[] roles)
          {
               if (!AuthorizedRole(roles, http.HttpContext.User)) throw new UnauthorizedAccessException("Not authorized");
          }

          private static Boolean AuthorizedRole(string[] allowedRoles, ClaimsPrincipal user)
          {
               var _roles = user.Claims.Where(e => e.Type == "roles").Select(e => e.Value);
               return _roles.Intersect(allowedRoles).Any();
          }

          public static string UserName(this HttpRequest http) => http?.HttpContext?.User?.Identity?.Name;
     }
}