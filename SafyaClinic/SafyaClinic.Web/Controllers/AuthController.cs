using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.Interfaces.Services;
using System.Security.Claims;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Web.Controllers;

public class AuthController : BaseController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) =>
        _authService = authService;

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _authService.LoginAsync(model);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Errors.First());
            return View(model);
        }

        var data = result.Data!;

        // Build claims identity from service response
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, data.UserId.ToString()),
            new(ClaimTypes.Name, data.FullName)
        };
        claims.AddRange(data.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        if (data.MustChangePassword)
            claims.Add(new Claim(AuthClaims.MustChangePassword, "true"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = data.ExpiresAt
            });

        if (data.MustChangePassword)
            return RedirectToAction(nameof(ChangePassword));

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword() => View();

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _authService.ChangePasswordAsync(CurrentUserId, model);
        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            return View(model);
        }

        // Re-issue the sign-in cookie without the "must change password" marker.
        if (User.HasClaim(AuthClaims.MustChangePassword, "true"))
        {
            var identity = new ClaimsIdentity(
                User.Claims.Where(c => c.Type != AuthClaims.MustChangePassword),
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
        }

        // Re-issue the sign-in cookie without the "must change password" marker.
        if (User.HasClaim(AuthClaims.MustChangePassword, "true"))
        {
            var identity = new ClaimsIdentity(
                User.Claims.Where(c => c.Type != AuthClaims.MustChangePassword),
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
        }

        return RedirectWithSuccess("Password changed successfully.", "Index", "Dashboard");
    }
}