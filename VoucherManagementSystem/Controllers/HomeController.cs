using Microsoft.AspNetCore.Mvc;
using VoucherManagementSystem.Interfaces;
using VoucherManagementSystem.Models;
using System.Diagnostics;

namespace VoucherManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IVoucherRepository _voucherRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IConfiguration configuration,
            IVoucherRepository voucherRepository,
            IProjectRepository projectRepository,
            ILogger<HomeController> logger)
        {
            _configuration = configuration;
            _voucherRepository = voucherRepository;
            _projectRepository = projectRepository;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalVouchers = (await _voucherRepository.GetAllAsync()).Count();
            ViewBag.ActiveProjects = (await _projectRepository.GetActiveProjectsAsync()).Count();

            var today = DateTime.Today;
            var todayVouchers = await _voucherRepository.GetVouchersByDateRangeAsync(today, today.AddDays(1));
            ViewBag.TodayTransactions = todayVouchers.Count();
            ViewBag.TodayAmount = todayVouchers.Sum(v => v.Amount);

            var recentVouchers = await _voucherRepository.GetVouchersWithDetailsAsync();
            ViewBag.RecentVouchers = recentVouchers.Take(10);

            return View();
        }

        [HttpGet]
        public IActionResult Login()
        {
            // If already logged in, redirect to home
            if (HttpContext.Session.GetString("IsLoggedIn") == "true")
            {
                return RedirectToAction("Index");
            }
            return View();
        }

        [HttpPost]
        public IActionResult DoLogin(string username, string password)
        {
            var adminUsername = _configuration["AdminCredentials:Username"];
            var adminPassword = _configuration["AdminCredentials:Password"];

            if (username == adminUsername && password == adminPassword)
            {
                HttpContext.Session.SetString("IsLoggedIn", "true");
                HttpContext.Session.SetString("Username", username);
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Invalid username or password" });
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
