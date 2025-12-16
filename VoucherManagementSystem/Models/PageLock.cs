using System.ComponentModel.DataAnnotations;

namespace VoucherManagementSystem.Models
{
    public class PageLock
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string PageName { get; set; } = "";

        [Required]
        [MaxLength(200)]
        public string PageUrl { get; set; } = "";

        public bool IsLocked { get; set; } = false;

        [MaxLength(500)]
        public string? Password { get; set; }

        public DateTime? LastModifiedDate { get; set; }

        [MaxLength(100)]
        public string? LastModifiedBy { get; set; }
    }
}
