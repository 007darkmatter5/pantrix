namespace Pantrix.Data;

public class AppUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string PasswordHash { get; set; } = "";

    // Consecutive wrong passwords, and how long sign-in is refused once there have been too many.
    public int FailedSignIns { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}
