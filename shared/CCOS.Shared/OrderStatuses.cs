namespace CCOS.Shared;

/// <summary>Order status values used by checkout, officer management, and persistence.</summary>
public static class OrderStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string PickedUp = "Picked up";
    public const string Canceled = "Canceled";
}