# AGENTS.md — Ivan (ایوان)

Self-hosted home & family manager. Blazor Web App (Interactive Server), .NET 10, SQLite, Persian-first (fa-IR, RTL), Radzen Blazor. **AGPL-3.0.**

## Git commit & push policy

- **Always commit your work after each edit/task.**
- **NEVER push unless explicitly commanded by the user.**

## Strict Per-Project Granular Version Bumping Rule

In each iteration, update the project version (`AssemblyVersion`, `FileVersion`, and `Version` in csproj files) **ONLY for projects that have at least one changed file**. NEVER update or bump the version of untouched/unmodified projects!

### Version Format: `MajorVersion.MinorVersion.Revision.Build`

1. **MajorVersion**: Only increases when a completely new feature suite or facelift is carried out and **strictly requires user command and confirmation**.
2. **MinorVersion**: Increases when a new feature is added or refactored. For bug fixes alone, do **not** change this part.
3. **Revision**: Calculated mathematically from the Persian Solar Hijri calendar date.
   - **Always obtain today's Persian date first** via PowerShell:
     ```powershell
     [System.Globalization.PersianCalendar]$p = New-Object System.Globalization.PersianCalendar; $d = Get-Date; "$($p.GetYear($d))/$($p.GetMonth($d))/$($p.GetDayOfMonth($d))"
     ```
     (Example output: `1405/8/16` where Year $Y=1405$, Month $M=8$, Day $D=16$).
   - **Formula** (with Base Year $B = 1405$):
     $$\text{Revision} = (Y - B) \times 372 + (M - 1) \times 31 + (D - 1)$$
     Every month gets 31 slots, yielding 372 slots per year. Order is strictly preserved because $D < 31$ and $M < 12$ never overflow into the next field. Valid for years 1405 to 1580 (176 years).
     - *Example 1*: 11 Aban 1405 ($Y=1405, M=8, D=11$) $\rightarrow 0 \times 372 + 7 \times 31 + 10 = \mathbf{227}$.
     - *Example 2*: 16 Aban 1405 ($Y=1405, M=8, D=16$) $\rightarrow 0 \times 372 + 7 \times 31 + 15 = \mathbf{232}$.
4. **Build**: An always-incrementing build counter across the project, incremented by 1 on every iteration/build regardless of other version parts.
   - Starting build number baseline: **`1`**.
   - Example full version: `1.0.232.1`.

## Spec is in the docs folder

`docs/Ivan (ایوان) Home & Family Manager — Implementation Plan.md` is the authoritative plan (phases, data model, decisions). Before adding features, check the plan for the agreed approach instead of inventing one.

## Solution and commands

- Solution file is `Ivan.slnx` (XML format, not classic `.sln`). Use `dotnet build Ivan.slnx` / `dotnet test`. New projects must be added to the matching `/src/` or `/tests/` folder in `Ivan.slnx`.
- Target framework is `net10.0` — .NET 10 SDK required.
- Run tests: `dotnet test`.
- Verified state: Web, Infrastructure, Application, Domain, Unit tests (14), and Integration tests (2) all build cleanly and pass (16 total passed).
- Offline package sources configured in `nuget.config` (`LocalCache`, `CustomPackages`, `VSOffline`). Exact pinned versions (`10.0.13`) exist locally.

## Layer rules (dependencies point inward)

```
Ivan.Web ──> Ivan.Infrastructure ──> Ivan.Application ──> Ivan.Domain
    └──────────────────────────────────────────────────────┘
```

- `Ivan.Domain` — entities, enums, value objects (e.g. `JalaliDate`). No dependencies, ever.
- `Ivan.Application` — services + interfaces (`ICalendarService`). References Domain only.
- `Ivan.Infrastructure` — EF Core/SQLite, adapters, background jobs. No consumers above Web.
- `Ivan.Web` — Blazor UI + `Program.cs` entrypoint. DI registrations live there.
- `Ivan.Tests.Unit` tests Application/Domain. `Ivan.Tests.Integration` references Infrastructure/Web.

## Persian-first / RTL conventions (project rules, not framework defaults)

- Default request culture is **fa-IR**. All user-facing text goes through `IStringLocalizer` resource files — no hard-coded strings in components.
- Layouts use CSS logical properties (`margin-inline-*`, not `margin-left/right`); `dir` follows the culture.
- Always work with dates via `JalaliDate` (Domain) and `ICalendarService` (Application). Never format dates with the default Gregorian pipeline or assume `DateTime.ToString` output for Persian users. `ICalendarService.ConvertDigits` / `NormalizeDigitsToLatin` handle Persian vs Latin digits — accept either in input, display per `DigitStyle`.
- Radzen has **no Persian-calendar date pickers** — use the custom `IvanJalaliDatePicker` / `IvanJalaliDateRangePicker` in `src/Ivan.Web/Components/Common/`, not Radzen date inputs.

## Planned invariants to respect now

- **Multi-tenancy:** every tenant table gets a `HouseholdId`. Plan: EF Core global query filters + a `SaveChanges` interceptor stamps it, plus a reflection-based test asserting cross-household queries return nothing.
- **Money:** store as integer minor units + ISO currency; never `decimal`/`double`. Display option for Toman/Rial.
- **Hosting:** app runs as a Windows Service (`UseWindowsService` in `Program.cs`). No Windows-only APIs in src — Docker support comes later.
- `UserPreferencesState` (Web, scoped) holds per-user culture/calendar/digit settings.
