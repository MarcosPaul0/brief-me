# Backlog Técnico — Módulo de Usuários e Acesso

**Sistema de Gestão de Briefings para o Setor de Design**
Baseado nos requisitos **RF001 – Autenticação**, **RF002 – Controle de Perfis** e **RF003 – Controle de Permissões**.

> Este é o **primeiro módulo** da aplicação e será a base de autenticação/autorização de todos os demais. As tarefas estão organizadas por camada da Clean Architecture e ordenadas para uma implementação incremental e desacoplada.

---

## Convenções e premissas

- **Stack:** .NET 10, Blazor, EF Core, PostgreSQL, Clean Architecture, DDD, Repository + Unit of Work, CQRS (onde fizer sentido), FluentValidation, JWT + Refresh Token, BCrypt, auditoria e SOLID.
- **Restrição obrigatória:** **NÃO** utilizar ASP.NET Identity. Toda autenticação/autorização é implementada manualmente sobre `Microsoft.AspNetCore.Authentication.JwtBearer` e `Microsoft.AspNetCore.Authorization`.
- **Camadas / projetos sugeridos:**
  - `Domain` — entidades, value objects, regras de negócio, interfaces de repositório.
  - `Application` — commands/queries (CQRS), handlers, DTOs, validators, interfaces de serviço (IJwtService, IPasswordHasher, IUnitOfWork, ICurrentUser).
  - `Infrastructure` — `DbContext`, configurações EF, repositórios, UoW, JwtService, BcryptPasswordHasher, migrations, seed.
  - `Api` — endpoints REST (controllers ou minimal APIs), pipeline de autenticação/autorização, tratamento global de erros.
  - `Web` (Blazor) — UI, `AuthenticationStateProvider` customizado, rotas protegidas.
- **Identificadores:** `Guid` (padrão) para todas as chaves primárias.
- **Auditoria:** `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` em todas as entidades auditáveis.
- **Soft delete:** `IsDeleted`, `DeletedAt`, `DeletedBy` com *global query filter* no EF.
- **Estimativas:** `Baixa` (≈ até 0,5 dia), `Média` (≈ 1–2 dias), `Alta` (≈ 3+ dias / complexidade/risco elevados).
- **Nomenclatura de tarefas:** `USR-XXX`.

### Ordem sugerida de execução (ondas)

1. **Fundação:** USR-001 → USR-003 → USR-004 → entidades (005–009) → EF/migration (002) → repositórios (010–014).
2. **Aplicação:** contratos + CQRS (015) → casos de uso (016–024).
3. **Segurança/Infra:** BCrypt (025) → JWT (026) → refresh (027) → pipeline auth (028) → autorização (029–032).
4. **Exposição:** APIs (033–037) → Blazor (038–045).
5. **Dados e qualidade:** seeds (046–048) → testes unitários (049–051) → testes de integração (052–054).

---

# 1. Modelagem do Banco de Dados

## USR-001 — Modelagem lógica de dados e dicionário de dados

**Título:** Definir o modelo relacional do módulo de Usuários e Acesso (ERD + dicionário de dados).

**Objetivo:** Produzir o desenho lógico das tabelas `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions` e `RefreshTokens`, com colunas, tipos, relacionamentos, índices e constraints, servindo de referência para a implementação EF Core.

**Regras:**
- Um usuário pode possuir uma ou mais roles (N:N via `UserRoles`).
- Uma role pode possuir uma ou mais permissions (N:N via `RolePermissions`).
- E-mail de usuário é único (desconsiderando registros com soft delete).
- Nome de role e código de permission são únicos.
- Todas as tabelas de entidade principal possuem colunas de auditoria; entidades com ciclo de vida gerenciável possuem soft delete.

**Detalhes técnicos:**
- Definir tipos PostgreSQL: `uuid` (PK), `varchar` com limites, `timestamptz` para datas, `boolean` para flags.
- `RefreshTokens`: colunas `Token` (hash), `ExpiresAt`, `RevokedAt`, `ReplacedByToken`, `UserId` (FK), `CreatedByIp`.
- Especificar índices: `IX_Users_Email` (único filtrado por `IsDeleted = false`), `IX_Roles_Name` (único), `IX_Permissions_Code` (único), `IX_RefreshTokens_Token`, `IX_RefreshTokens_UserId`.
- Especificar FKs com `ON DELETE` apropriado (ex.: `RefreshTokens.UserId` → `Users.Id` cascade; join tables com cascade).
- Entregar diagrama (ERD) + dicionário de dados em `/docs`.

**Dependências:** Nenhuma.

**Critérios de aceite:**
- **Given** os requisitos RF001–RF003, **When** o modelo é revisado, **Then** todas as 6 tabelas, seus relacionamentos N:N, índices únicos e colunas de auditoria/soft delete estão documentados e aprovados.
- **Given** o dicionário de dados, **When** um desenvolvedor consulta uma coluna, **Then** encontra tipo, obrigatoriedade, default e descrição.

**Estimativa:** Média.

---

## USR-002 — Configuração EF Core, DbContext e migration inicial

**Título:** Implementar `DbContext`, mapeamentos Fluent API e a migration inicial do schema.

**Objetivo:** Materializar o modelo lógico (USR-001) no PostgreSQL via EF Core code-first, com `IEntityTypeConfiguration` por entidade, índices, constraints, FKs, *global query filters* de soft delete e interceptor de auditoria.

**Regras:**
- Configurações de mapeamento ficam separadas das entidades (`IEntityTypeConfiguration<T>` em `Infrastructure/Persistence/Configurations`).
- *Global query filter* deve excluir registros com `IsDeleted = true` por padrão.
- Preenchimento de auditoria é automático via `SaveChangesInterceptor` (não manual nos handlers).
- Migrations são versionadas e reprodutíveis; nenhum ajuste manual no banco.

**Detalhes técnicos:**
- `AppDbContext : DbContext` com `DbSet` para cada entidade.
- `ApplyConfigurationsFromAssembly` no `OnModelCreating`.
- Índices únicos filtrados: `HasIndex(x => x.Email).IsUnique().HasFilter("\"IsDeleted\" = false")`.
- Interceptor `AuditableEntitySaveChangesInterceptor` que popula `CreatedAt/By` e `UpdatedAt/By` a partir de `ICurrentUser`.
- Comando de migration: `dotnet ef migrations add InitialUsersAccess -p Infrastructure -s Api`.
- Provider: `Npgsql.EntityFrameworkCore.PostgreSQL`.

**Dependências:** USR-003, USR-004, USR-005, USR-006, USR-007, USR-008, USR-009 (as entidades precisam existir para mapeamento). USR-015 (interface `ICurrentUser`) para o interceptor de auditoria.

**Critérios de aceite:**
- **Given** as entidades e configurações, **When** `dotnet ef database update` é executado, **Then** as 6 tabelas são criadas com PKs, FKs, índices únicos e constraints previstos.
- **Given** uma entidade auditável salva, **When** `SaveChanges` é chamado, **Then** as colunas de auditoria são preenchidas automaticamente.
- **Given** um registro com `IsDeleted = true`, **When** consultado sem `IgnoreQueryFilters`, **Then** não é retornado.

**Estimativa:** Alta.

---

# 2. Entidades de Domínio

## USR-003 — Classe base de entidade, auditoria e soft delete

**Título:** Criar abstrações base do domínio (`Entity`, `AggregateRoot`, `IAuditableEntity`, `ISoftDelete`).

**Objetivo:** Fornecer a base reutilizável para todas as entidades, com identidade, igualdade por Id, suporte a auditoria, soft delete e coleção de *domain events*.

**Regras:**
- Igualdade e hash baseados no `Id`.
- Entidades não expõem *setters* públicos; alterações ocorrem por métodos de domínio.
- Soft delete apenas marca o registro; nunca remove fisicamente.

**Detalhes técnicos:**
- `abstract class Entity { public Guid Id { get; protected set; } }` com override de `Equals`/`GetHashCode`.
- `abstract class AggregateRoot : Entity` com `IReadOnlyCollection<IDomainEvent> DomainEvents` + `RaiseDomainEvent`/`ClearDomainEvents`.
- `interface IAuditableEntity { DateTime CreatedAt; string? CreatedBy; DateTime? UpdatedAt; string? UpdatedBy; }`.
- `interface ISoftDelete { bool IsDeleted; DateTime? DeletedAt; string? DeletedBy; }` + método `MarkAsDeleted`.

**Dependências:** Nenhuma.

**Critérios de aceite:**
- **Given** duas instâncias com o mesmo `Id`, **When** comparadas, **Then** são consideradas iguais.
- **Given** uma entidade que implementa `ISoftDelete`, **When** `MarkAsDeleted(user)` é chamado, **Then** `IsDeleted = true` e `DeletedAt/By` são preenchidos.

**Estimativa:** Média.

---

## USR-004 — Value Object Email

**Título:** Criar value object `Email` com validação de formato.

**Objetivo:** Encapsular e validar o e-mail como conceito de domínio, garantindo formato válido e normalização.

**Regras:**
- E-mail inválido não pode ser instanciado (falha explícita no domínio).
- Normalizar para *lowercase* e *trim*.
- Comparação por valor (não por referência).

**Detalhes técnicos:**
- `record Email` (ou classe imutável) com *factory* `Email.Create(string)` retornando `Result<Email>` ou lançando `DomainException`.
- Validação por regex/`MailAddress`.
- Conversão implícita/explícita para `string` para persistência.

**Dependências:** USR-003.

**Critérios de aceite:**
- **Given** `"USER@Empresa.com "`, **When** `Email.Create` é chamado, **Then** o valor resultante é `"user@empresa.com"`.
- **Given** `"texto-invalido"`, **When** `Email.Create` é chamado, **Then** ocorre falha/erro de domínio.

**Estimativa:** Baixa.

---

## USR-005 — Entidade User

**Título:** Implementar o agregado `User` com regras de negócio encapsuladas.

**Objetivo:** Modelar o usuário com identidade, e-mail, hash de senha, status (ativo/inativo), auditoria, soft delete e relacionamento com roles.

**Regras:**
- Senha nunca é armazenada em texto puro; a entidade guarda apenas o `PasswordHash`.
- Usuário inativo não pode autenticar (regra validada no caso de uso de Login).
- Alteração de senha exige geração de novo hash.
- Nome e e-mail são obrigatórios.
- Métodos de domínio: `Create`, `ChangePassword`, `UpdateProfile`, `Activate`, `Deactivate`, `AssignRole`, `RemoveRole`.

**Detalhes técnicos:**
- `User : AggregateRoot, IAuditableEntity, ISoftDelete`.
- Propriedades: `FullName`, `Email` (VO), `PasswordHash`, `Status` (enum `Active`/`Inactive`), coleção `UserRoles`.
- Construtor privado + *factory* estática `User.Create(...)`.
- Encapsular coleção de roles como `IReadOnlyCollection`.

**Dependências:** USR-003, USR-004.

**Critérios de aceite:**
- **Given** dados válidos, **When** `User.Create` é chamado, **Then** o usuário é criado com status `Active` e sem roles.
- **Given** um usuário ativo, **When** `Deactivate()` é chamado, **Then** `Status = Inactive`.
- **Given** um novo hash, **When** `ChangePassword(hash)` é chamado, **Then** `PasswordHash` é atualizado e `UpdatedAt` marcado.

**Estimativa:** Média.

---

## USR-006 — Entidade Role

**Título:** Implementar a entidade `Role` (perfil de acesso).

**Objetivo:** Modelar os perfis (Administrador, Gestor de Design, Designer, Solicitante) com nome único e associação a permissions.

**Regras:**
- Nome de role é único e obrigatório.
- Uma role agrega zero ou mais permissions.
- Métodos: `Create`, `Rename`, `AddPermission`, `RemovePermission`.

**Detalhes técnicos:**
- `Role : AggregateRoot, IAuditableEntity`.
- Propriedades: `Name`, `Description`, coleção `RolePermissions`.
- Opcional: flag `IsSystemRole` para impedir exclusão dos perfis nativos.

**Dependências:** USR-003.

**Critérios de aceite:**
- **Given** um nome válido, **When** `Role.Create` é chamado, **Then** a role é criada sem permissions.
- **Given** uma permission, **When** `AddPermission` é chamado duas vezes com a mesma permission, **Then** não há duplicidade.

**Estimativa:** Média.

---

## USR-007 — Entidade Permission

**Título:** Implementar a entidade `Permission`.

**Objetivo:** Modelar permissões granulares (ex.: `users.manage`, `briefings.approve`, `sla.configure`) com código único.

**Regras:**
- `Code` é único, obrigatório e imutável após criação.
- `Code` segue convenção `recurso.acao` (ex.: `users.create`).

**Detalhes técnicos:**
- `Permission : Entity, IAuditableEntity`.
- Propriedades: `Code`, `Name`, `Description`, `Group` (ex.: "Usuários", "Briefings").
- Considerar catálogo de permissions como constantes em `Domain` para reuso em policies e seed.

**Dependências:** USR-003.

**Critérios de aceite:**
- **Given** um código válido, **When** `Permission.Create` é chamado, **Then** a permission é criada.
- **Given** o catálogo de permissions, **When** consultado, **Then** todos os códigos são únicos e seguem a convenção `recurso.acao`.

**Estimativa:** Baixa.

---

## USR-008 — Entidade RefreshToken

**Título:** Implementar a entidade `RefreshToken`.

**Objetivo:** Modelar tokens de atualização com expiração, revogação e rastreabilidade de rotação, vinculados a um usuário.

**Regras:**
- Um refresh token é válido se não expirado e não revogado.
- Ao rotacionar, o token antigo é revogado e aponta para o substituto (`ReplacedByToken`).
- Token é armazenado como hash (nunca em texto puro).

**Detalhes técnicos:**
- `RefreshToken : Entity`.
- Propriedades: `TokenHash`, `UserId`, `ExpiresAt`, `CreatedByIp`, `RevokedAt`, `RevokedByIp`, `ReplacedByTokenHash`.
- Propriedades calculadas: `IsExpired`, `IsRevoked`, `IsActive`.
- Métodos: `Revoke(ip, replacedBy?)`.

**Dependências:** USR-003, USR-005.

**Critérios de aceite:**
- **Given** um token com `ExpiresAt` no passado, **When** `IsActive` é avaliado, **Then** retorna `false`.
- **Given** um token ativo, **When** `Revoke` é chamado, **Then** `RevokedAt` é preenchido e `IsActive` retorna `false`.

**Estimativa:** Média.

---

## USR-009 — Relacionamentos UserRoles e RolePermissions

**Título:** Implementar as entidades de junção `UserRole` e `RolePermission`.

**Objetivo:** Materializar os relacionamentos N:N entre usuários/roles e roles/permissions de forma explícita (join entities), permitindo auditoria da associação.

**Regras:**
- Não pode haver associação duplicada (mesmo `UserId`+`RoleId` / `RoleId`+`PermissionId`).
- Associações registram quem e quando (auditoria).

**Detalhes técnicos:**
- `UserRole { UserId, RoleId }` e `RolePermission { RoleId, PermissionId }` como *join entities* com chave composta.
- Configurar chave composta e FKs no EF (USR-002).
- Métodos de agregado em `User`/`Role` controlam a criação/remoção das associações.

**Dependências:** USR-005, USR-006, USR-007.

**Critérios de aceite:**
- **Given** um usuário e uma role, **When** a role é atribuída duas vezes, **Then** existe apenas uma associação persistida.
- **Given** uma role com permissions, **When** a role é carregada com `Include`, **Then** as permissions associadas são retornadas.

**Estimativa:** Média.

---

# 3. Repositórios e Unit of Work

## USR-010 — IRepository genérico + Unit of Work

**Título:** Definir contratos de repositório genérico e `IUnitOfWork` e implementá-los sobre EF Core.

**Objetivo:** Fornecer a base de acesso a dados desacoplada (interfaces no `Domain`/`Application`, implementação na `Infrastructure`), com controle transacional via Unit of Work.

**Regras:**
- Interfaces de repositório ficam na camada de abstração; implementações na `Infrastructure`.
- Nenhum handler chama `DbContext` diretamente; apenas repositórios + UoW.
- `SaveChanges` só ocorre via `IUnitOfWork.CommitAsync`.

**Detalhes técnicos:**
- `IRepository<T>` com `GetByIdAsync`, `AddAsync`, `Update`, `Remove`, `Query` (IQueryable para specs/filtros).
- `IUnitOfWork` com `CommitAsync(CancellationToken)` e, opcionalmente, controle de transação explícito.
- Implementação `EfRepository<T>` + `UnitOfWork` injetados via DI (`Scoped`).
- Considerar *Specification pattern* para consultas reutilizáveis (opcional).

**Dependências:** USR-002.

**Critérios de aceite:**
- **Given** uma entidade nova, **When** `AddAsync` + `CommitAsync` são chamados, **Then** ela é persistida.
- **Given** duas operações no mesmo escopo, **When** `CommitAsync` falha, **Then** nenhuma alteração é persistida (atomicidade).

**Estimativa:** Alta.

---

## USR-011 — UserRepository

**Título:** Implementar `IUserRepository` e `UserRepository`.

**Objetivo:** Expor operações específicas de usuário além do CRUD genérico.

**Regras:**
- Consulta por e-mail respeita o *global query filter* (não retorna deletados) por padrão.
- Verificação de unicidade de e-mail deve ignorar o próprio registro em edições.

**Detalhes técnicos:**
- Métodos: `GetByEmailAsync(Email)`, `EmailExistsAsync(Email, Guid? ignoreId)`, `GetByIdWithRolesAsync(Guid)`, `GetPagedAsync(filtro, page, size, sort)`.
- Carregar roles/permissions com `Include`/`ThenInclude` quando necessário para autenticação.

**Dependências:** USR-010, USR-005, USR-009.

**Critérios de aceite:**
- **Given** um e-mail existente, **When** `GetByEmailAsync` é chamado, **Then** o usuário e suas roles são retornados.
- **Given** um e-mail já usado por outro usuário, **When** `EmailExistsAsync(email, ignoreId)` é chamado com id diferente, **Then** retorna `true`.

**Estimativa:** Média.

---

## USR-012 — RoleRepository

**Título:** Implementar `IRoleRepository` e `RoleRepository`.

**Objetivo:** Expor consultas de perfis, incluindo carregamento de permissions.

**Regras:**
- Nome de role é único (validação apoiada por consulta).

**Detalhes técnicos:**
- Métodos: `GetByNameAsync`, `GetByIdWithPermissionsAsync`, `GetAllAsync`, `NameExistsAsync(name, ignoreId)`.

**Dependências:** USR-010, USR-006, USR-009.

**Critérios de aceite:**
- **Given** uma role existente, **When** `GetByIdWithPermissionsAsync` é chamado, **Then** as permissions associadas são retornadas.

**Estimativa:** Baixa.

---

## USR-013 — PermissionRepository

**Título:** Implementar `IPermissionRepository` e `PermissionRepository`.

**Objetivo:** Expor consultas de permissions para associação e autorização.

**Regras:**
- `Code` é único.

**Detalhes técnicos:**
- Métodos: `GetByCodeAsync`, `GetByIdsAsync(IEnumerable<Guid>)`, `GetAllAsync`, `GetByCodesAsync`.

**Dependências:** USR-010, USR-007.

**Critérios de aceite:**
- **Given** uma lista de códigos, **When** `GetByCodesAsync` é chamado, **Then** apenas as permissions correspondentes são retornadas.

**Estimativa:** Baixa.

---

## USR-014 — RefreshTokenRepository

**Título:** Implementar `IRefreshTokenRepository` e `RefreshTokenRepository`.

**Objetivo:** Persistir e consultar refresh tokens para os fluxos de refresh, logout e revogação.

**Regras:**
- Consulta por token deve localizar pelo hash.
- Revogação em cascata dos tokens ativos de um usuário no logout global.

**Detalhes técnicos:**
- Métodos: `GetByTokenHashAsync`, `GetActiveByUserAsync(Guid)`, `RevokeAllActiveForUserAsync(Guid, ip)`.
- Índice em `TokenHash` para performance.

**Dependências:** USR-010, USR-008.

**Critérios de aceite:**
- **Given** um hash de token válido, **When** `GetByTokenHashAsync` é chamado, **Then** o token correspondente é retornado.
- **Given** um usuário com tokens ativos, **When** `RevokeAllActiveForUserAsync` é chamado, **Then** todos são marcados como revogados.

**Estimativa:** Média.

---

# 4. Casos de Uso (Application / CQRS)

## USR-015 — Contratos de aplicação + infraestrutura de CQRS e validação

**Título:** Definir interfaces de serviço, dispatcher de CQRS e pipeline de validação.

**Objetivo:** Preparar a camada de aplicação: contratos (`IJwtService`, `IPasswordHasher`, `IRefreshTokenService`, `ICurrentUser`), mediador de commands/queries e *behavior* de validação com FluentValidation.

**Regras:**
- Handlers dependem de abstrações, nunca de implementações concretas de infraestrutura.
- Toda entrada de command/query passa por validação antes do handler.
- Erros de validação retornam de forma padronizada (não como exceção não tratada).

**Detalhes técnicos:**
- Interfaces em `Application/Abstractions`: `IJwtService`, `IPasswordHasher`, `IRefreshTokenService`, `ICurrentUser`, `IUnitOfWork` (já em USR-010).
- CQRS via MediatR (ou dispatcher próprio) com `IRequest`/`IRequestHandler`.
- `ValidationBehavior<TRequest,TResponse>` no pipeline executando os `IValidator<T>`.
- Padrão de retorno `Result`/`Result<T>` para erros de negócio (evitar exceções para fluxo esperado).

**Dependências:** USR-010.

**Critérios de aceite:**
- **Given** um command inválido, **When** enviado ao mediador, **Then** o handler não é executado e um resultado de falha de validação é retornado.
- **Given** um handler, **When** inspecionado, **Then** não referencia tipos concretos de `Infrastructure`.

**Estimativa:** Média.

---

## USR-016 — Caso de uso: Login (RF001)

**Título:** Implementar `LoginCommand` + handler.

**Objetivo:** Autenticar por e-mail e senha, retornando access token (JWT) e refresh token.

**Regras:**
- Credenciais inválidas retornam mensagem genérica (não revelar se o e-mail existe).
- Usuário inativo ou deletado não autentica.
- Comparar senha via `IPasswordHasher.Verify` (BCrypt).
- Emitir access token com claims de identidade + roles + permissions e um refresh token persistido.

**Detalhes técnicos:**
- Input: `Email`, `Password`. Output: `AccessToken`, `RefreshToken`, `ExpiresIn`, dados básicos do usuário.
- Buscar usuário com roles/permissions (`GetByEmailAsync`).
- Gerar tokens via `IJwtService` + `IRefreshTokenService`; persistir refresh token com `CreatedByIp`.
- Validator: e-mail obrigatório/válido, senha obrigatória.

**Dependências:** USR-011, USR-014, USR-015, USR-025, USR-026, USR-027.

**Critérios de aceite:**
- **Given** credenciais válidas de usuário ativo, **When** o login é executado, **Then** um access token válido e um refresh token são retornados.
- **Given** senha incorreta, **When** o login é executado, **Then** retorna falha de autenticação genérica e nenhum token.
- **Given** usuário inativo, **When** o login é executado, **Then** o acesso é negado.

**Estimativa:** Alta.

---

## USR-017 — Caso de uso: Refresh Token (com rotação)

**Título:** Implementar `RefreshTokenCommand` + handler.

**Objetivo:** Emitir novo access token a partir de um refresh token válido, aplicando rotação (revoga o antigo e emite um novo).

**Regras:**
- Refresh token expirado ou revogado é rejeitado.
- Ao usar um refresh token, ele é revogado e substituído por um novo (rotação).
- Reuso de token já revogado é tratado como possível comprometimento (revogar cadeia/tokens ativos do usuário — opcional, recomendado).

**Detalhes técnicos:**
- Input: `RefreshToken`. Output: novo `AccessToken` + novo `RefreshToken`.
- Localizar por hash; validar `IsActive`; revogar com `ReplacedByTokenHash`.
- Reemitir claims atualizadas (roles/permissions atuais do usuário).

**Dependências:** USR-014, USR-015, USR-016, USR-026, USR-027.

**Critérios de aceite:**
- **Given** um refresh token ativo, **When** o refresh é executado, **Then** novos tokens são emitidos e o token anterior fica revogado.
- **Given** um refresh token já revogado, **When** o refresh é executado, **Then** a operação é negada.

**Estimativa:** Alta.

---

## USR-018 — Caso de uso: Logout (revogação)

**Título:** Implementar `LogoutCommand` + handler.

**Objetivo:** Revogar o refresh token corrente (ou todos os ativos do usuário) encerrando a sessão.

**Regras:**
- Logout revoga o refresh token informado; opção de "logout de todos os dispositivos" revoga todos os ativos do usuário.
- Access token permanece válido até expirar (curto TTL); a segurança do encerramento vem da revogação do refresh.

**Detalhes técnicos:**
- Input: `RefreshToken` (ou `UserId` do contexto). Marcar `RevokedAt`.
- Reutilizar `RevokeAllActiveForUserAsync` no modo global.

**Dependências:** USR-014, USR-015.

**Critérios de aceite:**
- **Given** um refresh token ativo, **When** o logout é executado, **Then** o token é revogado e não pode mais ser usado no refresh.

**Estimativa:** Média.

---

## USR-019 — Caso de uso: Cadastro de usuário

**Título:** Implementar `CreateUserCommand` + handler + validator.

**Objetivo:** Criar um novo usuário com senha hasheada e roles atribuídas.

**Regras:**
- E-mail deve ser único.
- Senha deve atender à política mínima (tamanho, complexidade).
- Roles informadas devem existir.
- Senha é hasheada com BCrypt antes de persistir.

**Detalhes técnicos:**
- Input: `FullName`, `Email`, `Password`, `RoleIds[]`.
- Validar unicidade via `EmailExistsAsync`; validar existência das roles.
- `User.Create(...)` + `AssignRole` por role; `IPasswordHasher.Hash`.
- Requer autorização (`users.create`).

**Dependências:** USR-011, USR-012, USR-015, USR-025.

**Critérios de aceite:**
- **Given** dados válidos e e-mail inédito, **When** o cadastro é executado, **Then** o usuário é criado, ativo e com as roles atribuídas.
- **Given** um e-mail já cadastrado, **When** o cadastro é executado, **Then** retorna erro de validação de unicidade.

**Estimativa:** Média.

---

## USR-020 — Caso de uso: Atualização de usuário

**Título:** Implementar `UpdateUserCommand` + handler + validator.

**Objetivo:** Atualizar dados de perfil (nome, e-mail, roles) de um usuário existente.

**Regras:**
- E-mail alterado deve continuar único (ignorando o próprio registro).
- Alteração de roles substitui o conjunto atual pelo informado.
- Não altera senha (fluxo próprio).

**Detalhes técnicos:**
- Input: `UserId`, `FullName`, `Email`, `RoleIds[]`.
- Carregar `GetByIdWithRolesAsync`; aplicar `UpdateProfile` + reconciliação de roles.
- Autorização (`users.update`).

**Dependências:** USR-011, USR-012, USR-015.

**Critérios de aceite:**
- **Given** um usuário existente, **When** os dados são atualizados, **Then** as alterações e a auditoria (`UpdatedAt/By`) são persistidas.
- **Given** um e-mail em uso por outro usuário, **When** a atualização é executada, **Then** retorna erro de unicidade.

**Estimativa:** Média.

---

## USR-021 — Caso de uso: Alteração de senha

**Título:** Implementar `ChangePasswordCommand` + handler + validator.

**Objetivo:** Permitir a troca de senha, validando a senha atual (autoatendimento) ou por reset administrativo.

**Regras:**
- No autoatendimento, a senha atual deve ser verificada antes da troca.
- Nova senha deve atender à política de complexidade e ser diferente da atual.
- Após a troca, considerar revogar refresh tokens ativos (segurança).

**Detalhes técnicos:**
- Input: `UserId`, `CurrentPassword?`, `NewPassword`.
- `IPasswordHasher.Verify` (quando aplicável) → `User.ChangePassword(newHash)`.
- Autorização: próprio usuário ou `users.reset-password`.

**Dependências:** USR-011, USR-015, USR-025, USR-014.

**Critérios de aceite:**
- **Given** a senha atual correta e uma nova senha válida, **When** a troca é executada, **Then** o hash é atualizado e (opcionalmente) sessões são revogadas.
- **Given** a senha atual incorreta, **When** a troca é executada, **Then** a operação é negada.

**Estimativa:** Média.

---

## USR-022 — Caso de uso: Alteração de status (ativar/inativar)

**Título:** Implementar `ChangeUserStatusCommand` + handler.

**Objetivo:** Ativar ou inativar um usuário.

**Regras:**
- Usuário inativado não pode autenticar; considerar revogar seus refresh tokens ativos.
- Não é permitido inativar o próprio usuário administrador logado (evitar *lockout*).

**Detalhes técnicos:**
- Input: `UserId`, `Status`. Chama `Activate()`/`Deactivate()`.
- Autorização (`users.manage`).

**Dependências:** USR-011, USR-015, USR-014.

**Critérios de aceite:**
- **Given** um usuário ativo, **When** inativado, **Then** `Status = Inactive` e seus refresh tokens ativos são revogados.
- **Given** o administrador logado, **When** tenta inativar a si mesmo, **Then** a operação é bloqueada.

**Estimativa:** Baixa.

---

## USR-023 — Caso de uso: Exclusão lógica (soft delete)

**Título:** Implementar `DeleteUserCommand` + handler.

**Objetivo:** Remover logicamente um usuário sem apagar o histórico.

**Regras:**
- Exclusão marca `IsDeleted`; o registro deixa de aparecer nas consultas padrão.
- E-mail de usuário deletado não bloqueia novo cadastro do mesmo e-mail (índice único filtrado por `IsDeleted`).
- Não permitir excluir o próprio usuário logado.

**Detalhes técnicos:**
- `MarkAsDeleted(currentUser)`; revogar refresh tokens ativos.
- Autorização (`users.delete`).

**Dependências:** USR-011, USR-015, USR-014.

**Critérios de aceite:**
- **Given** um usuário, **When** excluído logicamente, **Then** `IsDeleted = true` e ele não aparece em listagens padrão.
- **Given** um usuário deletado, **When** um novo cadastro usa o mesmo e-mail, **Then** o cadastro é permitido.

**Estimativa:** Baixa.

---

## USR-024 — Casos de uso de consulta (por Id, listagem paginada, filtros)

**Título:** Implementar `GetUserByIdQuery` e `GetUsersPagedQuery`.

**Objetivo:** Fornecer consultas otimizadas para as telas de usuário (detalhe e listagem paginada com filtros e ordenação).

**Regras:**
- Listagem retorna dados paginados (total, página, tamanho) e não expõe `PasswordHash`.
- Filtros por nome, e-mail, status e role.

**Detalhes técnicos:**
- Projetar diretamente para DTOs (`Select`) evitando *over-fetching*.
- `PagedResult<UserListItemDto>` com metadados de paginação.
- Ordenação parametrizável (whitelist de campos).

**Dependências:** USR-011, USR-015.

**Critérios de aceite:**
- **Given** uma base com N usuários, **When** a listagem é consultada com `page=2,size=10`, **Then** retorna a página correta com o total geral.
- **Given** um `UserId` válido, **When** `GetUserById` é executado, **Then** retorna o DTO sem o hash de senha.

**Estimativa:** Média.

---

# 5. Infraestrutura de Segurança (JWT / BCrypt / Refresh)

## USR-025 — Serviço de hash de senha (BCrypt)

**Título:** Implementar `IPasswordHasher` com `BcryptPasswordHasher`.

**Objetivo:** Fornecer hash e verificação de senha usando BCrypt (sem ASP.NET Identity).

**Regras:**
- *Work factor* configurável (padrão ≥ 11).
- Nunca logar senha ou hash.

**Detalhes técnicos:**
- Biblioteca `BCrypt.Net-Next`.
- `Hash(string password)` → gera salt embutido; `Verify(password, hash)` → `bool`.
- Registrar como `Singleton`/`Scoped` na DI.

**Dependências:** USR-015.

**Critérios de aceite:**
- **Given** uma senha, **When** `Hash` é chamado duas vezes, **Then** os hashes são diferentes (salt aleatório) e ambos verificam com sucesso.
- **Given** um hash e a senha correta, **When** `Verify` é chamado, **Then** retorna `true`.

**Estimativa:** Baixa.

---

## USR-026 — Serviço de emissão de JWT (access token)

**Título:** Implementar `IJwtService` (`JwtService`) para geração de access tokens.

**Objetivo:** Gerar JWTs assinados contendo claims de identidade, roles e permissions, com expiração curta.

**Regras:**
- Access token de curta duração (ex.: 15 minutos), configurável.
- Assinatura simétrica (HMAC) ou assimétrica (RSA), com chave em configuração segura (nunca hardcoded).
- Claims: `sub` (UserId), `email`, `name`, `role` (múltiplas), `permission` (múltiplas), `jti`, `exp`, `iss`, `aud`.

**Detalhes técnicos:**
- `JwtSettings` (Issuer, Audience, SecretKey/keys, AccessTokenMinutes, RefreshTokenDays) via `IOptions`.
- `System.IdentityModel.Tokens.Jwt` / `Microsoft.IdentityModel.Tokens`.
- `GenerateAccessToken(User)` → `(token, expiresAt)`.

**Dependências:** USR-015, USR-009 (permissions do usuário).

**Critérios de aceite:**
- **Given** um usuário com roles/permissions, **When** o token é gerado, **Then** ele contém as claims de role e permission correspondentes e uma expiração válida.
- **Given** um token gerado, **When** validado com a chave configurada, **Then** a assinatura é válida.

**Estimativa:** Alta.

---

## USR-027 — Serviço de Refresh Token (geração e rotação)

**Título:** Implementar `IRefreshTokenService` (`RefreshTokenService`).

**Objetivo:** Gerar refresh tokens seguros, armazenar apenas o hash e suportar rotação/revogação.

**Regras:**
- Token opaco de alta entropia (criptograficamente seguro).
- Persistir apenas o hash do token; o valor puro só trafega para o cliente.
- Expiração configurável (ex.: 7 dias).

**Detalhes técnicos:**
- Geração via `RandomNumberGenerator` (base64url).
- Hash do token (ex.: SHA-256) para armazenamento e busca.
- `Generate(userId, ip)`, `Rotate(oldToken, ip)`, `Revoke(token, ip)`.

**Dependências:** USR-014, USR-015.

**Critérios de aceite:**
- **Given** uma solicitação de refresh token, **When** gerado, **Then** o banco armazena o hash (não o valor puro) e o cliente recebe o valor puro.
- **Given** um token em rotação, **When** `Rotate` é chamado, **Then** o antigo é revogado apontando para o novo.

**Estimativa:** Média.

---

## USR-028 — Configuração do pipeline de autenticação JWT

**Título:** Configurar `AddAuthentication().AddJwtBearer(...)` e validação de tokens.

**Objetivo:** Habilitar a autenticação JWT na API com parâmetros de validação corretos e tratamento de expiração.

**Regras:**
- Validar issuer, audience, lifetime e assinatura.
- `ClockSkew` reduzido (ex.: 0–1 min) para expiração previsível.
- Respostas 401/403 padronizadas.

**Detalhes técnicos:**
- `TokenValidationParameters` a partir de `JwtSettings`.
- Eventos `OnAuthenticationFailed`/`OnChallenge` para respostas consistentes (ProblemDetails).
- `app.UseAuthentication(); app.UseAuthorization();` na ordem correta.

**Dependências:** USR-026.

**Critérios de aceite:**
- **Given** uma requisição com token válido, **When** acessa um endpoint `[Authorize]`, **Then** é autenticada com as claims do token.
- **Given** um token expirado/adulterado, **When** acessa um endpoint protegido, **Then** retorna 401.

**Estimativa:** Alta.

---

# 6. Autorização (Roles + Permissions via Policies)

## USR-029 — Modelo de claims e mapeamento de permissions

**Título:** Definir a estratégia de claims e o mapeamento de roles→permissions nas claims do token.

**Objetivo:** Padronizar como roles e permissions são representadas nas claims para consumo pelas policies.

**Regras:**
- Cada permission efetiva do usuário (derivada de suas roles) vira uma claim `permission`.
- Roles viram claims `role`.
- Evitar tokens excessivamente grandes: consolidar permissions distintas.

**Detalhes técnicos:**
- Constante `CustomClaimTypes.Permission = "permission"`.
- Ao emitir o token (USR-026), agregar as permissions distintas de todas as roles do usuário.

**Dependências:** USR-026, USR-009.

**Critérios de aceite:**
- **Given** um usuário com duas roles que compartilham uma permission, **When** o token é emitido, **Then** a claim `permission` correspondente aparece uma única vez.

**Estimativa:** Média.

---

## USR-030 — PermissionRequirement + AuthorizationHandler

**Título:** Implementar autorização baseada em permission.

**Objetivo:** Criar o `IAuthorizationRequirement` e o handler que autoriza com base nas claims `permission` do usuário.

**Regras:**
- Acesso concedido apenas se a claim `permission` exigida estiver presente.
- Administrador (por convenção) pode ter *bypass* para todas as permissions, se assim definido.

**Detalhes técnicos:**
- `PermissionRequirement(string permission)` + `PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>`.
- Handler verifica `context.User.HasClaim("permission", requirement.Permission)`.

**Dependências:** USR-029.

**Critérios de aceite:**
- **Given** um usuário com a permission exigida, **When** o handler avalia, **Then** o acesso é concedido.
- **Given** um usuário sem a permission, **When** o handler avalia, **Then** o acesso é negado (403).

**Estimativa:** Alta.

---

## USR-031 — Policy provider dinâmico para permissions

**Título:** Implementar `IAuthorizationPolicyProvider` dinâmico.

**Objetivo:** Permitir usar permissions como policies sem registrar manualmente cada uma (ex.: `[Authorize(Policy = "users.create")]`).

**Regras:**
- Policies são resolvidas dinamicamente a partir do nome (código da permission).
- Fallback para o provider padrão nas policies não relacionadas a permission.

**Detalhes técnicos:**
- `PermissionPolicyProvider : DefaultAuthorizationPolicyProvider` que cria `AuthorizationPolicy` com `PermissionRequirement` sob demanda.
- Atributo auxiliar `HasPermissionAttribute : AuthorizeAttribute` para ergonomia.

**Dependências:** USR-030.

**Critérios de aceite:**
- **Given** um endpoint decorado com `[HasPermission("users.create")]`, **When** acessado, **Then** a policy é resolvida dinamicamente e avaliada pelo handler.

**Estimativa:** Alta.

---

## USR-032 — Autorização por Role, atributos e integração no pipeline

**Título:** Integrar roles e permissions ao pipeline de autorização.

**Objetivo:** Disponibilizar `[Authorize(Roles=...)]` e `[HasPermission(...)]` de forma consistente e registrar os serviços de autorização.

**Regras:**
- Roles nativas: Administrador, Gestor de Design, Designer, Solicitante.
- Autorização é aplicada nas APIs e refletida na UI (Blazor).

**Detalhes técnicos:**
- Registrar `AddAuthorization`, `IAuthorizationHandler`, `IAuthorizationPolicyProvider` na DI.
- Definir constantes de roles/permissions compartilhadas entre API e Blazor.

**Dependências:** USR-028, USR-031.

**Critérios de aceite:**
- **Given** um usuário com role `Designer`, **When** acessa endpoint restrito a `Administrador`, **Then** recebe 403.
- **Given** um usuário `Administrador`, **When** acessa endpoints administrativos, **Then** o acesso é concedido.

**Estimativa:** Média.

---

# 7. APIs (Endpoints REST)

## USR-033 — Endpoints de autenticação (AuthController)

**Título:** Expor `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`.

**Objetivo:** Disponibilizar os fluxos de autenticação via API, orquestrando os casos de uso correspondentes.

**Regras:**
- `login` e `refresh` são anônimos; `logout` exige autenticação.
- Capturar IP de origem para os refresh tokens.
- Nunca retornar detalhes sensíveis em erros de autenticação.

**Detalhes técnicos:**
- Controllers finos: apenas recebem o request, chamam o mediador e retornam o resultado.
- DTOs de request/response dedicados; `refresh` pode usar cookie httpOnly ou corpo (definir estratégia).

**Dependências:** USR-016, USR-017, USR-018, USR-028.

**Critérios de aceite:**
- **Given** credenciais válidas, **When** `POST /auth/login`, **Then** retorna 200 com access + refresh token.
- **Given** um refresh válido, **When** `POST /auth/refresh`, **Then** retorna novos tokens.
- **Given** um usuário autenticado, **When** `POST /auth/logout`, **Then** o refresh é revogado (204/200).

**Estimativa:** Média.

---

## USR-034 — Endpoints de usuários (UsersController)

**Título:** Expor CRUD de usuários + status + senha + listagem paginada.

**Objetivo:** Disponibilizar a gestão de usuários protegida por permissions.

**Regras:**
- Cada rota exige a permission correspondente (`users.create`, `users.update`, `users.delete`, `users.view`, `users.reset-password`, `users.manage`).
- Respostas nunca incluem `PasswordHash`.

**Detalhes técnicos:**
- `POST /users`, `PUT /users/{id}`, `DELETE /users/{id}`, `PATCH /users/{id}/status`, `PATCH /users/{id}/password`, `GET /users`, `GET /users/{id}`.
- Paginação por query string (`page`, `size`, `search`, `status`, `roleId`, `sort`).

**Dependências:** USR-019, USR-020, USR-021, USR-022, USR-023, USR-024, USR-032.

**Critérios de aceite:**
- **Given** um `Administrador`, **When** cria/edita/exclui usuários, **Then** as operações respondem 2xx e persistem.
- **Given** um usuário sem `users.create`, **When** `POST /users`, **Then** recebe 403.

**Estimativa:** Alta.

---

## USR-035 — Endpoints de perfis (RolesController)

**Título:** Expor consulta e gestão de roles.

**Objetivo:** Permitir listar/detalhar roles e (para Administrador) criar/editar e gerenciar suas permissions.

**Regras:**
- Gestão de roles restrita a `roles.manage`.
- Roles de sistema não podem ser excluídas.

**Detalhes técnicos:**
- `GET /roles`, `GET /roles/{id}`, `POST /roles`, `PUT /roles/{id}`, `PUT /roles/{id}/permissions`.

**Dependências:** USR-012, USR-032.

**Critérios de aceite:**
- **Given** um `Administrador`, **When** atualiza as permissions de uma role, **Then** as associações são refletidas.
- **Given** uma role de sistema, **When** solicita exclusão, **Then** a operação é bloqueada.

**Estimativa:** Média.

---

## USR-036 — Endpoints de permissions e associações

**Título:** Expor catálogo de permissions e associações role↔permission e user↔role.

**Objetivo:** Suportar as telas de configuração de acesso.

**Regras:**
- Catálogo de permissions é somente leitura (definido via seed/constantes).
- Associações exigem `permissions.manage`/`roles.manage`.

**Detalhes técnicos:**
- `GET /permissions` (agrupadas por `Group`), `PUT /users/{id}/roles`, reuso de `PUT /roles/{id}/permissions`.

**Dependências:** USR-013, USR-009, USR-032.

**Critérios de aceite:**
- **Given** o catálogo, **When** `GET /permissions`, **Then** retorna as permissions agrupadas.
- **Given** um usuário, **When** suas roles são atualizadas, **Then** o próximo token reflete as novas permissions.

**Estimativa:** Média.

---

## USR-037 — Tratamento global de erros e respostas padronizadas

**Título:** Middleware de exceções + `ProblemDetails`.

**Objetivo:** Padronizar respostas de erro (validação, negócio, autorização, inesperado) em toda a API.

**Regras:**
- Erros de validação → 400 com detalhes por campo.
- Falhas de negócio (`Result` de falha) → 400/409 conforme o caso.
- Não autorizado → 401; sem permissão → 403; não encontrado → 404.
- Erros inesperados não vazam *stack trace* em produção.

**Detalhes técnicos:**
- Middleware/`IExceptionHandler` convertendo exceções e `Result` em `ProblemDetails`.
- Correlação por `traceId` nos logs.

**Dependências:** USR-015.

**Critérios de aceite:**
- **Given** um command inválido, **When** enviado, **Then** a API responde 400 com o mapa de erros por campo.
- **Given** um erro inesperado, **When** ocorre, **Then** retorna 500 com `ProblemDetails` sem detalhes sensíveis.

**Estimativa:** Média.

---

# 8. Interface Blazor

## USR-038 — AuthenticationStateProvider customizado

**Título:** Implementar `AuthenticationStateProvider` baseado em JWT.

**Objetivo:** Refletir o estado de autenticação no Blazor a partir do access token, expondo claims (roles/permissions) aos componentes.

**Regras:**
- Estado derivado do access token válido; expiração encerra o estado autenticado.
- Token armazenado de forma adequada ao modelo Blazor escolhido (Server: sessão/protegido; WASM: storage) — nunca em local inseguro para dados sensíveis.
- Claims de `permission` disponíveis para `AuthorizeView` e checagens de UI.

**Detalhes técnicos:**
- `JwtAuthenticationStateProvider : AuthenticationStateProvider` que faz *parse* do JWT em `ClaimsPrincipal`.
- Métodos `NotifyUserAuthentication`/`NotifyUserLogout`.
- `HttpClient` com `DelegatingHandler` que injeta `Authorization: Bearer`.

**Dependências:** USR-033.

**Critérios de aceite:**
- **Given** um token válido armazenado, **When** a aplicação inicia, **Then** o usuário aparece autenticado com suas roles/permissions.
- **Given** o logout, **When** executado, **Then** o estado passa a não autenticado.

**Estimativa:** Alta.

---

## USR-039 — Fluxo de login/logout + refresh automático

**Título:** Implementar serviço de sessão no cliente com refresh transparente.

**Objetivo:** Gerenciar o ciclo de vida dos tokens no cliente, renovando o access token automaticamente ao expirar.

**Regras:**
- Ao receber 401 por expiração, tentar refresh uma vez e repetir a requisição.
- Falha no refresh → logout e redirecionamento ao login.
- Evitar múltiplas chamadas de refresh concorrentes (serialização).

**Detalhes técnicos:**
- `AuthService` com `LoginAsync`, `LogoutAsync`, `TryRefreshAsync`.
- `DelegatingHandler` de refresh; *lock* para requisições concorrentes.

**Dependências:** USR-038.

**Critérios de aceite:**
- **Given** um access token expirado e refresh válido, **When** uma chamada protegida é feita, **Then** o token é renovado e a chamada conclui com sucesso.
- **Given** o refresh inválido, **When** a renovação falha, **Then** o usuário é deslogado e enviado ao login.

**Estimativa:** Alta.

---

## USR-040 — Rotas protegidas e componentes de autorização

**Título:** Implementar proteção de rotas e componentes por role/permission.

**Objetivo:** Garantir que páginas e elementos de UI só apareçam para quem tem acesso, alinhado às policies da API.

**Regras:**
- Páginas administrativas exigem role/permission adequadas.
- Elementos (botões de criar/editar/excluir) condicionados à permission.
- A UI é apenas conveniência; a API continua sendo a fonte de verdade da autorização.

**Detalhes técnicos:**
- `CascadingAuthenticationState` + `AuthorizeRouteView` + `[Authorize]` nas páginas.
- Componente utilitário `<AuthorizePermission Permission="users.create">` para checagens finas.

**Dependências:** USR-038.

**Critérios de aceite:**
- **Given** um usuário sem permissão, **When** acessa uma rota protegida, **Then** é redirecionado/negado.
- **Given** um usuário sem `users.create`, **When** vê a lista de usuários, **Then** o botão "Novo usuário" não é exibido.

**Estimativa:** Média.

---

## USR-041 — Página de Login

**Título:** Implementar a tela de login.

**Objetivo:** Permitir a autenticação do usuário com feedback de erro e estados de carregamento.

**Regras:**
- Validar campos no cliente antes do envio.
- Mensagem de erro genérica em falha de credenciais.
- Redirecionar ao destino original após login.

**Detalhes técnicos:**
- Formulário com `EditForm` + `DataAnnotations`/validação.
- Chamar `AuthService.LoginAsync`; tratar loading e erros.

**Dependências:** USR-039.

**Critérios de aceite:**
- **Given** credenciais válidas, **When** o usuário faz login, **Then** é autenticado e redirecionado.
- **Given** credenciais inválidas, **When** o usuário faz login, **Then** vê mensagem de erro genérica sem detalhes sensíveis.

**Estimativa:** Média.

---

## USR-042 — Tela de listagem de usuários

**Título:** Implementar a listagem paginada de usuários.

**Objetivo:** Exibir usuários com busca, filtros, ordenação e paginação, consumindo `GET /users`.

**Regras:**
- Requer permission de visualização (`users.view`).
- Ações (editar/status/excluir) condicionadas às permissions.

**Detalhes técnicos:**
- Tabela com paginação servidor; filtros por nome/e-mail/status/role.
- Estados de loading/vazio/erro.

**Dependências:** USR-040, USR-034.

**Critérios de aceite:**
- **Given** usuários cadastrados, **When** a tela carrega, **Then** exibe a primeira página com total e permite navegar.
- **Given** um filtro por status, **When** aplicado, **Then** a lista reflete o filtro.

**Estimativa:** Média.

---

## USR-043 — Tela de cadastro/edição de usuário

**Título:** Implementar o formulário de criação e edição de usuário.

**Objetivo:** Permitir criar e editar usuários, atribuir roles e (na criação) definir senha inicial.

**Regras:**
- Validação de e-mail, nome e política de senha no cliente + servidor.
- Seleção de uma ou mais roles.
- Editar não altera senha (fluxo separado de reset).

**Detalhes técnicos:**
- `EditForm` reutilizável para criar/editar; carregamento de roles via API.
- Tratamento de erros de validação retornados pela API (mapear por campo).

**Dependências:** USR-042.

**Critérios de aceite:**
- **Given** dados válidos, **When** o formulário é submetido, **Then** o usuário é criado/atualizado e a lista é atualizada.
- **Given** um e-mail duplicado, **When** submetido, **Then** o erro é exibido no campo correspondente.

**Estimativa:** Média.

---

## USR-044 — Tela de perfis (Roles)

**Título:** Implementar a tela de gestão de perfis.

**Objetivo:** Listar roles e gerenciar suas permissions (para Administrador).

**Regras:**
- Requer `roles.manage` para edição.
- Roles de sistema não podem ser excluídas.

**Detalhes técnicos:**
- Lista de roles + painel de associação de permissions (agrupadas por `Group`).
- Consumir `GET /roles`, `PUT /roles/{id}/permissions`.

**Dependências:** USR-040, USR-035.

**Critérios de aceite:**
- **Given** uma role, **When** o Administrador altera suas permissions, **Then** a alteração é persistida e refletida.

**Estimativa:** Média.

---

## USR-045 — Tela de permissões e associações

**Título:** Implementar a visualização do catálogo de permissions e a associação de roles a usuários.

**Objetivo:** Exibir permissions agrupadas e permitir gerenciar as roles de cada usuário.

**Regras:**
- Catálogo é somente leitura.
- Associação user↔role exige permission adequada.

**Detalhes técnicos:**
- Consumir `GET /permissions` e `PUT /users/{id}/roles`.
- Feedback ao usuário de que mudanças de role só valem após novo token/refresh.

**Dependências:** USR-040, USR-036.

**Critérios de aceite:**
- **Given** o catálogo, **When** a tela carrega, **Then** exibe as permissions agrupadas por módulo.
- **Given** um usuário, **When** suas roles são alteradas, **Then** a associação é persistida.

**Estimativa:** Média.

---

# 9. Seeds (Dados Iniciais)

## USR-046 — Seed de Permissions

**Título:** Popular o catálogo de permissions.

**Objetivo:** Inserir todas as permissions do sistema, derivadas dos perfis descritos nos requisitos.

**Regras:**
- Seed idempotente (não duplica ao rodar novamente).
- Códigos seguem `recurso.acao`.

**Detalhes técnicos:**
- Definir constantes de permission em `Domain` e usá-las no seed.
- Exemplos de grupos/códigos:
  - Usuários: `users.view`, `users.create`, `users.update`, `users.delete`, `users.manage`, `users.reset-password`
  - Acesso: `roles.manage`, `permissions.manage`
  - Briefings: `briefings.approve`, `briefings.reject`, `briefings.request-adjustments`, `briefings.view-all`, `briefings.create`, `briefings.view-own`
  - Tarefas: `tasks.create`, `tasks.assign`, `tasks.reassign`, `tasks.reopen`, `tasks.update-status`, `tasks.comment`, `tasks.deliver`, `tasks.approve-technical`, `tasks.view-assigned`, `tasks.view-all`
  - Configuração: `sla.configure`, `categories.configure`, `custom-fields.configure`, `checklists.configure`, `audit.view`, `system.configure`
- Executado via `HasData` ou serviço de seed no startup (ambiente controlado).

**Dependências:** USR-002.

**Critérios de aceite:**
- **Given** um banco vazio, **When** o seed roda, **Then** todas as permissions são criadas sem duplicidade.
- **Given** o seed executado novamente, **When** roda de novo, **Then** nada é duplicado.

**Estimativa:** Baixa.

---

## USR-047 — Seed de Roles + RolePermissions

**Título:** Criar os perfis nativos e suas permissions.

**Objetivo:** Inserir as roles Administrador, Gestor de Design, Designer e Solicitante com o conjunto de permissions descrito nos requisitos.

**Regras:**
- Roles marcadas como `IsSystemRole = true`.
- **Administrador:** todas as permissions.
- **Gestor de Design:** aprovar/rejeitar/solicitar ajustes de briefings, criar/atribuir/reatribuir/reabrir tarefas, definir prioridade/prazo, aprovar tecnicamente, `briefings.view-all`, `tasks.view-all`.
- **Designer:** `tasks.view-assigned`, `tasks.update-status`, `tasks.comment`, `tasks.deliver`, `briefings.view-related`.
- **Solicitante:** `briefings.create`, editar/salvar rascunho/cancelar, acompanhar, aprovar/reprovar entregas, `briefings.view-own`.
- Seed idempotente.

**Detalhes técnicos:**
- Associar permissions por código; falhar de forma clara se algum código não existir.

**Dependências:** USR-046.

**Critérios de aceite:**
- **Given** as permissions existentes, **When** o seed de roles roda, **Then** as 4 roles são criadas com suas permissions corretas.
- **Given** a role Administrador, **When** inspecionada, **Then** possui todas as permissions do catálogo.

**Estimativa:** Média.

---

## USR-048 — Seed do usuário Administrador

**Título:** Criar o usuário administrador inicial.

**Objetivo:** Garantir acesso inicial ao sistema com um administrador padrão.

**Regras:**
- E-mail: `admin@empresa.com`; senha inicial: `Admin@123` (hasheada com BCrypt).
- Role: Administrador.
- Recomendação: forçar troca de senha no primeiro login (flag opcional) e documentar a alteração da senha padrão em produção.
- Seed idempotente (não recria se já existir).

**Detalhes técnicos:**
- Hashear a senha via `IPasswordHasher` no seed (não gravar texto puro).
- Associar à role Administrador (USR-047).

**Dependências:** USR-047, USR-025.

**Critérios de aceite:**
- **Given** um banco recém-provisionado, **When** o seed roda, **Then** existe o usuário `admin@empresa.com` ativo com a role Administrador.
- **Given** as credenciais padrão, **When** o admin faz login, **Then** a autenticação é bem-sucedida e ele tem acesso total.

**Estimativa:** Baixa.

---

# 10. Testes Unitários

## USR-049 — Testes de entidades e regras de domínio

**Título:** Cobrir com testes as entidades e value objects.

**Objetivo:** Garantir as invariantes de domínio (User, Role, Permission, RefreshToken, Email, soft delete/auditoria).

**Regras:**
- Cobrir caminhos felizes e violações de invariante.

**Detalhes técnicos:**
- xUnit + FluentAssertions.
- Casos: criação válida/ inválida, `ChangePassword`, `Activate/Deactivate`, `AddPermission` sem duplicar, `RefreshToken.IsActive`, `Email.Create`.

**Dependências:** USR-005, USR-006, USR-007, USR-008, USR-004.

**Critérios de aceite:**
- **Given** as regras de domínio, **When** os testes rodam, **Then** todos passam e cobrem os cenários de sucesso e falha das invariantes.

**Estimativa:** Média.

---

## USR-050 — Testes de casos de uso (handlers + validators)

**Título:** Cobrir os handlers de CQRS e validators.

**Objetivo:** Validar a orquestração dos casos de uso (login, refresh, logout, CRUD, senha, status, exclusão, consultas) com dependências mockadas.

**Regras:**
- Repositórios e serviços mockados; sem I/O real.
- Verificar regras (unicidade de e-mail, senha inválida, usuário inativo, rotação de refresh).

**Detalhes técnicos:**
- xUnit + Moq/NSubstitute.
- Casos negativos: login com senha errada/usuário inativo; refresh revogado; cadastro com e-mail duplicado.

**Dependências:** USR-016 a USR-024, USR-015.

**Critérios de aceite:**
- **Given** um handler, **When** recebe entrada válida/ inválida, **Then** produz o resultado esperado e aciona as dependências corretas.
- **Given** um validator, **When** a entrada é inválida, **Then** retorna os erros esperados.

**Estimativa:** Alta.

---

## USR-051 — Testes dos serviços de segurança

**Título:** Cobrir `BcryptPasswordHasher`, `JwtService` e `RefreshTokenService`.

**Objetivo:** Garantir hashing, emissão/validação de JWT e geração/rotação de refresh token.

**Regras:**
- Não testar segredos reais de produção; usar configuração de teste.

**Detalhes técnicos:**
- Verificar: `Hash`/`Verify` do BCrypt; claims e expiração do JWT; unicidade/entropia e rotação do refresh token.

**Dependências:** USR-025, USR-026, USR-027.

**Critérios de aceite:**
- **Given** os serviços, **When** os testes rodam, **Then** hash verifica corretamente, o JWT contém claims/expiração esperadas e a rotação revoga o token anterior.

**Estimativa:** Média.

---

# 11. Testes de Integração

## USR-052 — Integração: fluxo de autenticação

**Título:** Testar login/refresh/logout ponta a ponta.

**Objetivo:** Validar os endpoints de autenticação contra um banco real de teste, incluindo persistência e rotação de tokens.

**Regras:**
- Banco isolado por execução (Testcontainers PostgreSQL ou banco descartável).
- Seeds aplicados no setup.

**Detalhes técnicos:**
- `WebApplicationFactory` + `Testcontainers.PostgreSql`.
- Cenários: login válido/ inválido; refresh válido/ revogado; logout revoga refresh.

**Dependências:** USR-033, USR-046, USR-047, USR-048.

**Critérios de aceite:**
- **Given** o admin semeado, **When** faz login e usa o refresh, **Then** recebe novos tokens e o anterior fica revogado.
- **Given** um refresh revogado, **When** reutilizado, **Then** a API retorna erro de autenticação.

**Estimativa:** Alta.

---

## USR-053 — Integração: autorização (roles/permissions)

**Título:** Testar policies, roles e permissions nos endpoints.

**Objetivo:** Garantir que o controle de acesso funciona fim a fim (401/403/200 conforme claims).

**Regras:**
- Cobrir cada role nativa contra endpoints representativos.

**Detalhes técnicos:**
- Gerar tokens para usuários de cada perfil; chamar endpoints protegidos.
- Cenários: sem token → 401; sem permission → 403; com permission → 200.

**Dependências:** USR-032, USR-034, USR-052.

**Critérios de aceite:**
- **Given** um `Designer`, **When** acessa um endpoint de `Administrador`, **Then** recebe 403.
- **Given** um `Administrador`, **When** acessa endpoints administrativos, **Then** recebe 200.

**Estimativa:** Alta.

---

## USR-054 — Integração: CRUD de usuários/perfis/permissões

**Título:** Testar as operações de gestão ponta a ponta.

**Objetivo:** Validar criação/edição/exclusão de usuários, gestão de roles e associações contra o banco de teste.

**Regras:**
- Verificar unicidade de e-mail, soft delete e reflexo das permissions.

**Detalhes técnicos:**
- Cenários: criar usuário → autenticar → verificar permissions; editar roles → refletir no novo token; soft delete → some das listagens.

**Dependências:** USR-034, USR-035, USR-036, USR-052.

**Critérios de aceite:**
- **Given** um usuário criado com role específica, **When** autentica, **Then** o token contém as permissions esperadas.
- **Given** um usuário excluído logicamente, **When** a lista é consultada, **Then** ele não aparece.

**Estimativa:** Média.

---

## Resumo de dependências (visão macro)

```
USR-001 ─▶ USR-002 ◀── USR-003..009 (entidades)
                 │
                 ▼
USR-010 (UoW) ─▶ USR-011..014 (repos)
                 │
                 ▼
USR-015 (contratos/CQRS) ─▶ USR-016..024 (casos de uso)
                 │
USR-025 (bcrypt) ─┼─▶ USR-026 (jwt) ─▶ USR-027 (refresh) ─▶ USR-028 (pipeline)
                 │
                 ▼
USR-029 ─▶ USR-030 ─▶ USR-031 ─▶ USR-032 (autorização)
                 │
                 ▼
USR-033..037 (APIs) ─▶ USR-038..045 (Blazor)
                 │
USR-046 ─▶ USR-047 ─▶ USR-048 (seeds)
                 │
                 ▼
USR-049..051 (unitários) ─▶ USR-052..054 (integração)
```

> **Observação de segurança:** a autorização real acontece no servidor (API). As checagens no Blazor são de conveniência de UX. Alterações de roles/permissions só passam a valer para o usuário após a emissão de um novo access token (login ou refresh).
