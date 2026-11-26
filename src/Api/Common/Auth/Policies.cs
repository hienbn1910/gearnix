namespace Gearnix.Api.Common.Auth;

/// <summary>Authorization policy names used in [Authorize(Policy = ...)] (Workflow.md §3, §7.11).</summary>
public static class Policies
{
    public const string Customer = "Customer";

    /// <summary>WarehouseStaff or Admin: view stock, the ledger and orders.</summary>
    public const string Staff = "Staff";

    /// <summary>Only WarehouseStaff: ship and complete orders (separation of duties, Workflow.md §3).</summary>
    public const string WarehouseStaff = "WarehouseStaff";

    public const string Admin = "Admin";
}
