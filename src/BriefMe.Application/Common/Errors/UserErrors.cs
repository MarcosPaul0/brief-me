namespace BriefMe.Application.Common.Errors;

/// <summary>
/// Single source of truth for the failure messages returned by the user use cases (USR-*). The email and
/// password validation wording is also consumed by the Auth validators, since both act on the same fields.
/// </summary>
public static class UserErrors
{
    public const string NotFound = "Usuário não encontrado.";

    public const string DuplicateEmail = "E-mail já cadastrado.";

    public const string IncorrectCurrentPassword = "Senha atual incorreta.";

    public const string SamePassword = "Nova senha deve ser diferente da senha atual.";

    public const string CannotDeactivateSelf = "Não é permitido inativar o próprio usuário logado.";

    public const string CannotDeleteSelf = "Não é permitido excluir o próprio usuário logado.";

    public const string CannotChangeOwnRole = "Não é permitido alterar o perfil do próprio usuário logado.";

    public static class Validation
    {
        public const string UserIdRequired = "UserId é obrigatório.";

        public const string FullNameRequired = "Nome é obrigatório.";

        public const string EmailRequired = "E-mail é obrigatório.";

        public const string InvalidEmailFormat = "E-mail em formato inválido.";

        public const string PasswordRequired = "Senha é obrigatória.";

        public const string InvalidStatus = "Status inválido.";

        public const string InvalidRole = "Perfil inválido.";

        public const string InvalidSortField = "SortField inválido.";

        public const string PageOutOfRange = "Page deve ser maior ou igual a 1.";

        public static string PageSizeOutOfRange(int maxPageSize) => $"PageSize deve estar entre 1 e {maxPageSize}.";
    }
}
