using Asnan.Domain.Enums;

namespace Asnan.Domain.Exceptions;

/// <summary>
/// Thrown by <see cref="Entities.AppointmentStateMachine"/> when a caller
/// attempts a transition not present in its transition table — mapped to
/// HTTP 409 by ExceptionHandlingMiddleware (ARCHITECTURE.md §7).
/// </summary>
public class InvalidAppointmentTransitionException : Exception
{
    public AppointmentStatus From { get; }

    public AppointmentStatus To { get; }

    public InvalidAppointmentTransitionException(AppointmentStatus from, AppointmentStatus to)
        : base($"Cannot transition an appointment from '{from}' to '{to}'.")
    {
        From = from;
        To = to;
    }
}
