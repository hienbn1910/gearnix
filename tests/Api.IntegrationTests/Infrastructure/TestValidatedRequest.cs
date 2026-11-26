using Gearnix.Api.Common.Errors;

namespace Gearnix.Api.IntegrationTests.Infrastructure;

/// <summary>A test-only request body with a validator, used to test the validation pipeline.</summary>
public sealed record TestValidatedRequest(string Name, int Quantity, ErrorKind Kind);
