
namespace CRUDAppUsingADO.Models.Attendance
{
    public sealed class AdminAttendanceEventDto
    {
        public int EventId { get; set; }

        public string EventTitle { get; set; } = "";

        public DateTime EventStartDate { get; set; }

        public DateTime EventCompletionDate { get; set; }
    }

    public sealed class AdminAttendanceReportDto
    {
        public int EventId { get; set; }

        public string EventTitle { get; set; } = "";

        public DateTime AttendanceDate { get; set; }

        // Required for date range validation
        public DateTime EventStartDate { get; set; }

        public DateTime EventCompletionDate { get; set; }

        public int TotalRegistered { get; set; }

        public int Present { get; set; }

        public int Absent { get; set; }

        public List<AdminAttendanceParticipantDto> Participants { get; set; }
            = new();
    }

    public sealed class AdminAttendanceParticipantDto
    {
        public int RegistrationId { get; set; }

        public int ParticipantId { get; set; }

        public string ParticipantName { get; set; } = "";

        public string Email { get; set; } = "";

        public string Mobile { get; set; } = "";

        public string AttendanceStatus { get; set; } = "Absent";

        public string? AttendanceTimeIST { get; set; }
    }
}
