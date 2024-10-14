using System.Security.Claims;
using Hangfire;
using IdentityApiAuth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityApiAuth.Controllers;
[Authorize]
public class HomeController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    public HomeController(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                if (await _userManager.IsEmailConfirmedAsync(user))
                {
                    RecurringJob.AddOrUpdate<IEmailSender>(
                        (emailSender) =>  emailSender.SendEmailAsync(user.Email!, 
                            "This is test email", 
                            "This is test email triggered by hangfire", false),
                        Cron.Minutely);
                }
                return Content("Hello, user!");
            }
        }

        return RedirectToAction("Login", "Account");
    }
}