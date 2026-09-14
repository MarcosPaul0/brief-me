namespace BriefMe.Application.Common.Errors;

/// <summary>Failure messages for the role and role-permission use cases (USR-046, USR-047).</summary>
public static class RoleErrors
{
    public const string NotFound = "Role não encontrada.";

    public const string DuplicateName = "Nome de role já cadastrado.";

    public const string InvalidPermissions = "Uma ou mais permissions informadas não existem.";

    public static class Validation
    {
        public const string RoleIdRequired = "RoleId é obrigatório.";

        public const string NameRequired = "Nome da role é obrigatório.";
    }
}
