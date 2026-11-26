namespace Gearnix.Api.Modules.Identity.Dtos;

public sealed record RegisterRequest(string Email, string Password, string FullName, string? Phone, string? City);
