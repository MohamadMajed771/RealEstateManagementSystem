using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services;

namespace PropertyTransferManagement.ViewModels
{
    public class MyCaseRow
    {
        public TransferCase Case { get; set; } = null!;
        public List<CaseParticipantRole> MyRoles { get; set; } = new();
        public int DocumentsToUpload { get; set; }
        public string? OfficerName { get; set; }
    }

    /// <summary>Case page for a buyer or seller.</summary>
    public class CaseDetailsViewModel
    {
        public TransferCase Case { get; set; } = null!;
        public List<CaseParticipantRole> MyRoles { get; set; } = new();
        public string? OfficerName { get; set; }
        public List<CaseDocumentRequirement> MyRequirements { get; set; } = new();
        public List<CaseDocument> MyDocuments { get; set; } = new();
        public List<CaseStatusHistory> History { get; set; } = new();
        public bool CanUpload { get; set; }

        public List<ConversationSummary> Conversations { get; set; } = new();
        public List<CaseAppointment> Appointments { get; set; } = new();
        public List<LoanAssistanceRequest> MyLoanRequests { get; set; } = new();
        public List<ServicePayment> MyPayments { get; set; } = new();
    }

    /// <summary>Case page for the assigned officer / admin.</summary>
    public class OfficerCaseViewModel
    {
        public TransferCase Case { get; set; } = null!;
        public List<CaseParticipant> Participants { get; set; } = new();
        public List<CaseInvitation> Invitations { get; set; } = new();
        public List<CaseDocumentRequirement> Requirements { get; set; } = new();
        public List<CaseDocument> Documents { get; set; } = new();
        public List<CaseStatusHistory> History { get; set; } = new();
        public List<CaseAuditLog> AuditLog { get; set; } = new();
        public IReadOnlyList<TransferCaseStatus> NextStatuses { get; set; } = Array.Empty<TransferCaseStatus>();
        public OfficialConfirmation? Confirmation { get; set; }
        public bool AllMandatoryApproved { get; set; }

        public List<ConversationSummary> Conversations { get; set; } = new();
        public List<CaseAppointment> Appointments { get; set; } = new();
        public List<LoanAssistanceRequest> LoanRequests { get; set; } = new();
        public List<ServicePayment> Payments { get; set; } = new();
    }

    public class OfficerOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ActiveCases { get; set; }
    }

    /// <summary>A staff account (officer and/or admin) on the Officers page.</summary>
    public class StaffMember
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsOfficer { get; set; }
        public bool IsAdmin { get; set; }
        public int ActiveCases { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public int TotalCases { get; set; }
        public int NewThisMonth { get; set; }
        public int Unassigned { get; set; }
        public int AwaitingDocuments { get; set; }
        public int UnderReview { get; set; }
        public int Completed { get; set; }
        public int CancelledOrRejected { get; set; }
        public List<TransferCase> Cases { get; set; } = new();
        public List<OfficerOption> Officers { get; set; } = new();
    }
}
