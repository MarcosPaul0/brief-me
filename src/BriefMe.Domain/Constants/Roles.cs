namespace BriefMe.Domain.Constants;

public static class Roles
{
    public const string Administrador = "Administrador";
    public const string GestorDeDesign = "Gestor de Design";
    public const string Designer = "Designer";
    public const string Solicitante = "Solicitante";

    public static IReadOnlyCollection<string> All { get; } =
    [
        Administrador,
        GestorDeDesign,
        Designer,
        Solicitante
    ];

    /// <summary>
    /// Native role definitions with their permission sets (USR-047). Single source of truth reused by
    /// the role seed. Codes reference <see cref="Permissions"/> constants so every entry is guaranteed
    /// to exist in the catalog (USR-046). <see cref="Administrador"/> receives the whole catalog.
    /// </summary>
    /// <remarks>
    /// The backlog also cites <c>briefings.view-related</c> (Designer) and finer-grained Solicitante
    /// actions (edit / save-draft / cancel / track / approve-delivery) that have no code in the
    /// USR-046 catalog. They are intentionally omitted here — the seed only maps to codes that exist.
    /// </remarks>
    public static IReadOnlyCollection<RoleDefinition> Catalog { get; } =
    [
        new(
            Administrador,
            "Acesso total ao sistema.",
            Permissions.Catalog.Select(permission => permission.Code).ToArray()),

        new(
            GestorDeDesign,
            "Gestão de briefings e tarefas do setor de design.",
            [
                Permissions.Briefings.Approve,
                Permissions.Briefings.Reject,
                Permissions.Briefings.RequestAdjustments,
                Permissions.Briefings.ViewAll,
                Permissions.Tasks.Create,
                Permissions.Tasks.Assign,
                Permissions.Tasks.Reassign,
                Permissions.Tasks.Reopen,
                Permissions.Tasks.ApproveTechnical,
                Permissions.Tasks.ViewAll
            ]),

        new(
            Designer,
            "Execução e entrega das tarefas atribuídas.",
            [
                Permissions.Tasks.ViewAssigned,
                Permissions.Tasks.UpdateStatus,
                Permissions.Tasks.Comment,
                Permissions.Tasks.Deliver
            ]),

        new(
            Solicitante,
            "Abertura e acompanhamento dos próprios briefings.",
            [
                Permissions.Briefings.Create,
                Permissions.Briefings.ViewOwn
            ])
    ];
}

/// <summary>Immutable descriptor of a native role and its permission codes used by the seed (USR-047).</summary>
public sealed record RoleDefinition(string Name, string? Description, IReadOnlyCollection<string> PermissionCodes);
