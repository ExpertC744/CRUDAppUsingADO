using System.ComponentModel.DataAnnotations;

namespace CRUDAppUsingADO.Models.Attendance;

public sealed class AttendanceSendOtpDto
{
    [Range(1, int.MaxValue)]
    public int EventId { get; set; }

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = "";
}

public sealed class AttendanceVerifyOtpDto
{
    [Range(1, int.MaxValue)]
    public int EventId { get; set; }

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = "";

    [Required, RegularExpression(@"^\d{6}$",
        ErrorMessage = "Enter a valid 6-digit OTP.")]
    public string OTP { get; set; } = "";
}

public sealed class RegisteredEventParticipant
{
    public int ParticipantId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
}

public sealed class AttendanceEventDates
{
    public DateTime EventStartDate { get; set; }
    public DateTime EventCompletionDate { get; set; }
}