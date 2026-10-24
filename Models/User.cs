namespace CRUDAppUsingADO.Models;
    public class User
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? PasswordHash { get; set; }
        public string? PasswordSalt { get; set; }
        public string RoleName { get; set; } = "User";
        public bool IsActive { get; set; }
        public bool IsEmailVerified { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutUntil { get; set; }
        public string? GoogleId { get; set; }
        public string? ProfilePicUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

