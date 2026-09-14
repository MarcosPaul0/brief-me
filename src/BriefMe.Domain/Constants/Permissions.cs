namespace BriefMe.Domain.Constants;

/// <summary>
/// Single source of truth for the permission catalog (USR-046). Code constants follow the
/// <c>recurso.acao</c> convention and are reused by policies, the seed and the UI. The
/// <see cref="Catalog"/> pairs each code with its display metadata for seeding.
/// </summary>
public static class Permissions
{
    public static class Groups
    {
        public const string Users = "Usuários";
        public const string Access = "Acesso";
        public const string Briefings = "Briefings";
        public const string Tasks = "Tarefas";
        public const string Configuration = "Configuração";
    }

    public static class Users
    {
        public const string View = "users.view";
        public const string Create = "users.create";
        public const string Update = "users.update";
        public const string Delete = "users.delete";
        public const string Manage = "users.manage";
        public const string ResetPassword = "users.reset-password";
    }

    public static class Access
    {
        public const string RolesManage = "roles.manage";
        public const string PermissionsManage = "permissions.manage";
    }

    public static class Briefings
    {
        public const string Approve = "briefings.approve";
        public const string Reject = "briefings.reject";
        public const string RequestAdjustments = "briefings.request-adjustments";
        public const string ViewAll = "briefings.view-all";
        public const string Create = "briefings.create";
        public const string ViewOwn = "briefings.view-own";
    }

    public static class Tasks
    {
        public const string Create = "tasks.create";
        public const string Assign = "tasks.assign";
        public const string Reassign = "tasks.reassign";
        public const string Reopen = "tasks.reopen";
        public const string UpdateStatus = "tasks.update-status";
        public const string Comment = "tasks.comment";
        public const string Deliver = "tasks.deliver";
        public const string ApproveTechnical = "tasks.approve-technical";
        public const string ViewAssigned = "tasks.view-assigned";
        public const string ViewAll = "tasks.view-all";
    }

    public static class Configuration
    {
        public const string SlaConfigure = "sla.configure";
        public const string CategoriesConfigure = "categories.configure";
        public const string CustomFieldsConfigure = "custom-fields.configure";
        public const string ChecklistsConfigure = "checklists.configure";
        public const string AuditView = "audit.view";
        public const string SystemConfigure = "system.configure";
    }

    /// <summary>All permissions to be seeded, with display metadata grouped by module.</summary>
    public static IReadOnlyCollection<PermissionDefinition> Catalog { get; } =
    [
        new(Users.View, "Visualizar usuários", "Consultar a listagem e o detalhe de usuários.", Groups.Users),
        new(Users.Create, "Criar usuários", "Cadastrar novos usuários.", Groups.Users),
        new(Users.Update, "Editar usuários", "Atualizar dados de perfil de usuários.", Groups.Users),
        new(Users.Delete, "Excluir usuários", "Remover logicamente (soft delete) usuários.", Groups.Users),
        new(Users.Manage, "Gerenciar usuários", "Ativar/inativar usuários e ações administrativas.", Groups.Users),
        new(Users.ResetPassword, "Redefinir senha", "Redefinir a senha de outros usuários.", Groups.Users),

        new(Access.RolesManage, "Gerenciar perfis", "Criar, editar e definir permissions de perfis.", Groups.Access),
        new(Access.PermissionsManage, "Gerenciar permissions", "Consultar o catálogo e associar permissions.", Groups.Access),

        new(Briefings.Approve, "Aprovar briefings", "Aprovar briefings submetidos.", Groups.Briefings),
        new(Briefings.Reject, "Rejeitar briefings", "Rejeitar briefings submetidos.", Groups.Briefings),
        new(Briefings.RequestAdjustments, "Solicitar ajustes", "Solicitar ajustes em briefings.", Groups.Briefings),
        new(Briefings.ViewAll, "Ver todos os briefings", "Visualizar todos os briefings do sistema.", Groups.Briefings),
        new(Briefings.Create, "Criar briefings", "Abrir novos briefings.", Groups.Briefings),
        new(Briefings.ViewOwn, "Ver próprios briefings", "Visualizar os briefings do próprio solicitante.", Groups.Briefings),

        new(Tasks.Create, "Criar tarefas", "Criar tarefas de design.", Groups.Tasks),
        new(Tasks.Assign, "Atribuir tarefas", "Atribuir tarefas a designers.", Groups.Tasks),
        new(Tasks.Reassign, "Reatribuir tarefas", "Reatribuir tarefas entre designers.", Groups.Tasks),
        new(Tasks.Reopen, "Reabrir tarefas", "Reabrir tarefas concluídas.", Groups.Tasks),
        new(Tasks.UpdateStatus, "Atualizar status", "Alterar o status de execução de tarefas.", Groups.Tasks),
        new(Tasks.Comment, "Comentar tarefas", "Adicionar comentários em tarefas.", Groups.Tasks),
        new(Tasks.Deliver, "Entregar tarefas", "Registrar a entrega de tarefas.", Groups.Tasks),
        new(Tasks.ApproveTechnical, "Aprovar tecnicamente", "Aprovar tecnicamente a entrega de tarefas.", Groups.Tasks),
        new(Tasks.ViewAssigned, "Ver tarefas atribuídas", "Visualizar as tarefas atribuídas a si.", Groups.Tasks),
        new(Tasks.ViewAll, "Ver todas as tarefas", "Visualizar todas as tarefas do sistema.", Groups.Tasks),

        new(Configuration.SlaConfigure, "Configurar SLA", "Definir os parâmetros de SLA.", Groups.Configuration),
        new(Configuration.CategoriesConfigure, "Configurar categorias", "Gerenciar categorias.", Groups.Configuration),
        new(Configuration.CustomFieldsConfigure, "Configurar campos", "Gerenciar campos personalizados.", Groups.Configuration),
        new(Configuration.ChecklistsConfigure, "Configurar checklists", "Gerenciar checklists.", Groups.Configuration),
        new(Configuration.AuditView, "Ver auditoria", "Consultar os registros de auditoria.", Groups.Configuration),
        new(Configuration.SystemConfigure, "Configurar sistema", "Ajustar configurações gerais do sistema.", Groups.Configuration)
    ];
}

/// <summary>Immutable descriptor of a catalog permission used by the seed (USR-046).</summary>
public sealed record PermissionDefinition(string Code, string Name, string? Description, string Group);
