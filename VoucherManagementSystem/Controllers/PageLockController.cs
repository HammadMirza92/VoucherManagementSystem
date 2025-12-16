using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoucherManagementSystem.Data;
using VoucherManagementSystem.Models;

namespace VoucherManagementSystem.Controllers
{
    public class PageLockController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PageLockController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: PageLock/MasterLock
        public async Task<IActionResult> MasterLock()
        {
            // Check if master lock is authenticated in session
            var isMasterUnlocked = HttpContext.Session.GetString("MasterLockUnlocked");

            if (isMasterUnlocked != "true")
            {
                // Redirect to master lock authentication page
                return RedirectToAction("MasterLockAuth");
            }

            // Initialize page locks if not exists
            await InitializePageLocksAsync();
            await InitializeMasterPasswordAsync();

            var pageLocks = await _context.PageLocks.OrderBy(p => p.PageName).ToListAsync();
            return View(pageLocks);
        }

        // GET: PageLock/MasterLockAuth
        public IActionResult MasterLockAuth()
        {
            return View();
        }

        // POST: PageLock/VerifyMasterPassword
        [HttpPost]
        public async Task<IActionResult> VerifyMasterPassword(string password)
        {
            try
            {
                var masterPassword = await _context.MasterPasswords
                    .FirstOrDefaultAsync(mp => mp.PasswordType == "MasterLock");

                if (masterPassword == null)
                {
                    return Json(new { success = false, message = "Master password not configured" });
                }

                if (masterPassword.Password == password)
                {
                    // Store in session that master lock is unlocked
                    HttpContext.Session.SetString("MasterLockUnlocked", "true");
                    return Json(new { success = true });
                }

                return Json(new { success = false, message = "Incorrect password" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: PageLock/ToggleLock
        [HttpPost]
        public async Task<IActionResult> ToggleLock(int id)
        {
            try
            {
                var pageLock = await _context.PageLocks.FindAsync(id);
                if (pageLock == null)
                {
                    return Json(new { success = false, message = "Page lock not found" });
                }

                pageLock.IsLocked = !pageLock.IsLocked;
                pageLock.LastModifiedDate = DateTime.Now;
                pageLock.LastModifiedBy = HttpContext.Session.GetString("Username") ?? "admin";

                await _context.SaveChangesAsync();

                return Json(new { success = true, isLocked = pageLock.IsLocked });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: PageLock/UpdatePassword
        [HttpPost]
        public async Task<IActionResult> UpdatePassword(int id, string password)
        {
            try
            {
                var pageLock = await _context.PageLocks.FindAsync(id);
                if (pageLock == null)
                {
                    return Json(new { success = false, message = "Page lock not found" });
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    return Json(new { success = false, message = "Password cannot be empty" });
                }

                pageLock.Password = password;
                pageLock.LastModifiedDate = DateTime.Now;
                pageLock.LastModifiedBy = HttpContext.Session.GetString("Username") ?? "admin";

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Password updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: PageLock/VerifyPassword
        [HttpPost]
        public async Task<IActionResult> VerifyPassword(string pageUrl, string password)
        {
            try
            {
                var pageLock = await _context.PageLocks.FirstOrDefaultAsync(p => p.PageUrl == pageUrl);

                if (pageLock == null || !pageLock.IsLocked)
                {
                    return Json(new { success = true });
                }

                if (pageLock.Password == password)
                {
                    // Store in session that this page is unlocked for this session
                    HttpContext.Session.SetString($"PageUnlocked_{pageUrl}", "true");
                    return Json(new { success = true });
                }

                return Json(new { success = false, message = "Incorrect password" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Helper method to initialize default page locks
        private async Task InitializePageLocksAsync()
        {
            var defaultPages = new List<(string Name, string Url)>
            {
                ("Items", "/Items/Index"),
                ("Banks", "/Banks/Index"),
                ("Customers", "/Customers/Index"),
                ("Projects", "/Projects/Index"),
                ("Expense Heads", "/ExpenseHeads/Index"),
                ("All Vouchers", "/Vouchers/Index"),
                ("General Create", "/Vouchers/GeneralCreate"),
                ("Dashboard", "/Home/Index"),
                ("Reports", "/Reports/Index"),
                ("Customer Ledger", "/Reports/CustomerLedger"),
                ("Capital Report", "/Reports/CapitalReport"),
                ("Stock Report", "/Reports/StockReport"),
                ("Profit Loss", "/Reports/ProjectReport"),
                ("Cash Flow", "/Reports/CashFlow"),
                ("Bank Statement", "/Reports/BankStatement"),
                ("All Projects Report", "/Reports/AllProjectsReport"),
                ("All Customers Report", "/Reports/AllCustomersReport"),
                ("All Expenses Report", "/Reports/AllExpensesReport"),
                ("Master Lock", "/PageLock/MasterLock")
            };

            foreach (var (name, url) in defaultPages)
            {
                var exists = await _context.PageLocks.AnyAsync(p => p.PageUrl == url);
                if (!exists)
                {
                    _context.PageLocks.Add(new PageLock
                    {
                        PageName = name,
                        PageUrl = url,
                        IsLocked = false,
                        Password = "1234", // Default password
                        LastModifiedDate = DateTime.Now,
                        LastModifiedBy = "system"
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        // Helper method to initialize master password
        private async Task InitializeMasterPasswordAsync()
        {
            var masterPassword = await _context.MasterPasswords
                .FirstOrDefaultAsync(mp => mp.PasswordType == "MasterLock");

            if (masterPassword == null)
            {
                _context.MasterPasswords.Add(new MasterPassword
                {
                    PasswordType = "MasterLock",
                    Password = "admin123", // Default master password
                    LastModifiedDate = DateTime.Now,
                    LastModifiedBy = "system"
                });
                await _context.SaveChangesAsync();
            }
        }
    }
}
