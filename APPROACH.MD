# Approach

How I worked through the assignment, task by task. Design decisions with their rejected
alternatives are in the [README](README.md); this file is about the order of work and the thinking.

## How I worked

I followed the priority order (Task 1, then 2, then 3) and kept every task small enough to check
by hand. For each task I settled where the logic belongs first, then built it in stages, and ended
every stage with format, build and tests, followed by my own check in Swagger, the browser or the
database. I used AI for planning, code and review, and I read each stage's result before committing
(see *AI usage* in the README). Where the spec was ambiguous I chose, wrote it down in the README, and moved on.

## Task 1: DEFECT-117 and filtering

**Defect.** I reproduced it first: a status change sent no request, and the next search request
carried old values. That pointed at the data-loading effect, not the API. The effect's dependencies
did not include the filters, so I fixed the cause by keying the request from every filter (plus a
reload counter), debouncing only the search text and ignoring stale responses. I rejected
forcing a reload, filtering in memory or remounting the component, because those hide the symptom.
The regression test asserts the actual arguments of each request, so it fails on the old code.

**Filtering.** The API accepted filter values and ignored them, so I added one small method per
filter and chained them before counting and paging. That keeps the count and pages right and means
the next filter (`slaStatus`, in Task 2) is one method and one line.

## Task 2: Escalation and assignment

The assignment says the challenge is where the rules go, so I started there. Priority, SLA and
assignment are small pure classes in the Domain (`PriorityPolicy`, `SlaPolicy`, `AgentSelector`),
and `TicketTriage` composes them for both create and escalate, so a rule exists once. Time comes
from an injected clock, which made the boundary tests exact. SLA status is derived, never stored,
and the SQL `slaStatus` filter uses the same thresholds; a test compares the two.

Then I added persistence (the `TicketEscalations` table with an index chosen for the history
query), the endpoints, and the frontend (one SLA indicator, escalation panel, history, triage
result). When I wrote the rule tests I found a bug: escalating a Critical ticket threw the wrong
exception. I fixed it so it returns `409`.

## Task 3: Login and authentication

I put credentials on `Agent` because the agents are the users, and used the framework's password
hasher behind an interface so Application stays free of infrastructure. The seeder hashes the dev
password at startup instead of using `HasData`, because random salts would change the model.
For access control I used a fallback policy so every new endpoint is protected by default, and a
test fails the build if one is left open. The escalation actor now comes from the token, so a
client cannot claim to be someone else. On the frontend, `api/client.ts` attaches the token and
handles a `401` in one place.

## Task 4 and the optional report

I did not attempt Task 4 (Docker) or Task 2.5 (SLA report). The assignment says an honest
unfinished task beats a rushed one, so I spent the time verifying Tasks 1 to 3 end to end and
documenting them. The README says what I would do for the image (multi-stage, non-root, no baked
secrets, `/health`, migrations as a separate deployment step).

