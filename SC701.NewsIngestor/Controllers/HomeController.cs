using Microsoft.AspNetCore.Mvc;
//comment: This is a simple HomeController for an ASP.NET Core MVC application.

namespace SC701_NewsIngestor.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}