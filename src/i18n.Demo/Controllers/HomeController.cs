using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using i18n.Demo.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Filters;

namespace i18n.Demo.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        readonly IWebHostEnvironment _webHostEnvironment;

        public HomeController(ILogger<HomeController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public async Task<IActionResult> DownloadPdf()
        {
            var pdfBytes = await System.IO.File.ReadAllBytesAsync(Path.Combine(_webHostEnvironment.WebRootPath, "assets", "sample.pdf"));
            return File(pdfBytes, "application/pdf", "sample.pdf");
        }

        public IActionResult Json()
        {
            return Json(new {Hello = "[[[Hello World]]]" });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
