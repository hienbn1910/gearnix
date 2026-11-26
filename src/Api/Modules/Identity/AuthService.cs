using Gearnix.Api.Common.Auth;
using Gearnix.Api.Common.Errors;
using Gearnix.Api.Data;
using Gearnix.Api.Modules.Identity.Dtos;
using Gearnix.Api.Modules.Identity.Entities;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Gearnix.Api.Modules.Identity;

/// <summary>Register, sign in, sign out and "who am I" (Workflow.md §9).</summary>
public sealed class AuthService(
    AppDbContext dbContext,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager)
{
    public async Task<Result<MeResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = new AppUser
        {
            UserName = email, // the email is the sign-in name; its unique index guards against duplicates
            Email = email,
            FullName = request.FullName.Trim(),
            PhoneNumber = NullIfBlank(request.Phone),
            City = NullIfBlank(request.City),
        };

        // The user and its role are saved together, or not at all.
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            IdentityResult created;
            try
            {
                created = await userManager.CreateAsync(user, request.Password);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: UserNameUniqueIndex,
            })
            {
                // Two registrations raced past Identity's "email already used?" check; the unique index on the
                // normalized user name (= email) stopped the second one. Roll back, and forget the failed insert so
                // nothing else in this request tries to save it again (Workflow.md §11.3).
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return IdentityErrors.EmailTaken;
            }

            if (!created.Succeeded)
            {
                return IsDuplicate(created) ? IdentityErrors.EmailTaken : throw Failure(created);
            }

            var roleAdded = await userManager.AddToRoleAsync(user, Roles.Customer);
            if (!roleAdded.Succeeded)
            {
                throw Failure(roleAdded);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return new MeResponse(user.Id, email, user.FullName, Roles.Customer);
    }

    public async Task<Result<MeResponse>> LoginAsync(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // Spend the same time as a real password check, so response time does not reveal which emails exist.
            userManager.PasswordHasher.HashPassword(TimingDummyUser, request.Password);
            return IdentityErrors.InvalidCredentials;
        }

        // Counts failures and locks the account after 5 (Workflow.md §11.5). Locked → the same error as a wrong
        // password, even when the password is right, so the lock does not reveal that the account exists.
        var check = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            if (check.IsLockedOut)
            {
                // Identity checks the lock before the password, so a locked account would answer much faster.
                // Pay for a hash here too, so timing reveals neither the lock nor the account.
                userManager.PasswordHasher.HashPassword(TimingDummyUser, request.Password);
            }

            return IdentityErrors.InvalidCredentials;
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return await ToMeAsync(user);
    }

    public Task LogoutAsync() => signInManager.SignOutAsync();

    public async Task<Result<MeResponse>> GetMeAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return IdentityErrors.UserNotFound;
        }

        return await ToMeAsync(user);
    }

    /// <summary>Identity's unique index on the normalized user name (= email).</summary>
    private const string UserNameUniqueIndex = "UserNameIndex";

    private static readonly AppUser TimingDummyUser = new() { FullName = "timing" };

    private async Task<MeResponse> ToMeAsync(AppUser user)
    {
        var role = (await userManager.GetRolesAsync(user)).Single(); // exactly one role per account (Workflow.md §3)
        return new MeResponse(user.Id, user.Email ?? "", user.FullName, role);
    }

    private static bool IsDuplicate(IdentityResult result) =>
        result.Errors.Any(error => error.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
            or nameof(IdentityErrorDescriber.DuplicateUserName));

    private static InvalidOperationException Failure(IdentityResult result) =>
        new("Identity operation failed: " + string.Join("; ", result.Errors.Select(error => error.Code)));

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
