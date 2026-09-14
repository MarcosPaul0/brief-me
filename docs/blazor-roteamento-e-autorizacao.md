# Blazor: Roteamento e Autorização — guia do `Routes.razor`

Documento introdutório para quem está começando em Blazor. Explica, do zero, cada
conceito que aparece em `src/BriefMe.Web/Components/Routes.razor` e como as peças
se conectam no BriefMe.

---

## Sumário

1. [O arquivo que estamos explicando](#1-o-arquivo-que-estamos-explicando)
2. [Fundamentos: componentes, parâmetros e render fragments](#2-fundamentos)
3. [A cadeia de inicialização: `Program.cs` → `App.razor` → `Routes.razor`](#3-a-cadeia-de-inicialização)
4. [`Router`](#4-router)
5. [`Found` e `RouteData`](#5-found-e-routedata)
6. [`NotFoundPage`](#6-notfoundpage)
7. [Layouts e `AuthorizeRouteView`](#7-layouts-e-authorizerouteview)
8. [Estado de autenticação em Blazor](#8-estado-de-autenticação-em-blazor)
9. [`AuthorizeView`](#9-authorizeview)
10. [O atributo `Context`](#10-o-atributo-context)
11. [`FocusOnNavigate`](#11-focusonnavigate)
12. [`AuthStateInitializer` (componente do BriefMe)](#12-authstateinitializer)
13. [Render modes e prerender](#13-render-modes-e-prerender)
14. [Fluxo completo, passo a passo](#14-fluxo-completo)
15. [Glossário rápido](#15-glossário-rápido)

---

## 1. O arquivo que estamos explicando

`src/BriefMe.Web/Components/Routes.razor`:

```razor
<AuthStateInitializer>
    <Router AppAssembly="typeof(Program).Assembly" NotFoundPage="typeof(Pages.NotFound)">
        <Found Context="routeData">
            <AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)">
                <NotAuthorized>
                    <AuthorizeView Context="innerAuthContext">
                        <Authorized>
                            <AccessDenied />
                        </Authorized>
                        <NotAuthorized>
                            <RedirectToLogin />
                        </NotAuthorized>
                    </AuthorizeView>
                </NotAuthorized>
                <Authorizing>
                    <AuthorizingPlaceholder />
                </Authorizing>
            </AuthorizeRouteView>
            <FocusOnNavigate RouteData="routeData" Selector="h1" />
        </Found>
    </Router>
</AuthStateInitializer>
```

São 22 linhas que definem **todo o comportamento de navegação e controle de acesso
do app**. Vamos destrinchar.

---

## 2. Fundamentos

Antes dos componentes de rota, três conceitos precisam estar claros.

### 2.1 Componente

Um arquivo `.razor` é uma classe C# gerada pelo compilador. `<MainLayout />` no
markup vira uma instanciação dessa classe. Componentes são a unidade básica de
tudo em Blazor — páginas, layouts, botões, o roteador. Não existe distinção
técnica entre "página" e "componente": uma página é apenas um componente que
declarou `@page "/rota"`.

### 2.2 Parâmetro (`[Parameter]`)

Atributos no markup viram propriedades marcadas com `[Parameter]`:

```razor
<Router AppAssembly="typeof(Program).Assembly" />
```

corresponde a:

```csharp
[Parameter] public Assembly AppAssembly { get; set; }
```

Note o `@` ausente e o `typeof(...)`: em atributos de componente, o valor é
**expressão C#**, não string. `AppAssembly="foo"` passaria a string `"foo"`;
`AppAssembly="typeof(Program).Assembly"` passa um objeto `Assembly`.

### 2.3 `RenderFragment` e `RenderFragment<T>`

Esses são os tipos que permitem passar **markup** como parâmetro.

```csharp
RenderFragment      // um bloco de markup, sem entrada
RenderFragment<T>   // um bloco de markup que RECEBE um valor T
```

Quando um componente declara:

```csharp
[Parameter] public RenderFragment? Authorizing { get; set; }
```

você pode escrever `<Authorizing>...</Authorizing>` dentro da tag dele. O
conteúdo vira uma lambda que o componente decide **se** e **quando** renderizar.

Quando o tipo é genérico:

```csharp
[Parameter] public RenderFragment<RouteData>? Found { get; set; }
```

o compilador Razor gera uma lambda **com parâmetro**:

```csharp
Found = (context) => builder => { /* seu markup */ };
```

Esse parâmetro implícito se chama `context` por padrão. Guarde isso — é a chave
da seção 10.

> **Ponto mental importante:** o conteúdo dentro de `<Found>` **não é executado
> imediatamente**. É uma receita entregue ao `Router`, que a executa quando (e se)
> encontrar uma rota. Um `RenderFragment` é código diferido, não markup pronto.

### 2.4 `ChildContent` — o nome mágico

Se um componente tem um parâmetro chamado exatamente `ChildContent`, o markup
solto dentro da tag é atribuído a ele automaticamente. É o caso do
`AuthStateInitializer` do BriefMe:

```csharp
[Parameter] public RenderFragment? ChildContent { get; set; }
```

Por isso `<AuthStateInitializer><Router .../></AuthStateInitializer>` funciona sem
você precisar escrever `<ChildContent>` explicitamente.

---

## 3. A cadeia de inicialização

Três arquivos em sequência. Entender a ordem elimina 80% da confusão inicial.

### 3.1 `Program.cs` — o ponto de entrada

```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// ...
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
```

`MapRazorComponents<App>()` diz: "toda requisição que não bater em outra rota
(controller, arquivo estático) deve renderizar o componente raiz `App`".

### 3.2 `App.razor` — o documento HTML

```razor
<!DOCTYPE html>
<html lang="en">
<head>
    <base href="/" />
    ...
    <HeadOutlet />
</head>
<body>
    <Routes />
    <ReconnectModal />
    <script src="@Assets["_framework/blazor.web.js"]"></script>
</body>
</html>
```

Diferente de frameworks JS, no Blazor o `<html>` inteiro é um componente. Aqui
aparecem alguns elementos que valem nota:

| Elemento | Função |
|---|---|
| `<base href="/" />` | **Obrigatório.** O `NavigationManager` usa isso para calcular caminhos relativos. Sem ele, o roteamento quebra. |
| `<HeadOutlet />` | Ponto de injeção para `<PageTitle>` e `<HeadContent>` declarados nas páginas. É por isso que `<PageTitle>Usuários</PageTitle>` dentro de `Users.razor` consegue alterar o `<title>` do documento. |
| `@Assets["..."]` | Resolve o caminho com *fingerprint* (hash de cache-busting) gerado pelo `MapStaticAssets()`. |
| `<ImportMap />` | Emite o import map de módulos JS. |
| `blazor.web.js` | O script que estabelece o **circuito** WebSocket com o servidor. Sem ele, a página é HTML estático. |

### 3.3 `Routes.razor` — o roteamento

Chamado pelo `App.razor`. É o assunto deste documento.

---

## 4. `Router`

```razor
<Router AppAssembly="typeof(Program).Assembly" NotFoundPage="typeof(Pages.NotFound)">
```

O `Router` é o componente que **traduz URL em componente**.

### Como ele monta a tabela de rotas

Na inicialização, ele varre o assembly indicado por `AppAssembly` (mais os de
`AdditionalAssemblies`, se houver) procurando **todo tipo com o atributo
`[Route]`**. E de onde vem esse atributo? Da diretiva `@page`:

```razor
@page "/usuarios"
```

O compilador Razor traduz isso em:

```csharp
[Route("/usuarios")]
public partial class Users : ComponentBase { }
```

Ou seja: `@page` é açúcar sintático. `AppAssembly="typeof(Program).Assembly"`
significa "procure rotas no assembly onde a classe `Program` vive" — no caso,
`BriefMe.Web`. Uma página pode ter várias diretivas `@page`, gerando vários
`[Route]` para o mesmo componente.

### Como ele reage à navegação

O `Router` assina o evento `LocationChanged` do `NavigationManager`. Também
intercepta cliques em `<a href>` internos, evitando o *full page reload* — a
navegação vira troca de componente no cliente, não nova requisição HTTP.

### Parâmetros de rota

Rotas podem capturar segmentos da URL:

```razor
@page "/usuarios/{Id:guid}/editar"

@code {
    [Parameter] public Guid Id { get; set; }
}
```

O `Router` faz o parse, valida a *route constraint* (`:guid`, `:int`, `:bool`…)
e injeta no parâmetro de mesmo nome. Se o valor não casar com a constraint, a
rota não bate — e cai no caso "não encontrado".

### Os dois desfechos

O `Router` só faz uma coisa: decidir se a URL bateu ou não, e delegar o resto.

| Situação | O que o Router renderiza |
|---|---|
| URL bateu com um `@page` | O `RenderFragment<RouteData>` chamado `Found` |
| Não bateu | A página indicada em `NotFoundPage` |

Ele **não** renderiza a página diretamente nem sabe nada sobre layout ou
autorização. Isso é responsabilidade de quem você colocou dentro do `Found`.

---

## 5. `Found` e `RouteData`

```razor
<Found Context="routeData">
```

`Found` é `RenderFragment<RouteData>`. Quando a URL casa, o `Router` cria um
objeto `RouteData` e invoca esse fragment passando-o.

`RouteData` carrega duas informações:

```csharp
public sealed class RouteData
{
    public Type PageType { get; }                              // ex.: typeof(Users)
    public IReadOnlyDictionary<string, object?> RouteValues { get; }  // ex.: { "Id": <guid> }
}
```

É o "pacote de resultado" da resolução de rota. Você o repassa adiante — no
BriefMe, para o `AuthorizeRouteView` (linha 4) e para o `FocusOnNavigate`
(linha 19). Ambos precisam saber qual página foi resolvida.

---

## 6. `NotFoundPage`

```razor
NotFoundPage="typeof(Pages.NotFound)"
```

API introduzida no .NET 9/10, mais limpa que o antigo `<NotFound>` como
`RenderFragment`. Aponta para um componente de página:

```razor
@page "/not-found"
@layout MainLayout

<h3>Not Found</h3>
<p>Sorry, the content you are looking for does not exist.</p>
```

Vantagem sobre o fragment antigo: por ser uma página real, ela tem `@layout`
próprio e URL própria — e pode ser reaproveitada pelo pipeline HTTP. É
exatamente o que o `Program.cs` faz:

```csharp
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
```

Assim, um 404 vindo do lado servidor (ex.: um controller) e um 404 vindo do
roteamento Blazor mostram a mesma tela.

---

## 7. Layouts e `AuthorizeRouteView`

### 7.1 O que é um layout

Um layout é um componente que herda `LayoutComponentBase` e renderiza `@Body`
onde o conteúdo da página deve entrar:

```razor
@inherits LayoutComponentBase

<div class="page">
    <div class="sidebar"><NavMenu /></div>
    <main>
        <article class="content px-4">
            @Body   <!-- a página entra aqui -->
        </article>
    </main>
</div>
```

Uma página escolhe o layout com `@layout OutroLayout`. Se não escolher, usa o
`DefaultLayout` configurado no `Routes.razor`. No BriefMe existem `MainLayout`
(com sidebar) e `AuthLayout` (tela de login, sem sidebar).

### 7.2 `RouteView` — o componente base

`RouteView` faz duas coisas: pega o `RouteData`, instancia `PageType` passando
os `RouteValues` como parâmetros, e embrulha tudo no layout.

### 7.3 `AuthorizeRouteView` — `RouteView` + autorização

```razor
<AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)">
```

Herda de `RouteView` e adiciona uma checagem **antes** de renderizar a página.

**Parâmetros:**

| Parâmetro | Tipo | Função |
|---|---|---|
| `RouteData` | `RouteData` | A página a renderizar. |
| `DefaultLayout` | `Type` | Layout para páginas sem `@layout`. |
| `Authorizing` | `RenderFragment` | Enquanto o estado de auth ainda não resolveu. |
| `NotAuthorized` | `RenderFragment<AuthenticationState>` | Quando a autorização falha. |

**Algoritmo:**

1. Inspeciona `RouteData.PageType` procurando o atributo `[Authorize]`.
2. **Sem `[Authorize]`** → renderiza a página direto. `Authorizing` e
   `NotAuthorized` nunca são usados. Página pública.
3. **Com `[Authorize]`** → aguarda a `Task<AuthenticationState>`:
   - Task pendente → renderiza `Authorizing`
   - Autorizado → renderiza a página dentro do layout
   - Negado → renderiza `NotAuthorized`

No BriefMe, `Users.razor` declara:

```razor
@attribute [Authorize(Policy = "users.view")]
```

(em uma página, `@attribute` é como se aplica um atributo à classe gerada).

### 7.4 A limitação central

`AuthorizeRouteView` reporta apenas "falhou". Não distingue **dois casos que
exigem respostas opostas**:

- **401** — usuário anônimo → deve ir para o login
- **403** — usuário logado, mas sem a policy `users.view` → deve ver "acesso negado"

Mandar um usuário já logado para a tela de login seria confuso e possivelmente
um loop. Daí a necessidade da próxima peça.

---

## 8. Estado de autenticação em Blazor

Antes do `AuthorizeView`, é preciso entender de onde vem a informação de "quem
é o usuário".

### 8.1 `AuthenticationStateProvider`

Serviço abstrato com um método central:

```csharp
public abstract Task<AuthenticationState> GetAuthenticationStateAsync();
```

`AuthenticationState` é um envelope simples em torno de um `ClaimsPrincipal`:

```csharp
public class AuthenticationState
{
    public ClaimsPrincipal User { get; }
}
```

De `User` saem `Identity?.IsAuthenticated`, `Identity?.Name`, `IsInRole("Admin")`,
`Claims`.

O BriefMe fornece uma implementação própria em `Program.cs`:

```csharp
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>();
```

Ela constrói o `ClaimsPrincipal` a partir do JWT guardado no `AuthSessionStore`.

### 8.2 Cascading value

```csharp
builder.Services.AddCascadingAuthenticationState();
```

Isso publica a `Task<AuthenticationState>` como **cascading value** — um valor
que desce automaticamente por toda a árvore de componentes, sem precisar ser
passado parâmetro a parâmetro. É assim que `AuthorizeRouteView` e `AuthorizeView`
acessam o estado sem ninguém tê-lo passado explicitamente no `Routes.razor`.

Note que o tipo é `Task<AuthenticationState>`, não `AuthenticationState`. Isso é
deliberado: permite que a UI renderize o `Authorizing` enquanto a task ainda não
completou.

---

## 9. `AuthorizeView`

Componente de propósito geral para renderização condicional por autorização.
Funciona em qualquer lugar — página, layout, menu lateral. Não tem relação com
rotas.

**Parâmetros:**

| Parâmetro | Tipo | Função |
|---|---|---|
| `Authorized` | `RenderFragment<AuthenticationState>` | Se autorizado. |
| `NotAuthorized` | `RenderFragment<AuthenticationState>` | Se não autorizado. |
| `Authorizing` | `RenderFragment` | Enquanto avalia. |
| `ChildContent` | `RenderFragment<AuthenticationState>` | Atalho para `Authorized`. |
| `Roles` | `string` | Lista separada por vírgula; passa com **qualquer uma**. |
| `Policy` | `string` | Nome de uma policy registrada. |
| `Resource` | `object?` | Objeto para policies baseadas em recurso. |

### A regra que faz o `Routes.razor` funcionar

**Sem `Roles` e sem `Policy`, o critério é apenas: o usuário está autenticado?**

```csharp
user.Identity?.IsAuthenticated == true
```

E é exatamente por isso que ele resolve a limitação da seção 7.4:

```razor
<NotAuthorized>                          <!-- rota negou: 401 ou 403? -->
    <AuthorizeView Context="innerAuthContext">
        <Authorized>                     <!-- está logado -> era 403 -->
            <AccessDenied />
        </Authorized>
        <NotAuthorized>                  <!-- anônimo -> era 401 -->
            <RedirectToLogin />
        </NotAuthorized>
    </AuthorizeView>
</NotAuthorized>
```

O `AuthorizeRouteView` diz *que* falhou; o `AuthorizeView` interno diz *por quê*.

### Uso comum fora de rotas

O caso mais frequente é esconder itens de menu ou botões:

```razor
<AuthorizeView Policy="users.create">
    <a href="/usuarios/novo">Novo usuário</a>
</AuthorizeView>
```

> **Aviso de segurança:** `AuthorizeView` controla apenas **o que é exibido**.
> Não é controle de acesso. Um usuário pode navegar direto pela URL ou chamar a
> API diretamente. A autorização real precisa estar na API/Application layer;
> a UI apenas reflete o que já é imposto no servidor.

---

## 10. O atributo `Context`

Agora a pergunta que originou este documento.

`Context` **não é uma propriedade do componente**. É uma diretiva do compilador
Razor que **renomeia a variável implícita** de um `RenderFragment<T>`.

Sem `Context`, a variável se chama `context`:

```razor
<AuthorizeView>
    <Authorized>
        Olá, @context.User.Identity?.Name
    </Authorized>
</AuthorizeView>
```

Com `Context="authState"`, ela passa a se chamar `authState`:

```razor
<AuthorizeView Context="authState">
    <Authorized>
        Olá, @authState.User.Identity?.Name
    </Authorized>
</AuthorizeView>
```

### Por que é obrigatório aqui

No `Routes.razor` há **aninhamento**:

```razor
<NotAuthorized>              <!-- RenderFragment<AuthenticationState> -> declara "context" -->
    <AuthorizeView>          <!-- tentaria declarar "context" de novo -->
```

Duas variáveis `context` no mesmo escopo léxico C# → erro de compilação. Renomear
a interna resolve. Pela mesma razão a linha 3 usa `Context="routeData"`: `Found`
é `RenderFragment<RouteData>` e o nome descritivo evita colisão e melhora a
leitura.

### O que é `innerAuthContext`, então

- **Tipo:** `AuthenticationState`
- **Conteúdo:** `.User` (um `ClaimsPrincipal`)
- **Nome:** arbitrário — escolha de quem escreveu; poderia ser `authState` ou `x`
- **Uso no arquivo:** **nenhum**. Foi declarado só para evitar a colisão de
  nomes. `AccessDenied` e `RedirectToLogin` não recebem nada dele.

### Sintaxe alternativa

`Context` na tag do componente vale para todos os `RenderFragment<T>` dele.
Também pode ser declarado por fragment individual:

```razor
<AuthorizeView>
    <Authorized Context="authState">Olá @authState.User.Identity?.Name</Authorized>
    <NotAuthorized Context="anonState">Faça login.</NotAuthorized>
</AuthorizeView>
```

O mesmo mecanismo aparece em `<Virtualize Context="item">` e em qualquer
componente seu que declare `RenderFragment<T>`.

---

## 11. `FocusOnNavigate`

```razor
<FocusOnNavigate RouteData="routeData" Selector="h1" />
```

Componente de **acessibilidade**. Após cada navegação, move o foco do teclado
para o primeiro elemento que casa com o seletor CSS.

O motivo: numa SPA a URL muda mas a página não recarrega, então leitores de tela
não anunciam nada e o foco continua onde estava (por exemplo, no link clicado no
menu). Focar o `<h1>` faz o leitor de tela anunciar o título da nova página.

Recebe `RouteData` justamente para saber quando refocar — a cada mudança de rota.
Fica **fora** do `AuthorizeRouteView`, como irmão dele: ambos são filhos do
`Found`, que aceita múltiplos elementos no mesmo fragment.

---

## 12. `AuthStateInitializer`

Componente próprio do BriefMe, não do framework
(`Components/Auth/AuthStateInitializer.razor`):

```razor
@inject AuthSessionStore SessionStore

@ChildContent

@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await SessionStore.RestoreAsync();
    }
}
```

Ele envolve **todo** o roteador. Sua única função: no primeiro render
interativo, restaurar a sessão (o JWT) do `ProtectedSessionStorage` do browser
para dentro do circuito do servidor.

**Por que precisa existir:** o Blazor Server mantém estado no servidor, mas ao
recarregar a página (F5) esse estado se perde — o circuito é novo. O token vive
no browser, e o servidor só consegue lê-lo via JS interop, que **só funciona
depois** que o componente renderizou de forma interativa. Daí `OnAfterRenderAsync`
e não `OnInitializedAsync`.

Consequência prática: durante o prerender, e no instante inicial do circuito, o
app **ainda não sabe quem é o usuário**. É por isso que o `<Authorizing>` importa
tanto, e por isso o `RedirectToLogin` tem essa guarda:

```csharp
if (RendererInfo.IsInteractive)
{
    await SessionStore.RestorationCompleted;
    if (SessionStore.AccessToken is not null)
        return;   // sessão apareceu; não navegue para o login
}
```

Sem essa espera, um F5 numa página protegida chutaria o usuário logado para o
login — porque no milissegundo inicial ninguém ainda leu o token do storage.

---

## 13. Render modes e prerender

Conceito do .NET 8+ que explica boa parte das esquisitices acima.

### Os modos

| Modo | Onde roda | Interatividade |
|---|---|---|
| *(nenhum)* — SSR estático | Servidor, uma vez | Nenhuma. HTML puro. |
| `InteractiveServer` | Servidor, via WebSocket | Sim, sobre o circuito |
| `InteractiveWebAssembly` | Browser, via .NET WASM | Sim, no cliente |
| `InteractiveAuto` | WASM se em cache, senão Server | Sim |

O BriefMe usa `InteractiveServer`, declarado por página:

```razor
@rendermode InteractiveServer
```

### O ciclo de dois passes

Por padrão, componentes interativos passam por **prerender**:

1. **Passe 1 (prerender, SSR):** o servidor renderiza HTML e envia. Rápido, bom
   para SEO. Mas **não há JS interop, não há `OnAfterRenderAsync`**, e o
   `localStorage`/`sessionStorage` é inacessível.
2. **Passe 2 (interativo):** o `blazor.web.js` conecta o WebSocket, o circuito
   inicia e os componentes renderizam de novo — agora com interop disponível.

Isso significa que **`OnInitializedAsync` roda duas vezes** e que qualquer coisa
que dependa do browser precisa esperar o passe 2. É exatamente a razão de
existirem o `AuthStateInitializer`, o `AuthorizingPlaceholder` e a checagem
`RendererInfo.IsInteractive` no `RedirectToLogin`.

Sem `<Authorizing>`, o usuário veria um flash de "acesso negado" no passe 1,
antes de a sessão ser restaurada no passe 2.

### Circuito

No Blazor Server, "circuito" é a conexão WebSocket + o estado dos componentes
mantido no servidor para aquele usuário. Se cair, o `ReconnectModal` (visível no
`App.razor`) tenta reconectar. Serviços `Scoped` vivem pelo tempo do circuito —
por isso `AuthSessionStore` é `Scoped` e sobrevive à navegação entre páginas,
mas não a um F5.

---

## 14. Fluxo completo

Usuário digita `/usuarios` na barra de endereço. O que acontece:

```
1.  Requisição HTTP -> MapRazorComponents<App>() -> renderiza App.razor

2.  App.razor renderiza <Routes />

3.  AuthStateInitializer renderiza seu ChildContent (o Router)
    (RestoreAsync ainda NÃO rodou — estamos no prerender)

4.  Router compara "/usuarios" com a tabela de rotas
    -> casa com Users.razor (@page "/usuarios")
    -> cria RouteData { PageType = typeof(Users), RouteValues = {} }
    -> invoca o fragment Found com esse RouteData (nome local: routeData)

5.  AuthorizeRouteView recebe routeData
    -> encontra [Authorize(Policy = "users.view")] em Users
    -> consulta a Task<AuthenticationState> cascateada

6a. Task ainda pendente
    -> renderiza <AuthorizingPlaceholder />
    -> circuito conecta, AuthStateInitializer.OnAfterRenderAsync roda,
       RestoreAsync() lê o JWT, o provider notifica mudança de estado,
       a árvore re-renderiza -> volta ao passo 5

6b. Autorizado (logado E com a policy users.view)
    -> instancia Users dentro de MainLayout -> página exibida

6c. Negado -> renderiza o fragment NotAuthorized:
    AuthorizeView (sem Roles/Policy) pergunta: está autenticado?
      - Sim  -> <AccessDenied />     (403: logado, falta permissão)
      - Não  -> <RedirectToLogin />  (401: anônimo -> vai para /login)

7.  FocusOnNavigate move o foco para o <h1> da nova página
```

E se a URL não existir (`/rota-inexistente`), o passo 4 falha e o `Router`
renderiza `NotFoundPage` — `Pages.NotFound`, com seu próprio `@layout MainLayout`.

---

## 15. Glossário rápido

| Termo | Significado |
|---|---|
| **Componente** | Classe gerada de um `.razor`. Unidade básica de UI. |
| **Página** | Componente com `@page`. Vira `[Route]` na compilação. |
| **Layout** | Componente com `@inherits LayoutComponentBase` que renderiza `@Body`. |
| **`RenderFragment`** | Markup como parâmetro; execução diferida. |
| **`RenderFragment<T>`** | Idem, mas recebe um valor — expõe a variável `context`. |
| **`Context="x"`** | Renomeia essa variável implícita. Evita colisão em aninhamentos. |
| **`RouteData`** | Resultado da resolução de rota: `PageType` + `RouteValues`. |
| **`NavigationManager`** | Serviço de URL atual, navegação e evento `LocationChanged`. |
| **Cascading value** | Valor que desce automaticamente pela árvore de componentes. |
| **`AuthenticationState`** | Envelope de `ClaimsPrincipal`; o "quem é o usuário". |
| **`AuthenticationStateProvider`** | Serviço que produz esse estado. Aqui: `JwtAuthenticationStateProvider`. |
| **Circuito** | Conexão WebSocket + estado de componentes no Blazor Server. |
| **Prerender** | Primeiro passe SSR, sem interatividade nem JS interop. |
| **Render mode** | Como o componente roda: estático, Server, WASM ou Auto. |

---

## Onde ler mais

- [Blazor routing and navigation](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/routing)
- [Blazor render modes](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes)
- [ASP.NET Core Blazor authentication and authorization](https://learn.microsoft.com/aspnet/core/blazor/security/)
- [Render fragments and templated components](https://learn.microsoft.com/aspnet/core/blazor/components/templated-components)
