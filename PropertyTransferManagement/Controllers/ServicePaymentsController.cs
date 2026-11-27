using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services;
using PropertyTransferManagement.Services.Payments;

namespace PropertyTransferManagement.Controllers
{
    public class ServiceCheckoutViewModel
    {
        public int PaymentId { get; set; }
        public Guid CaseId { get; set; }
        public string CaseNumber { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string? LastError { get; set; }

        [Required, Display(Name = "Name on card"), StringLength(100)]
        public string CardholderName { get; set; } = string.Empty;

        [Required, Display(Name = "Card number"), StringLength(23)]
        public string CardNumber { get; set; } = string.Empty;

        [Range(1, 12), Display(Name = "Month")]
        public int ExpMonth { get; set; }

        [Range(2000, 2100), Display(Name = "Year")]
        public int ExpYear { get; set; }

        [Required, StringLength(4, MinimumLength = 3), Display(Name = "CVC")]
        public string Cvc { get; set; } = string.Empty;
    }

    /// <summary>
    /// Optional assistance-service fees (spec §31) through a test gateway.
    /// Not government taxes or registration fees.
    /// </summary>
    [Authorize]
    public class ServicePaymentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly CaseService _cases;
        private readonly CaseAccess _access;
        private readonly DummyPaymentGateway _gateway;
        private readonly UserManager<ApplicationUser> _userManager;

        public ServicePaymentsController(ApplicationDbContext db, CaseService cases, CaseAccess access,
            DummyPaymentGateway gateway, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _cases = cases;
            _access = access;
            _gateway = gateway;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(Guid caseId, string serviceCode)
        {
            var userId = _userManager.GetUserId(User)!;
            if ((await _access.PartyRolesAsync(caseId, userId)).Count == 0) return Forbid();

            var service = ServiceCatalog.Find(serviceCode);
            if (service == null) return BadRequest();

            var transferCase = await _db.TransferCases.FindAsync(caseId);
            if (transferCase == null) return NotFound();
            if (CaseWorkflow.IsFinished(transferCase.Status))
            {
                TempData["Error"] = "This case is closed.";
                return RedirectToAction("Details", "Cases", new { id = caseId });
            }

            var pending = await _db.ServicePayments.FirstOrDefaultAsync(p =>
                p.TransferCaseId == caseId && p.PaidByUserId == userId && p.ServiceName == service.Name &&
                (p.Status == ServicePaymentStatus.Pending || p.Status == ServicePaymentStatus.Failed));
            if (pending != null) return RedirectToAction(nameof(Checkout), new { id = pending.Id });

            var payment = new ServicePayment
            {
                TransferCaseId = caseId,
                PaidByUserId = userId,
                ServiceName = service.Name,
                Amount = service.Price,
                Currency = "USD",
                Method = ServicePaymentMethod.TestPayment,
                Status = ServicePaymentStatus.Pending,
                IdempotencyKey = Guid.NewGuid().ToString("N")
            };
            _db.ServicePayments.Add(payment);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Checkout), new { id = payment.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Checkout(int id)
        {
            var payment = await LoadOwnAsync(id);
            if (payment == null) return NotFound();
            if (payment.Status == ServicePaymentStatus.Succeeded) return RedirectToAction(nameof(Receipt), new { id });

            return View(Fill(payment, new ServiceCheckoutViewModel { ExpMonth = DateTime.UtcNow.Month, ExpYear = DateTime.UtcNow.Year + 2 }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(int id, ServiceCheckoutViewModel model)
        {
            var payment = await LoadOwnAsync(id);
            if (payment == null) return NotFound();
            if (payment.Status == ServicePaymentStatus.Succeeded) return RedirectToAction(nameof(Receipt), new { id });
            if (payment.Status is not (ServicePaymentStatus.Pending or ServicePaymentStatus.Failed))
                return RedirectToAction("Details", "Cases", new { id = payment.TransferCaseId });

            if (!ModelState.IsValid) return View(Fill(payment, model));

            // Claim the payment in one UPDATE so two quick submits cannot both charge.
            var claimed = await _db.ServicePayments
                .Where(p => p.Id == payment.Id &&
                            (p.Status == ServicePaymentStatus.Pending || p.Status == ServicePaymentStatus.Failed))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, ServicePaymentStatus.Processing)
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));
            if (claimed == 0)
            {
                TempData["Error"] = "This payment is already being processed.";
                return RedirectToAction("Details", "Cases", new { id = payment.TransferCaseId });
            }

            var result = _gateway.Charge(payment.Amount, payment.Currency, model.CardNumber, model.ExpMonth, model.ExpYear, model.Cvc);
            var now = DateTime.UtcNow;

            if (!result.Succeeded)
            {
                payment.Status = ServicePaymentStatus.Failed;
                payment.FailureReason = result.FailureReason;
                payment.UpdatedAt = now;
                await _db.SaveChangesAsync();

                model.LastError = result.FailureReason + " No money was taken. You can try again.";
                model.CardNumber = model.Cvc = string.Empty;
                return View(Fill(payment, model));
            }

            payment.Status = ServicePaymentStatus.Succeeded;
            payment.ProviderTransactionId = result.ProviderTransactionId;
            payment.FailureReason = null;
            payment.PaidAt = now;
            payment.UpdatedAt = now;

            var c = payment.TransferCase;
            _cases.Notify(payment.PaidByUserId, c.Id, "Payment received",
                $"{payment.ServiceName}: {payment.Amount:N2} {payment.Currency} ({payment.ProviderTransactionId}).",
                CaseNotificationType.SystemAnnouncement);
            if (c.AssignedOfficerId != null)
                _cases.Notify(c.AssignedOfficerId, c.Id, $"Service paid — {c.CaseNumber}",
                    $"{payment.ServiceName} was paid for this case.", CaseNotificationType.SystemAnnouncement);
            _cases.Audit(c.Id, payment.PaidByUserId, "ServicePaid", "ServicePayment", payment.Id.ToString(),
                $"{payment.ServiceName}: {payment.Amount:N2} {payment.Currency}.");

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Receipt), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Receipt(int id)
        {
            var payment = await LoadOwnAsync(id);
            if (payment == null || payment.Status != ServicePaymentStatus.Succeeded) return NotFound();
            return View(payment);
        }

        private Task<ServicePayment?> LoadOwnAsync(int id)
        {
            var userId = _userManager.GetUserId(User);
            return _db.ServicePayments.Include(p => p.TransferCase)
                .FirstOrDefaultAsync(p => p.Id == id && p.PaidByUserId == userId);
        }

        private static ServiceCheckoutViewModel Fill(ServicePayment p, ServiceCheckoutViewModel m)
        {
            m.PaymentId = p.Id;
            m.CaseId = p.TransferCaseId;
            m.CaseNumber = p.TransferCase.CaseNumber;
            m.ServiceName = p.ServiceName;
            m.Amount = p.Amount;
            m.Currency = p.Currency;
            return m;
        }
    }
}
