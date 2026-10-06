namespace Pantrix.Data;

public class AppUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string PasswordHash { get; set; } = "";

    /// <summary>The first account on an install. Admins control who can sign up; they get no access to other kitchens.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>The kitchen this person is working in: their own, or one they have joined.</summary>
    public int KitchenId { get; set; }
    public Kitchen Kitchen { get; set; } = null!;

    // Consecutive wrong passwords, and how long sign-in is refused once there have been too many.
    public int FailedSignIns { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}
