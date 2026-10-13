using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyRentalPortal.Web.Data.Entities;
using PropertyRentalPortal.Web.Data.Seeding;
using PropertyRentalPortal.Web.Models;

namespace PropertyRentalPortal.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login credentials.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var isManager = await _userManager.IsInRoleAsync(user, DatabaseSeeder.PropertyManagerRole);
            return isManager
                ? RedirectToAction("Index", "Properties")
                : RedirectToAction("Index", "Applications");
        }

        ModelState.AddModelError(string.Empty, "Invalid login credentials.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Validate allowed roles
        var assignedRole = model.Role == "PropertyManager"
            ? DatabaseSeeder.PropertyManagerRole
            : DatabaseSeeder.ApplicantRole;

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            // Ensure role exists and add user
            if (!await _roleManager.RoleExistsAsync(assignedRole))
            {
                await _roleManager.CreateAsync(new IdentityRole(assignedRole));
            }
            await _userManager.AddToRoleAsync(user, assignedRole);

            await _signInManager.SignInAsync(user, isPersistent: false);

            TempData["SuccessMessage"] = $"Welcome, {user.FullName}! Your account has been created as {assignedRole}.";

            return assignedRole == DatabaseSeeder.PropertyManagerRole
                ? RedirectToAction("Index", "Properties")
                : RedirectToAction("AvailableUnits", "Properties");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Quick 1-click login helper for seamless testing and demonstration of both roles.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickLogin(string role)
    {
        var email = role == "PropertyManager"
            ? DatabaseSeeder.DefaultManagerEmail
            : DatabaseSeeder.DefaultApplicantEmail;

        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["SuccessMessage"] = $"Switched demo session to {user.FullName} ({role}).";

            return role == "PropertyManager"
                ? RedirectToAction("Index", "Properties")
                : RedirectToAction("Index", "Applications");
        }

        TempData["ErrorMessage"] = "Demo account not found. Please restart the app to seed default accounts.";
        return RedirectToAction("Login");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
