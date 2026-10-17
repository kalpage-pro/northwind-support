# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
escalate them, and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.
The assignment text is in [`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md); my approach per task
is in [`APPROACH.md`](APPROACH.md).

| Task | Status |
| --- | --- |
| 1. DEFECT-117 fix and server-side filtering | Done |
| 2. Ticket escalation and assignment (backend, SQL SLA filter, frontend, tests) | Done. 2.5 (optional SLA report) not attempted |
| 3. Login and authentication | Done |
| 4. Dockerise the API (optional) | **Not attempted** (see [Docker](#docker)) |

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

### Development login credentials

```text
Email:    alex.turner@northwind-support.example
Password: Dev-Only-Passw0rd!
```

This is a **local development fixture**, not a secret. It is seeded only when the API runs in the
Development environment. Every active seeded agent has the same password.
`yuki.tanaka@northwind-support.example` is inactive and cannot sign in (useful to see the generic
401). Swagger: log in with `POST /api/auth/login`, click **Authorize** and paste the token.

---

## What was implemented and the rules as understood

**Task 1.** The ticket-list filter defect is fixed at its cause, and every list filter
(`status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `search`) is
applied in the database before counting and paging.

**Task 2.** Create applies automatic triage (priority, SLA due date, assignee) and returns the
decision with human-readable reasons. `POST /api/tickets/{id}/escalate` raises priority one level
and writes an immutable history row; `GET /api/tickets/{id}/escalations` returns the history,
newest first; `GET /api/tickets?slaStatus=` filters in SQL. The UI has one shared SLA indicator,
an SLA filter, an escalation panel with client-side validation, the history list and the triage
result after create.

**Task 3.** Agents sign in with email and password and receive a JWT. Every endpoint except login
requires a valid token. The escalation actor comes from the token.

How I read the rules (where the spec was open I decided, and wrote it here):

| Rule | As implemented |
| --- | --- |
| BR-1 Priority | Requested priority, default Medium. A category flagged `ForcesCriticalPriority` is always Critical. |
| BR-2 / BR-3 SLA window | Due = created + window (Critical 4 h, High 8 h, Medium 24 h, Low 72 h). Premium customers get half, never below 1 h. |
| BR-4 / BR-5 Assignment | Eligible = active, specialised where the category `RequiresSpecialist`, strictly below `MaxOpenTickets`. Choose the fewest open tickets (open = not Resolved/Closed). **Tie-break: lowest agent id.** Nobody eligible: `201`, unassigned, with a reason (a normal outcome, not an error). |
| BR-6 Configurable | Windows, premium multiplier, 1 h floor and the 25% at-risk threshold are in the `Sla` section of `appsettings.json`, validated at startup. The category flags are data. No category names appear in code. |
| BR-7 Escalation | One level up. Due date recomputed from the moment of escalation using the *current* priority and customer tier. The current agent is kept if still eligible (the ticket's own slot is not counted against them), otherwise reassigned per BR-5. Reason 5 to 500 characters. Critical, Resolved and Closed tickets return `409` with a message saying which. |
| BR-8 SLA status | Derived, never stored; "now" is injected (`IClock`). No due date: NotApplicable. Resolved on or before due: Met. Resolved late, or unresolved and **strictly** past due: Breached. Unresolved with 25% or less of the window left: AtRisk (window from current priority and tier). Otherwise WithinSla. |

Other assumptions: `search` is a trimmed `Contains` match on title, reference and customer name
(blank is ignored). `unassignedOnly=true` together with an `assignedAgentId` returns an empty
result on purpose (the two contradict each other).

---

## Design decisions

| Decision | Chosen | Rejected, and why |
| --- | --- | --- |
| Where the rules live | Small pure policy classes in the Domain (`PriorityPolicy`, `SlaPolicy`, `AgentSelector`) composed by `TicketTriage`, used by **both** create and escalate | Rules in controllers or handlers (duplicated, hard to test) or in SQL only (cannot be unit tested without a database) |
| SLA status | Derived on read; the SQL `slaStatus` filter mirrors `SlaPolicy.Evaluate`, and a consistency test checks they agree | A stored status column: it goes stale as time passes |
| SLA settings | `Sla` section in `appsettings.json` | `appsettings.Development.json` only: the rules must apply in every environment, Development should only override |
| Credentials | Nullable `PasswordHash` on `Agent` | A separate `Users` table: agents *are* the users and there is no registration, so it would only add a join. It pays off only with non-agent users or several credentials per person |
| Hashing | Identity `PasswordHasher` (salted PBKDF2) behind `IPasswordHasher` | A home-made hash (never) and BCrypt (an extra dependency with no benefit here) |
| Authorization | Fallback policy `RequireAuthenticatedUser`; only login is `[AllowAnonymous]`; a test fails the build if an action is unprotected | `[Authorize]` on each controller: a new endpoint could be forgotten. Swagger is middleware, not an endpoint, so it stays open |
| Escalation actor | From the token through `ICurrentUser` (Application stays free of ASP.NET) | A value in the request body: any client could claim to be someone else |
| Login failures | Unknown email, wrong password, inactive agent and missing hash all return the identical `401` and message; a dummy hash is verified for unknown emails so timing does not reveal them | Specific messages (account enumeration) |
| Token | JWT HS256, **60 minute** lifetime, no refresh token (out of scope) | A long-lived token: a stolen one would stay useful |
| Token storage | `sessionStorage` (see [Authentication](#authentication)) | `localStorage` (survives browser restarts) and an HttpOnly cookie (safer, but needs CSRF protection and more work) |

**Patterns that genuinely fit:**
- **Policy objects** (stateless domain rules): testable without a database and shared by create and escalate.
- **Repository + Unit of Work:** persistence sits behind interfaces, so handlers are tested with mocks and the architecture tests keep EF Core out of Domain/Application.
- **Result object for login:** a wrong password is an expected outcome, so it is a value, not an exception.

---

## DEFECT-117

**Root cause.** The ticket-list load effect depended only on search, page and page size, so changing
status, priority, category, customer, agent, unassigned-only or sort did not fetch until another
search change, and that later fetch then used values captured earlier.

**Fix** (`useTicketList`). The request is keyed from *all* filters plus a reload counter; only the
search text is debounced (300 ms); a cancelled flag stops a stale response overwriting a newer one.
Nothing hides the symptom (no forced reload, no in-memory filtering, no remount key).

**Verify.** `useTicketList.test.ts` asserts the exact arguments of each `getTickets` call:
dropdown changes request immediately, earlier filters survive a search, and the search-driven page
reset waits until typing settles. Manually: change *Status* and watch the request appear in the
Network tab at once; type in search and see one request after the pause that still contains the status.

## Filtering

All filters are `IQueryable` expressions in `TicketQueries`, one small method each
(`FilterByStatus`, `…Priority`, `…Category`, `…Customer`, `…AssignedAgent`, `…UnassignedOnly`,
`…BySearch`, `…BySlaStatus`), chained by `ApplyFilters` **before** `CountAsync`, sorting, `Skip` and
`Take`. The same filtered query feeds the count and the page, so `totalCount` and `totalPages`
describe the filtered set. Filters combine with AND, and nothing is filtered in C#.

The `slaStatus` filter is an OR over the priority × customer-tier combinations, compared with
captured `DateTime` boundary constants (one `now` per request, no provider-specific functions), so
it runs on SQL Server and SQLite.

**How the SQL was checked:** tests run the real queries on SQLite (`TicketQueriesFilteringTests`,
`TicketQueriesSlaFilteringTests`), and the generated SQL can be read by evaluating
`tickets.ToQueryString()` after `ApplyFilters` in `TicketQueries.GetPagedAsync`.
**Adding the next filter:** one property on `TicketQuery`, one `FilterByX` method, one line in `ApplyFilters`.

## Authentication

- **Credentials:** nullable `Agents.PasswordHash` (`nvarchar(500)`), migration `AddAgentCredentials`.
  `Agent.CanSignIn` needs an active agent *and* a hash.
- **Hashing:** Identity `PasswordHasher` (salted PBKDF2), so the same password gives a different
  hash for every agent.
- **Dev seeding:** the development seeder gives a hash to every agent that has none, using the one
  fixture constant, at startup in Development only. It is idempotent (agents that already have a
  hash are skipped) and does not use EF `HasData`, because random salts would change the model on every run.
- **Token:** `POST /api/auth/login` returns `{ token, expiresAtUtc, user }`. HS256 JWT with claims
  `sub`, `name`, `email`, `jti`, **lifetime 60 minutes** (`Jwt:LifetimeMinutes`). The signing key
  comes from `Jwt:Key` / the `Jwt__Key` environment variable (listed in `.env.example`), is validated
  at startup (at least 32 bytes), and only a clearly fake placeholder is committed for Development.
- **Protection:** a central fallback policy requires a token on everything except login; `GET
  /api/auth/me` returns the current user or `401` (also for an agent deactivated after the token was issued).
- **Frontend:** the session is kept in `sessionStorage` under `support-desk-session` and attached as
  `Authorization: Bearer` by `api/client.ts`. Any `401` from a protected call clears it and redirects
  to `/login` (the login call itself is excluded). Protected routes remember the wanted page and
  return to it after login.
- **Token storage trade-off:** `sessionStorage` is cleared when the tab closes, but like
  `localStorage` it can be read by injected scripts (XSS). An HttpOnly cookie would be safer but needs CSRF protection.
- **Production would need:** HTTPS only; the key from a secret store and rotated; short-lived tokens
  with refresh and revocation; the agent's active state checked on every request (today only
  `/auth/me` does it); rate limiting and lockout; consider an HttpOnly cookie.

## Docker

**Task 4 was not attempted.** `docker-compose.yml` runs SQL Server only (`npm run db:up`); the API
runs with `npm run api`. There is no API `Dockerfile`, `.dockerignore`, `api` compose service or
`/health` endpoint. For production I would build the API in a multi-stage image running as a
non-root user on HTTP 8080, take the connection string and `Jwt__Key` from the environment (nothing
baked in), expose an unauthenticated `/health` that checks the database, and run migrations as a
separate deployment step instead of at application start.

## Database

- `Agents.PasswordHash` (nullable, `nvarchar(500)`).
- New table `TicketEscalations`: priority, agent and due-date before/after (agent and due date
  nullable), `Reason` (500), `EscalatedBy` (100), `EscalatedAtUtc`. Foreign keys use `Restrict`, so
  history is immutable and cannot be removed by deleting a ticket or an agent.
- **Index:** composite `(TicketId, EscalatedAtUtc)`. History is always read for one ticket, newest
  first, so one index serves both the filter and the sort (its first column also covers the
  `TicketId` foreign key). EF also creates single-column indexes for the two agent foreign keys.
- Migrations: `AddTicketEscalations`, `AddAgentCredentials`.
- Reads use `AsNoTracking` and narrow DTO projections, with filters before paging and no N+1.

## Testing

```bash
npm test                              # backend, then web
dotnet test SupportDesk.sln           # 122 tests: 4 architecture + 118 unit/integration
npm run test --workspace apps/web     # 27 tests in 9 files
```

Neither suite needs SQL Server (query and integration tests use SQLite).

**Covered:** SLA windows per priority and tier including the 1 h floor; forced-Critical categories;
agent selection (fewest open, specialists, limits, inactive, ties, nobody eligible); escalation
(one level, due date from now, agent kept or changed, history row, rejections); SLA status at exact
boundaries; filtering and the SQL/C# SLA consistency; login handler, JWT service and hashing;
integration tests (`WebApplicationFactory` + SQLite): `401` without a token, `200` with one, expired
and wrongly signed tokens, escalation actor from the token, and a test that fails if any controller
action is unprotected. Frontend: the list hook's requests, SLA indicator, escalation panel (short
reason blocks the call, `409` shown and the ticket unchanged), login, session and API client.

**Mutation check (done by hand):** I deliberately broke a rule, confirmed a test failed, then reverted.

**Manual checks:** Swagger (login, `401` without a token, escalate `400` / `404` / `409`, history),
the browser (filters, SLA indicator, escalation, login, logout, expiry) and SQL queries confirming
that every agent has a different hash and that restarting does not change them.

**Deliberately not tested:** browser end-to-end flows, load/performance, Docker, a production database.

## AI usage

**Tools.** Claude (chat) to plan, break the work into stages and review my approach; an AI coding
agent in VS Code for the frontend and for C# changes I could not make myself;

**How I split the work.**
- **Backend (C#): mostly written by me.** I designed the rules, the policy classes, the endpoints,
  the migrations and the authentication myself, and I used the agent only when I got stuck or a
  change was too large to make by hand. I reviewed everything the agent changed.
- **Frontend (React/TypeScript): mostly written by the AI agent.** I described what each screen and hook
  should do, the agent built it, and I checked the behaviour in the browser and with the tests.
  I can explain how it works, but I did not write it line by line.

**Workflow that worked.** One small stage at a time (rules, then persistence and endpoints, then
the SQL filter, then the UI; auth in stages), each ending with format, build and tests, and then
my own check in Swagger, the browser and the database before I committed.

**Accepted as-is.** The generated frontend structure (hooks, API client, protected routes), after
I tested it in the browser and with the tests.

**Bug found.** Escalating a Critical ticket returned a `500` because of the wrong exception type;
the tests exposed it, and it now returns `409` with a message.

**Verification.** Automated tests, the mutation check above, manual Swagger and browser runs, and
SQL queries on the database. I reviewed each change before committing.

## Known limitations

- `Contains` search has a leading wildcard, so it cannot use an index (full-text search at scale);
  case sensitivity follows the database collation.
- Concurrent creates can assign an agent past their limit (no locking or re-check at save time).
- Ticket references come from the current maximum id, so there can be gaps (for example after a
  restart, SQL Server identity values can jump).
- A JWT is not revoked when an agent is deactivated; it works on every endpoint except `/auth/me`
  until it expires (60 minutes).
- No lockout or rate limiting, refresh tokens, MFA, roles, registration or password reset (out of
  scope). The token is in `sessionStorage` (XSS trade-off). Development uses the self-signed
  HTTPS certificate.
- No browser end-to-end tests. Task 2.5 and Task 4 were not attempted.
- `dotnet list SupportDesk.sln package --vulnerable --include-transitive` reports a transitive
  `System.Security.Cryptography.Pkcs` 9.0.11 advisory (`GHSA-846f-f764-76mx` high,
  `GHSA-2fmv-rv2c-59x7` moderate). It was already present, is not caused by this work and should
  be fixed by upgrading the package.

---

## Reference

### Quick manual check of the UI

With SQL Server, the API and the web app running, log in and open `http://localhost:5173/tickets`.

1. Change the SLA filter: the list refreshes, the request contains `slaStatus`, paging returns to page 1.
2. Create a ticket: the result shows priority and reason, due date, SLA window and assignment.
3. Escalate an eligible ticket with a valid reason: the button shows a pending state, the ticket and history update without a reload.
4. A short reason shows an error and sends no request.
5. A Critical, Resolved or Closed ticket has the escalation disabled with an explanation.
6. For the `409`: open one eligible ticket in two tabs, escalate in the first, submit the old form in the second. The message appears, the form keeps its text and the ticket summary is unchanged.

### Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

### API at a glance

All endpoints except `POST /api/auth/login` require `Authorization: Bearer <token>`.

| Endpoint | Description |
| --- | --- |
| `POST /api/auth/login` | Sign in. Returns token, expiry and user |
| `GET /api/auth/me` | The current user |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `slaStatus`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection` |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket (applies triage) |
| `POST /api/tickets/{id}/escalate` | Escalate; the body has only `reason`. Returns the ticket and the history entry |
| `GET /api/tickets/{id}/escalations` | Escalation history, newest first |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `401` missing or invalid token or failed login,
`404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

### Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| Everything returns 401 | Log in again (token expired) or use the dev credentials above |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop, Settings, General |
