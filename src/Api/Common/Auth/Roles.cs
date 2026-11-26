namespace Gearnix.Api.Common.Auth;

/// <summary>The three roles (Workflow.md §3). Each account has exactly one.</summary>
public static class Roles
{
    public const string Customer = "Customer";
    public const string WarehouseStaff = "WarehouseStaff";
    public const string Admin = "Admin";

    public static IReadOnlyList<string> All { get; } = [Customer, WarehouseStaff, Admin];
}
