using Asnan.Application.Common;
using Asnan.Application.Payments;
using Asnan.Domain.Entities;
using Asnan.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asnan.Application.Appointments;

public class AppointmentService : IAppointmentService
{
    /// <summary>Statuses an appointment never leaves — everything else (PaymentPending, Scheduled) still counts as "upcoming" while its slot hasn't ended.</summary>
    private static readonly HashSet<AppointmentStatus> TerminalStatuses =
    [
        AppointmentStatus.Completed,
        AppointmentStatus.NoShow,
        AppointmentStatus.CancelledByPatient,
        AppointmentStatus.CancelledByDoctor,
        AppointmentStatus.CancelledByAdmin,
        AppointmentStatus.RefundPending,
        AppointmentStatus.Refunded,
        AppointmentStatus.PaymentFailed,
        AppointmentStatus.Expired,
    ];

    private readonly IApplicationDbContext _db;
    private readonly IRefundService _refundService;
    private readonly CancellationPolicyOptions _policyOptions;

    public AppointmentService(IApplicationDbContext db, IRefundService refundService, IOptions<CancellationPolicyOptions> policyOptions)
    {
        _db = db;
        _refundService = refundService;
        _policyOptions = policyOptions.Value;
    }

    public async Task<PagedResult<AppointmentSummaryDto>> ListAsync(Guid callerId, AppointmentListQuery query, CancellationToken cancellationToken = default)
    {
        var callerDoctorProfileId = await _db.DoctorProfiles
            .Where(d => d.UserId == callerId)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;

        // Computed-on-read completion (ARCHITECTURE.md §7 / AppointmentStateMachine's doc comment) —
        // this is the call site that finally exercises TryAutoComplete, run before the scope
        // filter below so a just-elapsed Scheduled appointment is correctly bucketed as Past.
        var dueForAutoComplete = await _db.Appointments
            .Where(a => a.Status == AppointmentStatus.Scheduled && a.SlotEndUtc <= now)
            .Where(a => a.PatientUserId == callerId || (callerDoctorProfileId != null && a.DoctorProfileId == callerDoctorProfileId))
            .ToListAsync(cancellationToken);
        if (dueForAutoComplete.Count > 0)
        {
            foreach (var appointment in dueForAutoComplete)
            {
                if (AppointmentStateMachine.TryAutoComplete(appointment, now, out var history))
                {
                    _db.AppointmentStatusHistories.Add(history!);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        var appointmentsQuery = _db.Appointments
            .Include(a => a.DoctorProfile)
            .Where(a => a.PatientUserId == callerId || (callerDoctorProfileId != null && a.DoctorProfileId == callerDoctorProfileId));

        appointmentsQuery = query.Scope == AppointmentListScope.Upcoming
            ? appointmentsQuery.Where(a => a.SlotEndUtc > now && !TerminalStatuses.Contains(a.Status))
            : appointmentsQuery.Where(a => a.SlotEndUtc <= now || TerminalStatuses.Contains(a.Status));

        appointmentsQuery = query.Scope == AppointmentListScope.Upcoming
            ? appointmentsQuery.OrderBy(a => a.SlotStartUtc)
            : appointmentsQuery.OrderByDescending(a => a.SlotStartUtc);

        var totalCount = await appointmentsQuery.CountAsync(cancellationToken);

        var appointments = await appointmentsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = appointments.Select(a => new AppointmentSummaryDto(
            a.Id, a.DoctorProfileId, a.DoctorProfile.FullName, a.SlotStartUtc, a.SlotEndUtc, a.Status, a.ConsultationFee, a.Currency)).ToList();

        return new PagedResult<AppointmentSummaryDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<CancelAppointmentResult> CancelAsync(Guid appointmentId, Guid callerId, bool callerIsAdmin, RequestCancelAppointmentDto dto, CancellationToken cancellationToken = default)
    {
        var (appointment, cancelledByStatus, error) = await AuthorizeAsync(appointmentId, callerId, callerIsAdmin, cancellationToken);
        if (error is not null)
        {
            return new CancelAppointmentResult(error.Value);
        }

        int refundPercentage;
        if (callerIsAdmin)
        {
            // An admin override bypasses the cancellation window entirely — full refund by default.
            refundPercentage = 100;
        }
        else
        {
            var policyResult = CancellationPolicy.Evaluate(DateTime.UtcNow, appointment!.SlotStartUtc, _policyOptions.RefundTiers);
            if (!policyResult.IsAllowed)
            {
                return new CancelAppointmentResult(CancelAppointmentStatus.CancellationWindowClosed);
            }

            refundPercentage = policyResult.RefundPercentage;
        }

        var refundResult = await _refundService.CancelAndRefundAsync(
            appointmentId,
            cancelledByStatus!.Value,
            callerId,
            new CancelAppointmentDto(dto.Reason, refundPercentage),
            cancellationToken);

        // CancelAndRefundAsync only returns AppointmentNotFound/NotCancellable for states already
        // ruled out above, so Success is the only remaining outcome in practice.
        return new CancelAppointmentResult(CancelAppointmentStatus.Success, refundResult.Result);
    }

    public async Task<PreviewCancellationResult> PreviewCancellationAsync(Guid appointmentId, Guid callerId, bool callerIsAdmin, CancellationToken cancellationToken = default)
    {
        var (appointment, _, error) = await AuthorizeAsync(appointmentId, callerId, callerIsAdmin, cancellationToken);
        if (error is not null)
        {
            return new PreviewCancellationResult(error.Value);
        }

        bool isAllowed;
        int refundPercentage;
        if (callerIsAdmin)
        {
            isAllowed = true;
            refundPercentage = 100;
        }
        else
        {
            var policyResult = CancellationPolicy.Evaluate(DateTime.UtcNow, appointment!.SlotStartUtc, _policyOptions.RefundTiers);
            isAllowed = policyResult.IsAllowed;
            refundPercentage = policyResult.RefundPercentage;
        }

        var refundAmount = Math.Round(appointment!.ConsultationFee * refundPercentage / 100m, 2);
        return new PreviewCancellationResult(
            CancelAppointmentStatus.Success,
            new CancellationPreviewDto(appointment.Id, isAllowed, refundPercentage, refundAmount, appointment.Currency));
    }

    /// <summary>Shared load + object-level-authorization + cancellability check for CancelAsync/PreviewCancellationAsync.</summary>
    private async Task<(Appointment? Appointment, AppointmentStatus? CancelledByStatus, CancelAppointmentStatus? Error)> AuthorizeAsync(
        Guid appointmentId, Guid callerId, bool callerIsAdmin, CancellationToken cancellationToken)
    {
        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);
        if (appointment is null)
        {
            return (null, null, CancelAppointmentStatus.AppointmentNotFound);
        }

        var doctor = await _db.DoctorProfiles.FirstAsync(d => d.Id == appointment.DoctorProfileId, cancellationToken);

        AppointmentStatus cancelledByStatus;
        if (callerIsAdmin)
        {
            cancelledByStatus = AppointmentStatus.CancelledByAdmin;
        }
        else if (appointment.PatientUserId == callerId)
        {
            cancelledByStatus = AppointmentStatus.CancelledByPatient;
        }
        else if (doctor.UserId == callerId)
        {
            cancelledByStatus = AppointmentStatus.CancelledByDoctor;
        }
        else
        {
            return (null, null, CancelAppointmentStatus.Forbidden);
        }

        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            return (null, null, CancelAppointmentStatus.NotCancellable);
        }

        return (appointment, cancelledByStatus, null);
    }
}
