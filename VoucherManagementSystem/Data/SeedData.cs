using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VoucherManagementSystem.Models;

namespace VoucherManagementSystem.Data
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());

            // Ensure database is created
            context.Database.EnsureCreated();

            // Check if data already exists
            if (context.Customers.Any())
            {
                return; // Database has been seeded
            }

            // Seed Customers
            var customers = new Customer[]
            {
                new Customer { Name = "General Customer", Phone = "0300-0000000", Address = "Main Market", IsActive = true },
                new Customer { Name = "ABC Trading", Phone = "0321-1234567", Address = "Industrial Area", IsActive = true },
                new Customer { Name = "XYZ Corporation", Phone = "0333-7654321", Address = "Business District", IsActive = true },
                new Customer { Name = "Ahmed Enterprises", Phone = "0345-5555555", Address = "Commercial Zone", IsActive = true },
                new Customer { Name = "Ali & Sons", Phone = "0312-9999999", Address = "City Center", IsActive = true }
            };
            context.Customers.AddRange(customers);
            context.SaveChanges();

            // Seed Items
            var items = new Item[]
            {
                new Item { Name = "Cement", Unit = "Bag", StockTrackingEnabled = true, CurrentStock = 100, DefaultRate = 1250 },
                new Item { Name = "Steel", Unit = "Ton", StockTrackingEnabled = true, CurrentStock = 50, DefaultRate = 185000 },
                new Item { Name = "Bricks", Unit = "Thousand", StockTrackingEnabled = true, CurrentStock = 200, DefaultRate = 12000 },
                new Item { Name = "Sand", Unit = "Truck", StockTrackingEnabled = true, CurrentStock = 30, DefaultRate = 45000 },
                new Item { Name = "Crush", Unit = "Truck", StockTrackingEnabled = true, CurrentStock = 25, DefaultRate = 55000 },
                new Item { Name = "Paint", Unit = "Gallon", StockTrackingEnabled = true, CurrentStock = 80, DefaultRate = 3500 },
                new Item { Name = "Tiles", Unit = "Box", StockTrackingEnabled = true, CurrentStock = 150, DefaultRate = 2800 }
            };
            context.Items.AddRange(items);
            context.SaveChanges();

            // Seed Banks
            var banks = new Bank[]
            {
                new Bank { Name = "HBL", AccountNumber = "1234567890", Balance = 500000, Details = "Main Branch Account" },
                new Bank { Name = "MCB", AccountNumber = "0987654321", Balance = 750000, Details = "Corporate Account" },
                new Bank { Name = "UBL", AccountNumber = "1122334455", Balance = 300000, Details = "Business Account" },
                new Bank { Name = "Allied Bank", AccountNumber = "5544332211", Balance = 450000, Details = "Current Account" },
                new Bank { Name = "Meezan Bank", AccountNumber = "9988776655", Balance = 600000, Details = "Islamic Banking Account" }
            };
            context.Banks.AddRange(banks);
            context.SaveChanges();

            // Seed ExpenseHeads
            var expenseHeads = new ExpenseHead[]
            {
                new ExpenseHead { Name = "Labor Charges", DefaultRate = 1200, Notes = "Daily labor wages" },
                new ExpenseHead { Name = "Transportation", DefaultRate = 5000, Notes = "Vehicle and fuel expenses" },
                new ExpenseHead { Name = "Utilities", DefaultRate = 0, Notes = "Electricity, Gas, Water bills" },
                new ExpenseHead { Name = "Office Expenses", DefaultRate = 0, Notes = "Stationery and supplies" },
                new ExpenseHead { Name = "Maintenance", DefaultRate = 0, Notes = "Equipment and machinery maintenance" },
                new ExpenseHead { Name = "Marketing", DefaultRate = 0, Notes = "Advertisement and promotion" },
                new ExpenseHead { Name = "Miscellaneous", DefaultRate = 0, Notes = "Other expenses" }
            };
            context.ExpenseHeads.AddRange(expenseHeads);
            context.SaveChanges();

            // Seed Projects
            var projects = new Project[]
            {
                new Project
                {
                    Name = "Plaza Construction",
                    Description = "5-story commercial plaza project",
                    StartDate = DateTime.Now.AddMonths(-3),
                    IsActive = true
                },
                new Project
                {
                    Name = "Residential Complex",
                    Description = "50 houses residential project",
                    StartDate = DateTime.Now.AddMonths(-6),
                    IsActive = true
                },
                new Project
                {
                    Name = "Highway Bridge",
                    Description = "Government infrastructure project",
                    StartDate = DateTime.Now.AddMonths(-1),
                    IsActive = true
                },
                new Project
                {
                    Name = "Shopping Mall",
                    Description = "Modern shopping center development",
                    StartDate = DateTime.Now.AddMonths(-2),
                    IsActive = true
                }
            };
            context.Projects.AddRange(projects);
            context.SaveChanges();

            // Seed Customer Item Rates
            var customerRates = new CustomerItemRate[]
            {
                new CustomerItemRate { CustomerId = customers[1].Id, ItemId = items[0].Id, Rate = 1200 },
                new CustomerItemRate { CustomerId = customers[1].Id, ItemId = items[1].Id, Rate = 182000 },
                new CustomerItemRate { CustomerId = customers[2].Id, ItemId = items[0].Id, Rate = 1280 },
                new CustomerItemRate { CustomerId = customers[2].Id, ItemId = items[2].Id, Rate = 11500 }
            };
            context.CustomerItemRates.AddRange(customerRates);
            context.SaveChanges();
        }
    }
}