using Microsoft.AspNetCore.Mvc;
using SC701.Models;

//comment: This is a simple HomeController for an ASP.NET Core MVC application.

namespace SC701.NewsIngestor.Controllers
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