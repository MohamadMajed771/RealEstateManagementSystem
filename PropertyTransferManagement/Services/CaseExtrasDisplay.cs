using PropertyTransferManagement.Models;

namespace PropertyTransferManagement.Services
{
    /// <summary>Display names that follow the project specification wording.</summary>
    public static class CaseExtrasDisplay
    {
        public static string Loan(LoanAssistanceStatus s) => s switch
        {
            LoanAssistanceStatus.Pending => "Request submitted",
            LoanAssistanceStatus.InformationRequested => "Documents required",
            LoanAssistanceStatus.UnderReview => "Under review",
            LoanAssistanceStatus.ReferredToBank => "Submitted to lender",
            LoanAssistanceStatus.Closed => "Lender decision recorded",
            LoanAssistanceStatus.Rejected => "Not taken forward",
            LoanAssistanceStatus.Cancelled => "Cancelled",
            _ => s.ToString()
        };

        public static bool LoanIsOpen(LoanAssistanceStatus s) =>
            s is LoanAssistanceStatus.Pending or LoanAssistanceStatus.UnderReview
                or LoanAssistanceStatus.InformationRequested or LoanAssistanceStatus.ReferredToBank;

        public static string Lender(LenderDecision? d) => d switch
        {
            LenderDecision.ApprovedByLender => "Approved by the lender",
            LenderDecision.DeclinedByLender => "Declined by the lender",
            _ => ""
        };

        public static string Appointment(CaseAppointmentType t) => t switch
        {
            CaseAppointmentType.DocumentVerification => "Document verification",
            CaseAppointmentType.BuyerSellerMeeting => "Buyer & seller meeting",
            CaseAppointmentType.OfficerConsultation => "Consultation with the officer",
            CaseAppointmentType.OfficialProcedure => "Official procedure",
            _ => "Other"
        };

        public static string Audience(CaseAppointmentAudience a) => a switch
        {
            CaseAppointmentAudience.Buyer => "Buyer",
            CaseAppointmentAudience.Seller => "Seller",
            _ => "Buyer and seller"
        };

        public static bool Concerns(CaseAppointment a, IEnumerable<CaseParticipantRole> roles) =>
            roles.Any(r => a.Audience == CaseAppointmentAudience.BuyerAndSeller ||
                           (r == CaseParticipantRole.Buyer && a.Audience == CaseAppointmentAudience.Buyer) ||
                           (r == CaseParticipantRole.Seller && a.Audience == CaseAppointmentAudience.Seller));

        public static CaseParticipantRole? AudienceRole(CaseAppointmentAudience a) => a switch
        {
            CaseAppointmentAudience.Buyer => CaseParticipantRole.Buyer,
            CaseAppointmentAudience.Seller => CaseParticipantRole.Seller,
            _ => null
        };
    }

    /// <summary>
    /// Optional assistance services sold by the transfer service.
    /// These are service fees, NOT government taxes or registration fees.
    /// </summary>
    public static class ServiceCatalog
    {
        public record Service(string Code, string Name, string Description, decimal Price);

        public static readonly IReadOnlyList<Service> All = new[]
        {
            new Service("DOC_PREP", "Document preparation assistance",
                "An officer checks your documents before you upload them and explains what is missing.", 25m),
            new Service("EXPRESS_REVIEW", "Express document review",
                "Your uploaded documents are reviewed within one working day.", 40m),
            new Service("APPOINTMENT_HELP", "Appointment arrangement",
                "We arrange the meetings needed for the procedure and send you the details.", 15m)
        };

        public static Service? Find(string? code) => All.FirstOrDefault(s => s.Code == code);
    }
}
