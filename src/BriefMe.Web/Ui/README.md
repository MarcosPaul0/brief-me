# Ui — shared primitives

shadcn/ui-style building blocks for `BriefMe.Web`: owned in the repo, styled once from the
`DESIGN-sap.md` tokens (Tailwind `@theme` in `Styles/app.tailwind.css`), composed by `Pages/` and
`Components/<Feature>/`. They know tokens only — never DTOs, API clients, Application or Domain types.

Conventions shared by every primitive:

- `Class` is appended after the primitive's own classes (`ClassNames.Join`, the `cn()` equivalent). There is no
  tailwind-merge: to override a property the primitive already sets, mark it important (`text-negative!`).
- Unmatched attributes (`id`, `aria-*`, `rows`, `disabled`, `type`, ...) pass through to the root element.
- Variants are enums mapped by a `switch` inside the component (the `cva` equivalent).

## Actions

| Component | Parameters | Notes |
| --- | --- | --- |
| `Button` | `Variant` (`Default`, `Secondary`, `Outline`, `Ghost`, `Link`, `Destructive`), `Size` (`Default` 44px, `Sm`), `Type` (`button`), `Disabled`, `Loading`, `Href`, `OnClick` | `Loading` disables and shows a `Spinner`. `Href` renders an `<a>`. |
| `Spinner` | `Class` | Inherits the text colour. |

```razor
<Button OnClick="SaveAsync" Loading="_isSaving">@(_isSaving ? "Salvando..." : "Salvar")</Button>
<Button Variant="ButtonVariant.Link" Size="ButtonSize.Sm" OnClick="() => Open(item.Id)">Abrir</Button>
```

## Fields

Two families, like shadcn's `Input` vs `Form*`:

| Component | Use | Binding |
| --- | --- | --- |
| `Input`, `Textarea`, `NativeSelect` | outside an `EditForm` (filters, inline forms) | `@bind-Value` on `string?`, fires on change |
| `FormInput`, `FormTextarea`, `FormSelect<T>`, `FormDateInput<T>` | inside an `EditForm` | Blazor's own `InputText`/`InputTextArea`/`InputSelect`/`InputDate`, restyled |
| `FormMessage<T>` | validation message under a `Form*` control | `For="() => _model.Field"` |
| `FormField` | label + control + helper text | `Label`, `For` (control id), `Description` |
| `Label` | standalone label | `For` |

```razor
<FormField Label="E-mail" For="email">
    <FormInput id="email" @bind-Value="_model.Email" type="email" />
    <FormMessage For="() => _model.Email" />
</FormField>

<FormField Label="Status" For="status">
    <NativeSelect id="status" @bind-Value="_statusFilter">
        <option value="">Todos</option>
    </NativeSelect>
</FormField>
```

## Surfaces and feedback

| Component | Parameters | Notes |
| --- | --- | --- |
| `Card`, `CardTitle`, `CardDescription` | `Class` | Flat canvas surface, 12px corner, band rule. Padding is the caller's (`Class="p-6"`). |
| `Badge` | `Variant` (`Neutral`, `Info`, `Success`, `Warning`, `Danger`) | Features map their states to a variant, e.g. `BriefingLabels.StatusBadgeVariant`. |
| `Alert` | `Variant` (`Danger` default, `Warning`, `Info`, `Success`), `Title` | `role="alert"` for Danger/Warning, `status` otherwise. |
| `Dialog` | `Open`, `OnClose`, `Title`, `Description`, `Dismissible`, `ChildContent`, `Footer` | Not rendered when closed. Backdrop/Esc call `OnClose` when dismissible. |
| `LoadingState` | `Text` | Centred spinner + text for a loading region. |
| `EmptyState` | `Title`, `Description`, `Tone` (`Default`, `Danger`), `ChildContent` (action) | Empty, not found or failed load. |

```razor
<Dialog Open="_target is not null" OnClose="Dismiss" Dismissible="!_busy" Title="Cancelar briefing"
        Description="Esta ação não pode ser desfeita.">
    <Footer>
        <Button Variant="ButtonVariant.Secondary" OnClick="Dismiss">Voltar</Button>
        <Button Variant="ButtonVariant.Destructive" OnClick="ConfirmAsync" Loading="_busy">Confirmar</Button>
    </Footer>
</Dialog>
```

## Data

| Component | Parameters | Notes |
| --- | --- | --- |
| `Table` | `Header` (`TableHead` cells), `ChildContent` (`TableRow` rows), `Class` (on `<table>`, e.g. `min-w-[720px]`) | Scroll wrapper included. |
| `TableHead`, `TableRow` | `Class` | |
| `TableCell` | `Primary` (identifying column in ink), `Class` | |
| `Pagination` | `Page`, `TotalPages`, `TotalCount`, `ItemLabel`, `OnPageChange` (target page) | Previous/next disabled at the bounds. |

## Page shell

| Component | Parameters | Notes |
| --- | --- | --- |
| `PageContainer` | `Width` (`Sm` 560, `Md` 760, `Lg` 900, `Xl` 1100) | Canvas ground, gutters, centred column. |
| `PageHeader` | `Title`, `Description`, `Meta`, `Actions` | The page's `h1` (`FocusOnNavigate` targets it). |

```razor
<PageContainer>
    <PageHeader Title="Usuários" Description="Gerencie os usuários e seus perfis de acesso.">
        <Actions><Button OnClick="NavigateToNew">Novo usuário</Button></Actions>
    </PageHeader>
    ...
</PageContainer>
```

## Adding a primitive

One file per component in this folder, `Class` + `AdditionalAttributes` parameters, variants as an enum, styling
only from theme tokens. Add a row here in the matching section.
