
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class CaseNotification
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        public Guid? TransferCaseId { get; set; }

        public TransferCase? TransferCase { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Message { get; set; } = string.Empty;

        [Required]
        [EnumDataType(typeof(CaseNotificationType))]
        public CaseNotificationType Type { get; set; }

        [StringLength(500)]
        public string? ActionUrl { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime? ReadAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;
    }

    public enum CaseNotificationType
    {
        CaseCreated = 1,
        CaseStatusChanged = 2,
        DocumentRequired = 3,
        DocumentApproved = 4,
        DocumentRejected = 5,
        OfficerAssigned = 6,
        AppointmentScheduled = 7,
        AppointmentChanged = 8,
        NewMessage = 9,
        OfficialConfirmationReceived = 10,
        CaseCompleted = 11,
        CaseRejected = 12,
        SystemAnnouncement = 13
    }
}
