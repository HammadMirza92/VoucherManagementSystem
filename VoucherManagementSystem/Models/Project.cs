using System.ComponentModel.DataAnnotations;

namespace VoucherManagementSystem.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Project name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        [Display(Name = "Project Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Project Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Active Status")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<Voucher>? Vouchers { get; set; }
    }
}