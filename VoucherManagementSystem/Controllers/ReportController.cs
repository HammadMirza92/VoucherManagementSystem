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

                // Get opening balance
                var openingBalance = await GetBankOpeningBalanceAsync(bankId, fromDate);

                var transactions = await _bankRepository.GetBankTransactionsAsync(bankId, fromDate, toDate);

                ViewBag.Bank = bank;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.OpeningBalance = openingBalance;
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

        // GET: Reports/CashStatement - Tracks cash from vouchers (CashType = Cash)
        public async Task<IActionResult> CashStatement(DateTime? fromDate, DateTime? toDate, int? customerId, string? voucherType)
        {
            try
            {
                var endDate = toDate ?? DateTime.Today;
                var startDate = fromDate ?? DateTime.Today.AddMonths(-1);

                // Get customers for filter dropdown
                ViewBag.Customers = new SelectList(await _customerRepository.GetActiveCustomersAsync(), "Id", "Name", customerId);

                // Voucher types for filter
                var voucherTypes = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "-- All Types --" },
                    new SelectListItem { Value = "Sale", Text = "Sale" },
                    new SelectListItem { Value = "Purchase", Text = "Purchase" },
                    new SelectListItem { Value = "CashReceived", Text = "Cash Received" },
                    new SelectListItem { Value = "CashPaid", Text = "Cash Paid" },
                    new SelectListItem { Value = "Expense", Text = "Expense" },
                    new SelectListItem { Value = "Hazri", Text = "Hazri" }
                };
                ViewBag.VoucherTypes = new SelectList(voucherTypes, "Value", "Text", voucherType);
                ViewBag.SelectedVoucherType = voucherType;

                // Build query for cash vouchers
                var query = _context.Vouchers
                    .Include(v => v.PurchasingCustomer)
                    .Include(v => v.ReceivingCustomer)
                    .Include(v => v.Item)
                    .Include(v => v.ExpenseHead)
                    .Where(v => v.CashType == CashType.Cash &&
                               v.VoucherDate >= startDate && v.VoucherDate <= endDate.AddDays(1));

                // Apply customer filter if selected
                if (customerId.HasValue)
                {
                    query = query.Where(v => v.PurchasingCustomerId == customerId || v.ReceivingCustomerId == customerId);
                    ViewBag.SelectedCustomerId = customerId;
                    ViewBag.SelectedCustomer = await _customerRepository.GetByIdAsync(customerId.Value);
                }

                // Apply voucher type filter if selected
                if (!string.IsNullOrEmpty(voucherType) && Enum.TryParse<VoucherType>(voucherType, out var vType))
                {
                    query = query.Where(v => v.VoucherType == vType);
                }

                var vouchers = await query.OrderBy(v => v.VoucherDate).ThenBy(v => v.Id).ToListAsync();

                // Get cash adjustments for the period
                var cashAdjustments = await _context.CashAdjustments
                    .Where(a => a.AdjustmentDate >= startDate && a.AdjustmentDate <= endDate.AddDays(1))
                    .OrderBy(a => a.AdjustmentDate)
                    .ToListAsync();

                // Calculate opening balance (all cash transactions before start date)
                var openingBalance = await GetCashOpeningBalanceAsync(startDate, customerId);

                // Calculate totals from vouchers
                decimal totalReceipts = 0;
                decimal totalPayments = 0;

                foreach (var v in vouchers)
                {
                    switch (v.VoucherType)
                    {
                        case VoucherType.Sale:
                        case VoucherType.CashReceived:
                            totalReceipts += v.Amount;
                            break;
                        case VoucherType.Purchase:
                        case VoucherType.Expense:
                        case VoucherType.CashPaid:
                        case VoucherType.Hazri:
                            totalPayments += v.Amount;
                            break;
                    }
                }

                // Add cash adjustments to totals
                foreach (var adj in cashAdjustments)
                {
                    if (adj.AdjustmentType == CashAdjustmentType.CashIn)
                        totalReceipts += adj.Amount;
                    else
                        totalPayments += adj.Amount;
                }

                ViewBag.FromDate = startDate;
                ViewBag.ToDate = endDate;
                ViewBag.OpeningBalance = openingBalance;
                ViewBag.TotalReceipts = totalReceipts;
                ViewBag.TotalPayments = totalPayments;
                ViewBag.ClosingBalance = openingBalance + totalReceipts - totalPayments;
                ViewBag.Vouchers = vouchers;
                ViewBag.CashAdjustments = cashAdjustments;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating cash statement");
                TempData["Error"] = "Error generating cash statement.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Reports/AddCashAdjustment
        public IActionResult AddCashAdjustment(string type = "CashIn")
        {
            ViewBag.AdjustmentType = type == "CashOut" ? CashAdjustmentType.CashOut : CashAdjustmentType.CashIn;
            return View(new CashAdjustment { AdjustmentType = type == "CashOut" ? CashAdjustmentType.CashOut : CashAdjustmentType.CashIn });
        }

        // POST: Reports/AddCashAdjustment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCashAdjustment(CashAdjustment adjustment)
        {
            if (ModelState.IsValid)
            {
                adjustment.CreatedBy = HttpContext.Session.GetString("Username") ?? "Admin";
                adjustment.CreatedDate = DateTime.Now;

                // Generate reference number
                var count = await _context.CashAdjustments.CountAsync() + 1;
                adjustment.ReferenceNumber = $"CASH-{(adjustment.AdjustmentType == CashAdjustmentType.CashIn ? "IN" : "OUT")}-{DateTime.Now:yyyyMMdd}-{count:D4}";

                _context.CashAdjustments.Add(adjustment);
                await _context.SaveChangesAsync();

                TempData["Success"] = adjustment.AdjustmentType == CashAdjustmentType.CashIn
                    ? $"Cash In of Rs. {adjustment.Amount:N0} added successfully!"
                    : $"Cash Out of Rs. {adjustment.Amount:N0} recorded successfully!";

                return RedirectToAction(nameof(CashStatement));
            }

            ViewBag.AdjustmentType = adjustment.AdjustmentType;
            return View(adjustment);
        }

        // Helper method to get opening cash balance
        private async Task<decimal> GetCashOpeningBalanceAsync(DateTime date, int? customerId = null)
        {
            decimal balance = 0;

            // Get voucher transactions before date
            var voucherQuery = _context.Vouchers
                .Where(v => v.CashType == CashType.Cash && v.VoucherDate < date);

            if (customerId.HasValue)
            {
                voucherQuery = voucherQuery.Where(v => v.PurchasingCustomerId == customerId || v.ReceivingCustomerId == customerId);
            }

            var previousVouchers = await voucherQuery.ToListAsync();

            foreach (var v in previousVouchers)
            {
                switch (v.VoucherType)
                {
                    case VoucherType.Sale:
                    case VoucherType.CashReceived:
                        balance += v.Amount;
                        break;
                    case VoucherType.Purchase:
                    case VoucherType.Expense:
                    case VoucherType.CashPaid:
                    case VoucherType.Hazri:
                        balance -= v.Amount;
                        break;
                }
            }

            // Add cash adjustments before date (only if no customer filter)
            if (!customerId.HasValue)
            {
                var adjustments = await _context.CashAdjustments
                    .Where(a => a.AdjustmentDate < date)
                    .ToListAsync();

                foreach (var adj in adjustments)
                {
                    if (adj.AdjustmentType == CashAdjustmentType.CashIn)
                        balance += adj.Amount;
                    else
                        balance -= adj.Amount;
                }
            }

            return balance;
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
                else if (reportType == "customerLedger" && id.HasValue && fromDate.HasValue && toDate.HasValue)
                {
                    var customer = await _customerRepository.GetByIdAsync(id.Value);
                    if (customer != null)
                    {
                        var openingBalance = await GetCustomerOpeningBalanceAsync(id.Value, fromDate.Value);

                        var vouchers = await _context.Vouchers
                            .Include(v => v.PurchasingCustomer)
                            .Include(v => v.ReceivingCustomer)
                            .Include(v => v.Item)
                            .Include(v => v.ExpenseHead)
                            .Include(v => v.Project)
                            .Where(v => (v.PurchasingCustomerId == id.Value || v.ReceivingCustomerId == id.Value) &&
                                       v.VoucherDate >= fromDate.Value &&
                                       v.VoucherDate <= toDate.Value.AddDays(1))
                            .OrderBy(v => v.VoucherDate)
                            .ThenBy(v => v.Id)
                            .ToListAsync();

                        // Headers
                        worksheet.Cell(1, 1).Value = "Customer Ledger Report";
                        worksheet.Cell(2, 1).Value = $"Customer: {customer.Name}";
                        worksheet.Cell(3, 1).Value = $"Period: {fromDate.Value:dd-MMM-yyyy} to {toDate.Value:dd-MMM-yyyy}";

                        // Table headers
                        worksheet.Cell(5, 1).Value = "Date";
                        worksheet.Cell(5, 2).Value = "Transaction No";
                        worksheet.Cell(5, 3).Value = "Type";
                        worksheet.Cell(5, 4).Value = "Particulars";
                        worksheet.Cell(5, 5).Value = "Debit (Dr)";
                        worksheet.Cell(5, 6).Value = "Credit (Cr)";
                        worksheet.Cell(5, 7).Value = "Balance";

                        // Opening balance
                        int row = 6;
                        worksheet.Cell(row, 1).Value = fromDate.Value.ToString("dd-MMM-yyyy");
                        worksheet.Cell(row, 4).Value = "Opening Balance";
                        worksheet.Cell(row, 5).Value = openingBalance > 0 ? openingBalance : 0;
                        worksheet.Cell(row, 6).Value = openingBalance < 0 ? Math.Abs(openingBalance) : 0;
                        worksheet.Cell(row, 7).Value = $"{Math.Abs(openingBalance):N0} {(openingBalance >= 0 ? "Dr" : "Cr")}";
                        row++;

                        decimal runningBalance = openingBalance;
                        decimal totalDebit = 0;
                        decimal totalCredit = 0;

                        // NEW DR/CR Logic: Purchase=CR, Sale=DR
                        foreach (var voucher in vouchers)
                        {
                            decimal debit = 0;
                            decimal credit = 0;
                            string particulars = "";

                            if (voucher.PurchasingCustomerId == id.Value)
                            {
                                switch (voucher.VoucherType)
                                {
                                    case VoucherType.Purchase:
                                        credit = voucher.Amount;  // Purchase = CR
                                        particulars = $"Purchase - {voucher.Item?.Name ?? "N/A"}";
                                        break;
                                    case VoucherType.CashPaid:
                                        debit = voucher.Amount;   // CashPaid = DR
                                        particulars = "Cash Paid";
                                        break;
                                    case VoucherType.CCR:
                                        debit = voucher.Amount;   // CCR = DR
                                        particulars = $"CCR - From {voucher.ReceivingCustomer?.Name ?? "N/A"}";
                                        break;
                                }
                            }

                            if (voucher.ReceivingCustomerId == id.Value)
                            {
                                switch (voucher.VoucherType)
                                {
                                    case VoucherType.Sale:
                                        debit = voucher.Amount;   // Sale = DR
                                        particulars = $"Sale - {voucher.Item?.Name ?? "N/A"}";
                                        break;
                                    case VoucherType.CashReceived:
                                        credit = voucher.Amount;  // CashReceived = CR
                                        particulars = "Cash Received";
                                        break;
                                    case VoucherType.CCR:
                                        credit = voucher.Amount;  // CCR = CR
                                        particulars = $"CCR - To {voucher.PurchasingCustomer?.Name ?? "N/A"}";
                                        break;
                                }
                            }

                            runningBalance += debit - credit;
                            totalDebit += debit;
                            totalCredit += credit;

                            worksheet.Cell(row, 1).Value = voucher.VoucherDate.ToString("dd-MMM-yyyy");
                            worksheet.Cell(row, 2).Value = voucher.TransactionNumber;
                            worksheet.Cell(row, 3).Value = voucher.VoucherType.ToString();
                            worksheet.Cell(row, 4).Value = particulars;
                            worksheet.Cell(row, 5).Value = debit > 0 ? debit : 0;
                            worksheet.Cell(row, 6).Value = credit > 0 ? credit : 0;
                            worksheet.Cell(row, 7).Value = $"{Math.Abs(runningBalance):N0} {(runningBalance >= 0 ? "Dr" : "Cr")}";
                            row++;
                        }

                        // Totals
                        worksheet.Cell(row, 4).Value = "Total:";
                        worksheet.Cell(row, 5).Value = totalDebit;
                        worksheet.Cell(row, 6).Value = totalCredit;
                        worksheet.Cell(row, 7).Value = $"{Math.Abs(runningBalance):N0} {(runningBalance >= 0 ? "Dr" : "Cr")}";

                        // Format
                        worksheet.Row(5).Style.Font.Bold = true;
                        worksheet.Row(5).Style.Fill.BackgroundColor = XLColor.LightGray;
                        worksheet.Row(row).Style.Font.Bold = true;
                        worksheet.Row(row).Style.Fill.BackgroundColor = XLColor.LightGray;

                        var range = worksheet.Range(5, 1, row, 7);
                        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    }
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

        // GET: Reports/CustomerLedger
        public async Task<IActionResult> CustomerLedger(int? customerId, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                if (!customerId.HasValue)
                {
                    // Show selection page
                    ViewBag.Customers = new SelectList(await _customerRepository.GetActiveCustomersAsync(), "Id", "Name");
                    return View();
                }

                var customer = await _customerRepository.GetByIdAsync(customerId.Value);
                if (customer == null)
                {
                    TempData["Error"] = "Customer not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Default to showing last 90 days if no dates specified
                var endDate = toDate ?? DateTime.Today;
                var startDate = fromDate ?? DateTime.Today.AddDays(-90);

                // Get opening balance (transactions before start date)
                var openingBalance = await GetCustomerOpeningBalanceAsync(customerId.Value, startDate);

                // Get all transactions for the customer in the date range
                var vouchers = await _context.Vouchers
                    .Include(v => v.PurchasingCustomer)
                    .Include(v => v.ReceivingCustomer)
                    .Include(v => v.Item)
                    .Include(v => v.ExpenseHead)
                    .Include(v => v.Project)
                    .Where(v => (v.PurchasingCustomerId == customerId.Value ||
                                v.ReceivingCustomerId == customerId.Value) &&
                               v.VoucherDate >= startDate &&
                               v.VoucherDate <= endDate.AddDays(1))
                    .OrderBy(v => v.VoucherDate)
                    .ThenBy(v => v.Id)
                    .ToListAsync();

                // Calculate totals
                // NEW DR/CR Logic: Purchase=CR, Sale=DR
                decimal totalDebit = 0;
                decimal totalCredit = 0;

                foreach (var voucher in vouchers)
                {
                    // Purchase = CR (we owe the supplier)
                    // CashPaid = DR (we paid, reduces what we owe)
                    if (voucher.PurchasingCustomerId == customerId.Value)
                    {
                        switch (voucher.VoucherType)
                        {
                            case VoucherType.Purchase:
                                totalCredit += voucher.Amount;  // Purchase = CR
                                break;
                            case VoucherType.CashPaid:
                            case VoucherType.CCR:
                                totalDebit += voucher.Amount;   // CashPaid = DR
                                break;
                        }
                    }

                    // Sale = DR (customer owes us)
                    // CashReceived = CR (customer paid, reduces what they owe)
                    if (voucher.ReceivingCustomerId == customerId.Value)
                    {
                        switch (voucher.VoucherType)
                        {
                            case VoucherType.Sale:
                                totalDebit += voucher.Amount;   // Sale = DR
                                break;
                            case VoucherType.CashReceived:
                            case VoucherType.CCR:
                                totalCredit += voucher.Amount;  // CashReceived = CR
                                break;
                        }
                    }
                }

                ViewBag.Customer = customer;
                ViewBag.FromDate = startDate;
                ViewBag.ToDate = endDate;
                ViewBag.OpeningBalance = openingBalance;
                ViewBag.TotalDebit = totalDebit;
                ViewBag.TotalCredit = totalCredit;
                ViewBag.ClosingBalance = openingBalance + totalDebit - totalCredit;
                ViewBag.Vouchers = vouchers;
                ViewBag.Customers = new SelectList(await _customerRepository.GetActiveCustomersAsync(), "Id", "Name", customerId);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating customer ledger");
                TempData["Error"] = "Error generating customer ledger.";
                return RedirectToAction(nameof(Index));
            }
        }

        // Helper method to get opening balance for customer
        // NEW DR/CR Logic: Purchase=CR, Sale=DR
        private async Task<decimal> GetCustomerOpeningBalanceAsync(int customerId, DateTime date)
        {
            var previousVouchers = await _context.Vouchers
                .Where(v => (v.PurchasingCustomerId == customerId || v.ReceivingCustomerId == customerId) &&
                           v.VoucherDate < date)
                .ToListAsync();

            decimal balance = 0;
            foreach (var voucher in previousVouchers)
            {
                // Purchase = CR (we owe them) - decreases balance
                // CashPaid = DR (we paid) - increases balance
                if (voucher.PurchasingCustomerId == customerId)
                {
                    switch (voucher.VoucherType)
                    {
                        case VoucherType.Purchase:
                            balance -= voucher.Amount;  // CR decreases balance
                            break;
                        case VoucherType.CashPaid:
                        case VoucherType.CCR:
                            balance += voucher.Amount;  // DR increases balance
                            break;
                    }
                }

                // Sale = DR (they owe us) - increases balance
                // CashReceived = CR (they paid) - decreases balance
                if (voucher.ReceivingCustomerId == customerId)
                {
                    switch (voucher.VoucherType)
                    {
                        case VoucherType.Sale:
                            balance += voucher.Amount;  // DR increases balance
                            break;
                        case VoucherType.CashReceived:
                        case VoucherType.CCR:
                            balance -= voucher.Amount;  // CR decreases balance
                            break;
                    }
                }
            }
            return balance;
        }

        // Helper method to get opening bank balance
        private async Task<decimal> GetBankOpeningBalanceAsync(int bankId, DateTime date)
        {
            var previousVouchers = await _context.Vouchers
                .Where(v => (v.BankCustomerPaidId == bankId || v.BankCustomerReceiverId == bankId) &&
                           v.VoucherDate < date)
                .ToListAsync();

            decimal balance = 0;
            foreach (var voucher in previousVouchers)
            {
                // Money paid from bank (debit - reduces bank balance)
                if (voucher.BankCustomerPaidId == bankId)
                {
                    balance -= voucher.Amount;
                }

                // Money received into bank (credit - increases bank balance)
                if (voucher.BankCustomerReceiverId == bankId)
                {
                    balance += voucher.Amount;
                }
            }
            return balance;
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