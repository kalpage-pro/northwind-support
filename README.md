# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

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

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## DEFECT-117 root cause

The ticket list load effect depended only on search, page and page size, so changing status,
priority, category, customer, agent, unassigned-only or sort did not fetch until another search
change. The hook now debounces only search text and keys the request from all filters plus reload
count. Regression tests cover immediate dropdown requests, preserving earlier filters during
search, and delaying the search-driven page reset until the search settles.

## Server-side filtering

The API applies one small method per filter through an `ApplyFilters` chain before counting,
sorting and paging. Each method leaves the query unchanged when its filter is not supplied, so
filters combine with AND; a future `slaStatus` filter can be added as one more method in the chain.

### Known limitations

Search uses `Contains`, which creates a leading wildcard and cannot use an index. Search
case-sensitivity follows the database collation. Authentication, notifications, caching and the
other out-of-scope assignment features remain future work.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection`. Filters are applied server-side before counting and paging. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
#"npm run db:up"
#"docker compose ps"

as a first api running web running also data base are up and docker is complete

 Fix a defect case status In Progress show no url request but search is okey 10 -15 min

http://localhost:5173/api/tickets?page=1&pageSize=20&status=InProgress&unassignedOnly=false&sortBy=createdAtUtc&sortDirection=desc
Request method
GET
Status code 200 okey

tsak 1.2 in this case now work all filters it (1h - 2.30h)

Document DEFECT-117 root cause and server-side filtering

Add the DEFECT-117 root cause, fix and regression tests, describe the
server-side filter chain and how a slaStatus filter would be added, and
note the Contains/collation limitation under known limitations.

What I Implemented
Task 1: DEFECT-117 Fix

Fixed DEFECT-117 in the ticket list feature.

Previously, filtering was happening after the total count and paging logic, which could produce incorrect totals and page results. All filters are now applied directly in the database query before counting and paging.

Supported filters:

Status
Priority
Category
Customer
Agent
Unassigned only
Search text


Task 2: Business Rules (In Progress)

Started implementing the core ticketing business rules as small, reusable domain classes. The goal is to keep the logic centralized, easy to test, and shared between ticket creation and escalation.

The following areas have been implemented:

Priority calculation
SLA calculations and status evaluation
Agent selection
Ticket escalation
Rules Implemented
Priority
Uses the requested priority when provided.
Defaults to Medium if none is supplied.
Categories marked as ForcesCriticalPriority always create Critical tickets.
SLA and Due Dates

Due dates are calculated from the ticket creation time using configurable SLA settings:

Priority	Time LimitLow	72 hours
Medium	24 hours
High	8 hours
Critical	4 hours

For premium customers:

SLA windows are reduced by 50%.
The SLA will never be less than 1 hour.

SLA status is calculated dynamically and is not stored in the database.

A ticket becomes AtRisk when 25% or less of its SLA time remains.

Agent Assignment

Tickets are assigned only to agents who:

Are active
Have not reached their ticket limit
Have the required specialization (when needed)

When multiple agents qualify:

The agent with the fewest open tickets is selected.
If tied, the lowest agent ID wins.


The test suite covers SLA behaviour, priority handling, ticket assignment, escalation rules, history tracking, tie-breaking logic, and triage workflows.

In short: DEFECT-117 was fixed by moving all filtering into the database query, and Stage A introduces the core business rules for priority, SLA management, assignment, and escalation in a clean, reusable, and fully testable domain design.


#2.2 got like 3hours
and 
api testing are here

POST /api/tickets \forced Critical, premium customer
{ 
"title": "Server is down",
 "description": "Nothing responds since morning",
 "customerId": 1,
 "categoryId": 5,
 "requestedPriority": "Low" 
}

POST /api/tickets \standard Medium, no specialist
{ 
"title": "Customer cannot log in",
 "description": "The customer reports a login error since this morning", "customerId": 2,
 "categoryId": 1,
 "requestedPriority": "Medium"
 }

POST /api/tickets \default priority
{ 
"title": "Question about my invoice",
 "description": "I would like to understand the latest invoice", "customerId": 4,
 "categoryId": 2
 }

POST /api/tickets \premium window and floor
{ 
"title": "Dashboard is slow to load",
"description": "Pages take ten seconds to open", 
"customerId": 3, 
"categoryId": 3, 
"requestedPriority": "High" 
}


POST /api/tickets \validation error
{ 
"title": "Hi", 
"description": "short", 
"customerId": 1, 
"categoryId": 1 
}


POST /api/tickets \unknown customer
{
"title": "Valid title here", 
"description": "A valid long description", 
"customerId": 999, 
"categoryId": 1 
}

POST /api/tickets/{id}/escalate \success
{ 
"reason": "Customer is waiting", 
"escalatedBy": "lead@test" 
}

POST /api/tickets/{id}/escalate \reason too short
{ 
"reason": "no", 
"escalatedBy": "lead@test" 
}

POST /api/tickets/{id}/escalate \missing actor
{
 "reason": "Customer is waiting", 
"escalatedBy": "" 
}

GET /api/tickets/99999/escalations


Persistence for ticket escalations, EF migration, automatic creation of escalation entities, escalation endpoints, escalation history endpoints, validators, SLA-aware read operations, and SQLite/integration tests.
Added the 'AddTicketEscalations' migration.
Verified formatting, build, and tests: 100 tests passed.
Confirmed that the API starts, the migration is applied, and the seeding is successful.