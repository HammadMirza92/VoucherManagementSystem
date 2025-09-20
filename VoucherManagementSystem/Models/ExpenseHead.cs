using System.ComponentModel.DataAnnotations;

namespace VoucherManagementSystem.Models
{
    public class ExpenseHead
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Expense head name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        [Display(Name = "Expense Head Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Default Rate")]
        [Range(0, double.MaxValue, ErrorMessage = "Rate cannot be negative")]
        public decimal DefaultRate { get; set; } = 0;

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Active Status")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<Voucher>? Vouchers { get; set; }
    }
}