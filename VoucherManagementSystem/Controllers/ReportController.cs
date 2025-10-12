using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VoucherManagementSystem.Data;
using VoucherManagementSystem.Interfaces;
using VoucherManagementSystem.Models;
using ClosedXML.Excel;

namespace VoucherManagementSystem.Controllers
{
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IVoucherRepository _voucherRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IBankRepository _bankRepository;
        private readonly IItemRepository _itemRepository;
        private readonly IExpenseHeadRepository _expenseHeadRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            ApplicationDbContext context,
            IVoucherRepository voucherRepository,
            IProjectRepository projectRepository,
            IBankRepository bankRepository,
            IItemRepository itemRepository,
            IExpenseHeadRepository expenseHeadRepository,
            ICustomerRepository customerRepository,
            ILogger<ReportsController> logger)
        {
            _context = context;
            _voucherRepository = voucherRepository;
            _projectRepository = projectRepository;
            _bankRepository = bankRepository;
            _itemRepository = itemRepository;
            _expenseHeadRepository = expenseHeadRepository;
            _customerRepository = customerRepository;
            _logger = logger;
        }

        // GET: Reports
        public async Task<IActionResult> Index()
        {
            try
            {
                ViewBag.Projects = new SelectList(await _projectRepository.GetActiveProjectsAsync(), "Id", "Name");
                ViewBag.Banks = new SelectList(await _bankRepository.GetActiveBanksAsync(), "Id", "Name");
                ViewBag.Items = new SelectList(await _itemRepository.GetActiveItemsAsync(), "Id", "Name");
                ViewBag.ExpenseHeads = new SelectList(await _expenseHeadRepository.GetActiveExpenseHeadsAsync(), "Id", "Name");

                // Statistics for dashboard
                ViewBag.TotalVouchers = (await _voucherRepository.GetAllAsync()).Count();
                ViewBag.ActiveProjects = (await _projectRepository.GetActiveProjectsAsync()).Count();
                ViewBag.TotalCustomers = (await _customerRepository.GetActiveCustomersAsync()).Count();
                ViewBag.TotalItems = (await _itemRepository.GetActiveItemsAsync()).Count();

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reports index");
                TempData["Error"] = "Error loading reports. Please try again.";
                return View();
            }
        }

        // POST: Reports/ProfitLoss
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfitLoss(int projectId, DateTime fromDate, DateTime toDate)
        {
            try
            {
                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project == null)
                {
                    TempData["Error"] = "Project not found.";
                    return RedirectToAction(nameof(Index));
                }

                var revenue = await _projectRepository.GetProjectRevenueAsync(projectId, fromDate, toDate);
                var expenses = await _projectRepository.GetProjectExpenseAsync(projectId, fromDate, toDate);
                var profitLoss = revenue - expenses;

                var vouchers = await _voucherRepository.GetVouchersByProjectAsync(projectId);
                vouchers = vouchers.Where(v => v.VoucherDate >= fromDate && v.VoucherDate <= toDate);

                ViewBag.Project = project;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.Revenue = revenue;
                ViewBag.Expenses = expenses;
                ViewBag.ProfitLoss = profitLoss;
                ViewBag.Vouchers = vouchers;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating profit/loss report");
                TempData["Error"] = "Error generating report. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Reports/StockReport
        //public async Task<IActionResult> StockReport()
        //{
        //    try
        //    {
        //        var items = await _context.Items
        //            .Where(i => i.StockTrackingEnabled && i.IsActive)
        //            .OrderBy(i => i.Name)
        //            .ToListAsync();

        //        return View(items);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error generating stock report");
        //        TempData["Error"] = "Error generating stock report.";
        //        return RedirectToAction(nameof(Index));
        //    }
        //}

        // GET: Reports/BankStatement
        public async Task<IActionResult> BankStatement(int bankId, DateTime fromDate, DateTime toDate)
        {
            try
            {
                var bank = await _bankRepository.GetByIdAsync(bankId);
                if (bank == null)
                {
                    TempData["Error"] = "Bank not found.";
                    return RedirectToAction(nameof(Index));
                }

                var transactions = await _bankRepository.GetBankTransactionsAsync(bankId, fromDate, toDate);

                ViewBag.Bank = bank;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.Transactions = transactions;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating bank statement");
                TempData["Error"] = "Error generating bank statement.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Reports/ExportToExcel
        public async Task<IActionResult> ExportToExcel(string reportType, int? id, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Report");

                if (reportType == "vouchers")
                {
                    var vouchers = await _voucherRepository.GetVouchersWithDetailsAsync();
                    if (fromDate.HasValue && toDate.HasValue)
                    {
                        vouchers = vouchers.Where(v => v.VoucherDate >= fromDate.Value && v.VoucherDate <= toDate.Value);
                    }

                    // Headers
                    worksheet.Cell(1, 1).Value = "Transaction No";
                    worksheet.Cell(1, 2).Value = "Type";
                    worksheet.Cell(1, 3).Value = "Date";
                    worksheet.Cell(1, 4).Value = "Amount";
                    worksheet.Cell(1, 5).Value = "Customer";
                    worksheet.Cell(1, 6).Value = "Project";

                    // Data
                    int row = 2;
                    foreach (var voucher in vouchers)
                    {
                        worksheet.Cell(row, 1).Value = voucher.TransactionNumber;
                        worksheet.Cell(row, 2).Value = voucher.VoucherType.ToString();
                        worksheet.Cell(row, 3).Value = voucher.VoucherDate.ToString("yyyy-MM-dd");
                        worksheet.Cell(row, 4).Value = voucher.Amount;
                        worksheet.Cell(row, 5).Value = voucher.PurchasingCustomer?.Name ?? voucher.ReceivingCustomer?.Name ?? "";
                        worksheet.Cell(row, 6).Value = voucher.Project?.Name ?? "";
                        row++;
                    }

                    // Format as table
                    var range = worksheet.Range(1, 1, row - 1, 6);
                    range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Row(1).Style.Font.Bold = true;
                    worksheet.Row(1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                else if (reportType == "stock")
                {
                    var items = await _itemRepository.GetItemsWithStockAsync();

                    // Headers
                    worksheet.Cell(1, 1).Value = "Item Name";
                    worksheet.Cell(1, 2).Value = "Unit";
                    worksheet.Cell(1, 3).Value = "Current Stock";
                    worksheet.Cell(1, 4).Value = "Default Rate";
                    worksheet.Cell(1, 5).Value = "Stock Value";

                    // Data
                    int row = 2;
                    foreach (var item in items)
                    {
                        worksheet.Cell(row, 1).Value = item.Name;
                        worksheet.Cell(row, 2).Value = item.Unit;
                        worksheet.Cell(row, 3).Value = item.CurrentStock;
                        worksheet.Cell(row, 4).Value = item.DefaultRate;
                        worksheet.Cell(row, 5).Value = item.CurrentStock * item.DefaultRate;
                        row++;
                    }

                    // Format as table
                    var range = worksheet.Range(1, 1, row - 1, 5);
                    range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Row(1).Style.Font.Bold = true;
                    worksheet.Row(1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                else if (reportType == "customers")
                {
                    var customers = await _customerRepository.GetActiveCustomersAsync();

                    // Headers
                    worksheet.Cell(1, 1).Value = "Name";
                    worksheet.Cell(1, 2).Value = "Phone";
                    worksheet.Cell(1, 3).Value = "Address";
                    worksheet.Cell(1, 4).Value = "Status";

                    // Data
                    int row = 2;
                    foreach (var customer in customers)
                    {
                        worksheet.Cell(row, 1).Value = customer.Name;
                        worksheet.Cell(row, 2).Value = customer.Phone;
                        worksheet.Cell(row, 3).Value = customer.Address;
                        worksheet.Cell(row, 4).Value = customer.IsActive ? "Active" : "Inactive";
                        row++;
                    }

                    // Format as table
                    var range = worksheet.Range(1, 1, row - 1, 4);
                    range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Row(1).Style.Font.Bold = true;
                    worksheet.Row(1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                return File(content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{reportType}_Report_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to Excel");
                TempData["Error"] = "Error exporting report. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Reports/CashFlow
        public async Task<IActionResult> CashFlow(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                // Default to showing last 30 days if no dates specified
                var endDate = toDate ?? DateTime.Today;
                var startDate = fromDate ?? DateTime.Today.AddDays(-30);

                // Get all cash transactions
                var vouchers = await _voucherRepository.GetVouchersByDateRangeAsync(startDate, endDate.AddDays(1));

                // Filter only cash transactions
                var cashVouchers = vouchers.Where(v =>
                    v.CashType == CashType.Cash ||
                    v.VoucherType == VoucherType.CashPaid ||
                    v.VoucherType == VoucherType.CashReceived).ToList();

                // Calculate cash in and out
                decimal cashIn = 0;
                decimal cashOut = 0;
                decimal openingBalance = await GetOpeningCashBalanceAsync(startDate);

                foreach (var voucher in cashVouchers)
                {
                    switch (voucher.VoucherType)
                    {
                        case VoucherType.Sale:
                        case VoucherType.CashReceived:
                            if (voucher.CashType == CashType.Cash)
                                cashIn += voucher.Amount;
                            break;
                        case VoucherType.Purchase:
                        case VoucherType.Expense:
                        case VoucherType.CashPaid:
                        case VoucherType.Hazri:
                            if (voucher.CashType == CashType.Cash)
                                cashOut += voucher.Amount;
                            break;
                    }
                }

                ViewBag.FromDate = startDate;
                ViewBag.ToDate = endDate;
                ViewBag.CashIn = cashIn;
                ViewBag.CashOut = cashOut;
                ViewBag.OpeningBalance = openingBalance;
                ViewBag.ClosingBalance = openingBalance + cashIn - cashOut;
                ViewBag.CashVouchers = cashVouchers.OrderBy(v => v.VoucherDate);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating cash flow report");
                TempData["Error"] = "Error generating cash flow report.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Reports/StockReport - UPDATED with date filtering
        public async Task<IActionResult> StockReport(DateTime? fromDate, DateTime? toDate, bool showAll = false)
        {
            try
            {
                var items = await _context.Items
                    .Where(i => i.StockTrackingEnabled && i.IsActive)
                    .OrderBy(i => i.Name)
                    .ToListAsync();

                // If dates are specified, calculate stock movement
                if (fromDate.HasValue && toDate.HasValue && !showAll)
                {
                    var vouchers = await _voucherRepository.GetVouchersByDateRangeAsync(
                        fromDate.Value,
                        toDate.Value.AddDays(1));

                    // Create stock movement summary
                    var stockMovements = new Dictionary<int, StockMovement>();

                    foreach (var item in items)
                    {
                        stockMovements[item.Id] = new StockMovement
                        {
                            Item = item,
                            OpeningStock = await GetOpeningStockAsync(item.Id, fromDate.Value),
                            PurchaseQty = 0,
                            SaleQty = 0,
                            CurrentStock = item.CurrentStock
                        };
                    }

                    // Calculate movements from vouchers
                    foreach (var voucher in vouchers.Where(v => v.ItemId.HasValue))
                    {
                        if (stockMovements.ContainsKey(voucher.ItemId.Value))
                        {
                            if (voucher.VoucherType == VoucherType.Purchase && voucher.StockInclude)
                            {
                                stockMovements[voucher.ItemId.Value].PurchaseQty += voucher.Quantity ?? 0;
                            }
                            else if (voucher.VoucherType == VoucherType.Sale)
                            {
                                stockMovements[voucher.ItemId.Value].SaleQty += voucher.Quantity ?? 0;
                            }
                        }
                    }

                    ViewBag.StockMovements = stockMovements.Values;
                    ViewBag.ShowMovement = true;
                }
                else
                {
                    ViewBag.ShowMovement = false;
                }

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.ShowAll = showAll;

                return View(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating stock report");
                TempData["Error"] = "Error generating stock report.";
                return RedirectToAction(nameof(Index));
            }
        }

        // Helper method to get opening cash balance
        private async Task<decimal> GetOpeningCashBalanceAsync(DateTime date)
        {
            var previousVouchers = await _context.Vouchers
                .Where(v => v.VoucherDate < date &&
                           (v.CashType == CashType.Cash ||
                            v.VoucherType == VoucherType.CashPaid ||
                            v.VoucherType == VoucherType.CashReceived))
                .ToListAsync();

            decimal balance = 0;
            foreach (var voucher in previousVouchers)
            {
                switch (voucher.VoucherType)
                {
                    case VoucherType.Sale:
                    case VoucherType.CashReceived:
                        if (voucher.CashType == CashType.Cash)
                            balance += voucher.Amount;
                        break;
                    case VoucherType.Purchase:
                    case VoucherType.Expense:
                    case VoucherType.CashPaid:
                    case VoucherType.Hazri:
                        if (voucher.CashType == CashType.Cash)
                            balance -= voucher.Amount;
                        break;
                }
            }
            return balance;
        }

        // Helper method to get opening stock
        private async Task<decimal> GetOpeningStockAsync(int itemId, DateTime date)
        {
            var previousVouchers = await _context.Vouchers
                .Where(v => v.ItemId == itemId && v.VoucherDate < date)
                .ToListAsync();

            decimal stock = 0;
            foreach (var voucher in previousVouchers)
            {
                if (voucher.VoucherType == VoucherType.Purchase && voucher.StockInclude)
                {
                    stock += voucher.Quantity ?? 0;
                }
                else if (voucher.VoucherType == VoucherType.Sale)
                {
                    stock -= voucher.Quantity ?? 0;
                }
            }
            return stock;
        }

        // GET: Reports/DailyCashBook
        public async Task<IActionResult> DailyCashBook(DateTime? date)
        {
            try
            {
                var reportDate = date ?? DateTime.Today;
                var nextDay = reportDate.AddDays(1);

                // Get opening balance
                var openingBalance = await GetOpeningCashBalanceAsync(reportDate);

                // Get today's transactions
                var todayVouchers = await _context.Vouchers
                    .Include(v => v.PurchasingCustomer)
                    .Include(v => v.ReceivingCustomer)
                    .Include(v => v.Item)
                    .Include(v => v.ExpenseHead)
                    .Include(v => v.Project)
                    .Where(v => v.VoucherDate >= reportDate &&
                               v.VoucherDate < nextDay &&
                               (v.CashType == CashType.Cash ||
                                v.VoucherType == VoucherType.CashPaid ||
                                v.VoucherType == VoucherType.CashReceived))
                    .OrderBy(v => v.VoucherDate)
                    .ToListAsync();

                ViewBag.ReportDate = reportDate;
                ViewBag.OpeningBalance = openingBalance;
                ViewBag.Vouchers = todayVouchers;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating daily cash book");
                TempData["Error"] = "Error generating daily cash book.";
                return RedirectToAction(nameof(Index));
            }
        }
    }

    // Helper class for stock movement
    public class StockMovement
    {
        public Item Item { get; set; }
        public decimal OpeningStock { get; set; }
        public decimal PurchaseQty { get; set; }
        public decimal SaleQty { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal ClosingStock => OpeningStock + PurchaseQty - SaleQty;
    }


}