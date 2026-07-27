using CivicSignal.Application.Identity;

namespace CivicSignal.Infrastructure.Identity;

internal static class IdentitySeedData
{
    public static readonly Guid AdministratorRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OperatorRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ReviewerRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ReporterRoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static ApplicationRole[] Roles =>
    [
        CreateRole(AdministratorRoleId, CivicSignalRoles.Administrator),
        CreateRole(OperatorRoleId, CivicSignalRoles.Operator),
        CreateRole(ReviewerRoleId, CivicSignalRoles.Reviewer),
        CreateRole(ReporterRoleId, CivicSignalRoles.Reporter)
    ];

    private static ApplicationRole CreateRole(Guid id, string name)
    {
        return new ApplicationRole
        {
            Id = id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ConcurrencyStamp = id.ToString()
        };
    }
}
