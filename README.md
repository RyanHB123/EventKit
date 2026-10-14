# EventKit

An event equipment lending platform for a community venue. Built with React, TypeScript, ASP.NET Core 10, Entity Framework Core and Neon PostgreSQL.

## First version

- Staff sign-in with hashed passwords, HTTP-only session cookies, CSRF protection and sign-in rate limiting.
- Overview of stock, reservations, physical loans and maintenance.
- Searchable equipment catalogue with category and availability filters.
- Multi-item bookings with collection/return dates and borrower contact details.
- Checkout, complete return and cancellation workflows.
- Inventory editing that protects existing reservations.
- Versioned database migrations, domain tests and a real PostgreSQL concurrency test.

## Run locally

Requires Node 24 LTS and the .NET 10 SDK. This workspace has a portable SDK at `.tools/dotnet`; the helper scripts use it automatically. Docker is optional for local development.

The current `.env.local` points to the **eventkit-dev** Neon branch. The separate production schema has been initialized with a unique staff account; fictional equipment and bookings were not seeded there. The file contains server-side secrets; never copy it into the frontend or commit it.

```powershell
npm install
npm install --prefix frontend
npm run db:setup
npm run dev
```

Open http://127.0.0.1:5173. In Development, use **Open demo workspace**, or sign in with `admin@eventkit.local` / `EventKit-demo-2026!`. This demo account and its fictional data are restricted to the development setup. All application data is persisted in PostgreSQL; refreshes do not reset it.

The frontend development server proxies `/api` to the C# API on port 5357. In the production Docker image, ASP.NET Core serves both the compiled frontend and API from one origin, so one Render web service hosts the whole app.

## Database and stock rules

Relational tables: `Equipment`, `Borrowers`, `Bookings`, `BookingItems` and `StaffUsers`. Foreign keys and database check constraints protect relationships and quantities.

Collection and return dates are inclusive, in the venue's Europe/London calendar. Available stock across a date range is total stock minus maintenance and the **peak simultaneous reservation quantity** in that range. Two non-overlapping bookings are not added together. Overdue physical loans remain unavailable until returned. A confirmed reservation can only be checked out on its booked dates, and checkout also checks physical stock.

Stock-affecting writes acquire a PostgreSQL transaction advisory lock before checking availability. This prevents simultaneous booking requests or stock edits from overselling. The single shared lock intentionally favours correctness and simplicity at this project's scale.

Return is currently all-or-nothing for a booking. Track damaged units by increasing the item's maintenance count after the return. New borrower accounts, partial returns and cancellation confirmation can be added as later features. Borrower contact details are shared per email address; existing contacts are reused.

EF migrations use `DATABASE_URL_UNPOOLED`; normal app traffic uses the pooled `DATABASE_URL`. To generate another migration, install the `dotnet-ef` 10.x tool and use:

```powershell
dotnet ef migrations add YourMigration --project backend/EventKit.Api
npm run db:migrate
```

In this Windows workspace, the SDK and EF tool are already installed locally. If your system `dotnet` is older, generate migrations with:

```powershell
$env:DOTNET_ROOT = (Resolve-Path '.tools/dotnet').Path
$env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
$env:ASPNETCORE_ENVIRONMENT = 'Development'
& '.\.tools\ef\dotnet-ef.exe' migrations add YourMigration --project backend/EventKit.Api
```

## Verify

```powershell
npm test
npm run build
npm run lint
```

The PostgreSQL concurrency test is skipped unless `EVENTKIT_TEST_DATABASE_URL` is set to a migrated development/test branch connection. It creates its own test equipment and borrower, races two reservations and cleans up its rows. Never point it at production. CI runs the domain tests, frontend lint/build and API publish without database credentials.

Initial verification passed all 19 backend tests, including the real PostgreSQL concurrency test, frontend lint/build and Release API publish. Browser verification covered sign-in, booking creation, checkout, return, inventory saving, search and mobile navigation. HTTP verification covered the compiled frontend and assets, authentication, CSRF protection and invalid requests. A completed fictional Community workshop booking is retained as demonstration history.

The Linux AMD64 Docker image `eventkit:local` also builds successfully. Container verification covered the compiled frontend/assets, Neon TLS connection, sign-in, CSRF protection, stock/history reads, custom `PORT` and non-root runtime user. A separate production-mode check covered default port 10000, disabled demo access, secure session cookies and sign-in behind a simulated HTTPS reverse proxy. These checks used the development database and did not migrate production or change business records.

## Run with Docker

The verified local container is named `eventkit-local`, available at http://127.0.0.1:5180. It uses the development database. Stop it with `docker stop eventkit-local`; its `--rm` setting removes the container when stopped, while the image and Neon data remain.

To rebuild and run it from PowerShell:

```powershell
docker build --tag eventkit:local .
$taskDbLine = Get-Content '.env.local' | Where-Object { $_ -match '^DATABASE_URL=' } | Select-Object -First 1
$env:DATABASE_URL = $taskDbLine.Substring($taskDbLine.IndexOf('=') + 1).Trim().Trim('"').Trim("'")
docker run --detach --rm --name eventkit-local --publish 127.0.0.1:5180:8080 --env DATABASE_URL --env ASPNETCORE_ENVIRONMENT=Development --env PORT=8080 eventkit:local
```

Use the local demo sign-in described above. The container binds to localhost only. Docker receives the database URL at runtime; credentials are excluded from the image. In this Codex session, Docker's install directory had to be added to the process's PATH so the CLI could find its credential helper; no global PATH settings were changed.

## API

All business endpoints require a staff session. Unsafe requests additionally require `X-CSRF-TOKEN`, obtained from `GET /api/auth/session`.

| Method | Endpoint | Purpose |
| --- | --- | --- |
| GET | `/api/health` | Liveness check |
| GET | `/api/auth/session` | Current staff identity and CSRF token |
| POST | `/api/auth/login` | Sign in |
| POST | `/api/auth/logout` | Sign out |
| GET | `/api/equipment?start=YYYY-MM-DD&end=YYYY-MM-DD` | Stock and date-range availability |
| POST | `/api/equipment` | Add stock |
| PUT | `/api/equipment/{id}` | Edit stock and maintenance |
| GET | `/api/bookings` | Most recent 1,000 bookings |
| POST | `/api/bookings` | Reserve equipment |
| POST | `/api/bookings/{id}/checkout` | Record collection |
| POST | `/api/bookings/{id}/return` | Record complete return |
| POST | `/api/bookings/{id}/cancel` | Release a confirmed reservation |

Responses use JSON and HTTP status codes: 400 for invalid input, 401 for missing sign-in, 404 for unknown resources and 409 for stock/status conflicts.
