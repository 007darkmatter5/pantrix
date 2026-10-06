namespace Pantrix.Data;

/// <summary>
/// One household's data: its recipes, ingredients, inventory, meal plans, shopping lists and stores.
/// Every account owns a kitchen, and can instead be working in someone else's after joining it.
/// </summary>
public class Kitchen
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    // Null only for a kitchen carried over from before accounts existed, until the first account claims it.
    public int? OwnerId { get; set; }
    public AppUser? Owner { get; set; }

    /// <summary>What the owner gives someone so they can join. Changing it stops the old one working.</summary>
    public string JoinCode { get; set; } = "";
}

/// <summary>Implemented by the records a kitchen owns directly; everything else belongs to one of these.</summary>
public interface IKitchenOwned
{
    int KitchenId { get; set; }
}

/// <summary>A setting for the whole install, such as whether new people can sign up.</summary>
public class AppSetting
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}
