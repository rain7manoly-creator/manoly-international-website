# Manoly Warehouse

A production warehouse management system for a real-world tile & fixtures distributor. Built with ASP.NET Core 8 on the back of a clean-architecture domain model, persisted in PostgreSQL via EF Core, and deployed on Fly.io.


---

## What it does

The warehouse has 414 physical shelf positions across two sides (A/B/C and D/E/F racks), plus an overflow zone ("Area Z") for stock that arrives before a shelf is available. The app models that layout exactly and gives warehouse staff a single tool for:

- Receiving purchase orders and assigning each item to a specific shelf position or Area Z
- Moving stock from Area Z onto a shelf when space frees up
- Fast search over products, categories, and shelf codes
- Full audit log of every quantity adjustment (who / when / why)
- Role-based access — administrators manage users, catalog, and shelf layout; staff record inventory movements
- PDF exports of full inventory or per-shelf breakdowns for stock takes
- Arabic-first, RTL-native UI

## Stack

| Layer | Choice |
| ----- | ------ |
| Runtime | ASP.NET Core 8 (Razor MVC) |
| Database | PostgreSQL 16 (Neon in prod) |
| ORM | EF Core 8 with Npgsql provider |
| Auth | ASP.NET Identity, cookie-based |
| Frontend | Server-rendered Razor + Tailwind CSS |
| PDF export | QuestPDF |
| Logging | Serilog (structured, console sink) |
| Hosting | Fly.io (Dubai region), Docker multi-stage build |
| CI | Fly's remote builder — `fly deploy` from any branch |

## Architecture

Clean architecture split into four projects worth of concerns, in a single deployable:

```
Domain/          Entities with private setters, factory methods, invariants
                 enforced in the aggregate (Shelf.IsFull, AreaZ.Dispatch, etc.)

Application/    Service interfaces + implementations. All business rules
                 live here — controllers only orchestrate.

Infrastructure/ EF Core DbContext, entity configurations, migrations, seeding.

Controllers/    Thin — bind, call a service, return a view.
Views/          Razor views + shared layout, Tailwind classes.
```

A few decisions worth calling out:

- **User-initiated transactions inside the retry strategy.** `EnableRetryOnFailure` requires transactional work to run through `CreateExecutionStrategy().ExecuteAsync(...)` — otherwise EF refuses at runtime. Every service that opens a transaction (`PurchaseOrderService`, `ProductService`, `AreaZService`) wraps its work accordingly.
- **Data Protection keys persisted in the DB.** Without this, auth cookies and antiforgery tokens invalidate on every redeploy. The `DataProtectionKey` table is managed by `IDataProtectionKeyContext` on the same `DbContext`.
- **Partial unique index on Area Z.** Only one *active* Area Z row per product is allowed at a time. Enforced at the DB level with a PostgreSQL partial index (`HasFilter("\"IsDispatched\" = false")`), not just in application code.
- **Shelf capacity as a domain invariant.** `Shelf.IsFull`, `PositionOccupiedException`, and `ShelfFullException` live in the domain — services throw them, middleware translates them into user-facing Arabic messages.
- **Seeded shelf topology.** All 414 shelves are seeded via `HasData` in `OnModelCreating`, so a fresh database boots straight into a usable state.
- **Health check gates traffic.** `AddDbContextCheck<AppDbContext>()` powers `/health`, which Fly polls every 30 seconds — a wedged app or unreachable DB is taken out of rotation before it serves errors.

## Local setup

Requires .NET 8 SDK, Node 20+ (for Tailwind), and a local PostgreSQL 16.

```bash
git clone https://github.com/faresibrahim/manoly-international-website.git
cd manoly-international-website

# 1. Point the app at your local Postgres
cp appsettings.json appsettings.Development.json
# edit the DefaultConnection line, then:

# 2. Restore + build CSS + run
dotnet restore
npm install
npm run css:build
dotnet run
```

Migrations run automatically on startup, and the admin user is seeded from `Seed:AdminUserName` + the `Seed__AdminPassword` env var (or user-secrets in dev).

## Deployment

The `Dockerfile` uses three stages: Node compiles Tailwind, .NET publishes the app, and a slim `aspnet:8.0` runtime image is the final artifact. Fly.io picks it up via `fly.toml`:

```bash
fly deploy
```

Prod config lives in Fly secrets:

- `DATABASE_URL` — Neon connection string (`postgres://...`)
- `Seed__AdminPassword` — first-boot admin password

## Notable files

- [`Program.cs`](Program.cs) — pipeline, forwarded headers, migration bootstrap, health check
- [`Extensions/ServiceCollectionExtensions.cs`](Extensions/ServiceCollectionExtensions.cs) — DI wiring, Identity + auth cookie config
- [`Infrastructure/Persistence/AppDbContext.cs`](Infrastructure/Persistence/AppDbContext.cs) — model config, table renames, shelf seed
- [`Application/Services/PurchaseOrderService.cs`](Application/Services/PurchaseOrderService.cs) — the receiving workflow, retry-safe transactions
- [`Domain/Entities/Shelf.cs`](Domain/Entities/Shelf.cs) — the aggregate the whole app revolves around

## License

MIT — see [LICENSE](LICENSE).
