using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityApiAuth.Controllers;
[Authorize]
public class HomeController : Controller
{
    public HomeController()
    {
        
    }

    [HttpGet]
    public IActionResult Index() => Content("Hello, user");
}