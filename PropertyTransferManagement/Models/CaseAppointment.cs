
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class CaseAppointment : IValidatableObject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid TransferCaseId { get; set; }

        public TransferCase TransferCase { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        [EnumDataType(typeof(CaseAppointmentType))]
        public CaseAppointmentType AppointmentType { get; set; }

        [Required]
        public DateTime StartAt { get; set; }

        [Required]
        public DateTime EndAt { get; set; }

        [StringLength(300)]
        public string? Location { get; set; }

        [StringLength(500)]
        [Url]
        public string? MeetingUrl { get; set; }

        [Required]
        [EnumDataType(typeof(CaseAppointmentStatus))]
        public CaseAppointmentStatus Status { get; set; } =
            CaseAppointmentStatus.Scheduled;

        public string? CreatedByUserId { get; set; }

        public ApplicationUser? CreatedByUser { get; set; }

        /// <summary>Who must attend (besides the officer).</summary>
        public CaseAppointmentAudience Audience { get; set; } = CaseAppointmentAudience.BuyerAndSeller;

        /// <summary>Set when the 24-hour reminder was sent.</summary>
        public DateTime? ReminderSentAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (EndAt <= StartAt)
            {
                yield return new ValidationResult(
                    "Appointment end time must be after start time.",
                    new[] { nameof(EndAt) });
            }
        }
    }

    public enum CaseAppointmentType
    {
        DocumentVerification = 1,
        BuyerSellerMeeting = 2,
        OfficerConsultation = 3,
        OfficialProcedure = 4,
        Other = 5
    }

    public enum CaseAppointmentStatus
    {
        Scheduled = 1,
        Completed = 2,
        Cancelled = 3,
        Rescheduled = 4,
        NoShow = 5
    }

    public enum CaseAppointmentAudience
    {
        Buyer = 1,
        Seller = 2,
        BuyerAndSeller = 3
    }
}
