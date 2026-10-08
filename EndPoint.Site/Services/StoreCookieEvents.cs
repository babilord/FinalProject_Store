using System.Security.Claims;
using FinalProject_Store.Application.Interfaces.Contexts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace EndPoint.Site.Services;

public sealed class StoreCookieEvents(IDataBaseContext context) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext validation)
    {
        if (!long.TryParse(validation.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !await context.Users.AnyAsync(x => x.Id == userId && x.isActive, validation.HttpContext.RequestAborted))
        {
            validation.RejectPrincipal();
            await validation.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }
        var roles = await context.UserInRoles.Where(x => x.UserId == userId).Select(x => x.Role.Name)
            .Distinct().ToListAsync(validation.HttpContext.RequestAborted);
        var identity = (ClaimsIdentity)validation.Principal!.Identity!;
        if (!roles.Order().SequenceEqual(identity.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct().Order()))
        {
            foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList()) identity.RemoveClaim(claim);
            foreach (var role in roles) identity.AddClaim(new Claim(ClaimTypes.Role, role));
            validation.ShouldRenew = true;
        }
    }
}
