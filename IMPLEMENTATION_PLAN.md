# Off the Grid Implementation Plan

## Scope Baseline
This plan is derived from `plan.md` and implemented as a phased baseline in this repository.

## Architecture
- Backend: .NET Web API (`OTG.Api`) with layered projects (`OTG.Application`, `OTG.Domain`, `OTG.Infrastructure`).
- Data: Azure Cosmos DB (SQL API) with typed repositories and partition-key-aware access patterns.
- Frontend: Angular + SKY UX-oriented scaffold (role-aware shell/routing/guards) under `frontend/`.

## Cosmos Containers and Partition Keys
- `users` -> `/id`
- `hackathons` -> `/hackathonId`
- `ideas` -> `/hackathonId`
- `comments` -> `/ideaId`
- `ratings` -> `/ideaId`
- `teams` -> `/hackathonId`
- `teamJoinRequests` -> `/teamId`
- `teamInvites` -> `/teamId`

## Implemented Backend Steps
1. Solution scaffolding with API, domain/application/infrastructure projects, and tests.
2. Domain models created for identity/profile/roles, hackathons/tracks/awards/criteria/milestones, ideas/comments/ratings, teams/requests/invites.
3. Application repository interfaces added for all core entities.
4. Cosmos options, singleton client registration, container provider, and startup bootstrap hosted service implemented.
5. Cosmos document models and mapping layer implemented.
6. Repository implementations added with partition key usage and query methods.
7. API startup wired with JWT auth, authorization, health checks, Cosmos services, and config sections.
8. Review feedback applied for critical fixes:
   - Hackathon document identity consistency.
   - Idea status query and partition scoping correctness.
   - JWT signing key now required (no insecure fallback).
9. Initial API endpoints implemented:
   - `api/auth` register/login/me + Blackbaud OAuth start/callback wiring.
   - `api/hackathons/{hackathonId}` read endpoint.
   - `api/ideas` search/get/upsert endpoint set.
   - `api/teams` search/get/upsert endpoint set.
   - `api/profile/me` get/update endpoint set.
10. Additional security hardening applied from review:
   - JWT lifetime validation and clock skew configured.
   - Blackbaud OAuth state is stored and validated as one-time state.
   - Idea/team updates now load existing records and enforce owner-or-admin checks.
11. Blackbaud OAuth callback now implements server-side flow:
   - Exchanges authorization code via `https://oauth2.sky.blackbaud.com/token` using Basic auth.
   - Fetches current SKY user profile and merchant account data.
   - Finds/creates user by email and syncs Blackbaud profile fields.
   - Issues sign-in token via HttpOnly cookie and redirects back to allowed frontend origin.
12. Additional callback hardening from review:
   - Enforces redirect origin allowlist.
   - Removes token from query string to prevent leakage.
   - Sanitizes provider error details returned to clients.
   - Makes OAuth state consume atomic under concurrency.
13. Refresh-token persistence and rotation implemented:
   - Stores Blackbaud refresh token + token metadata on linked profiles.
   - Adds authenticated `POST /api/auth/blackbaud/refresh` endpoint.
   - Rotates refresh token on each refresh-token grant response.
14. OAuth integration tests added:
   - Callback success path (state -> exchange -> profile sync -> redirect + auth cookie).
   - Callback failure path (OAuth exchange exception -> 502 problem response).
15. Authorization and judging/admin workflow baseline added:
   - `NotBanned` authorization policy and handler wired into API.
   - Added `api/judging/assigned` and `api/judging/ratings` endpoints for judge/admin flows.
   - Added `api/admin/judging/assign-judge` and `api/admin/judging/mark-winner` endpoints.
16. Refresh endpoint test coverage expanded:
   - Success path with refresh-token rotation persistence.
   - Not-linked profile rejection path.
   - Upstream refresh failure path.
   - Banned-user forbidden path (policy enforcement).
17. Judging/admin integration coverage expanded:
   - Judge assigned-ideas view returns only assigned submissions.
   - Participant access to judge-only endpoints is forbidden.
   - Judge scoring is blocked when not assigned.
   - Admin judge assignment enables subsequent judge rating submission.
18. Admin user-management endpoints implemented:
   - `GET /api/admin/users` for bounded user search/listing.
   - `PUT /api/admin/users/{userId}/roles` for role assignment updates.
   - `PUT /api/admin/users/{userId}/ban` for ban/unban toggling.
19. Admin security hardening applied:
   - Added bounded `IUserRepository.SearchAsync(query, limit)` contract.
   - Prevents self-demotion and self-ban for admins.
   - Enforces at least one unbanned admin remains.
   - Normalizes returned role values to lowercase.
20. Admin user-management integration tests added:
   - Non-admin forbidden access.
   - Role update success path.
   - Ban toggle success path.
   - Invalid role payload rejection.
   - Self-demotion/self-ban rejection.
   - Last-admin protection conflict path.
21. Winner visibility APIs implemented:
   - `GET /api/winners?hackathonId=...` returns winners grouped by track.
   - Includes special-recognition winners without track assignment.
22. Analytics summary API implemented:
   - `GET /api/analytics?hackathonId=...` (admin-only) returns ideas/teams/user role counts,
     ideas-by-status, and team size distribution.
23. Winner/analytics integration tests added:
   - Winner grouping and special-recognition behavior.
   - Winner visibility after admin mark-winner action.
   - Analytics role restriction and aggregate payload validation.
24. Team join/invite workflow APIs implemented:
   - `POST /api/teams/{teamId}/join-requests` to submit participant join requests.
   - `GET /api/teams/{teamId}/join-requests` and approve/reject actions for leader/admin moderation.
   - `POST /api/teams/{teamId}/invites` and `GET /api/teams/{teamId}/invites` for leader/admin invite management.
   - `POST /api/teams/{teamId}/invites/{inviteId}/approve` and `POST /api/teams/{teamId}/invites/accept?token=...` for approval + acceptance flow.
   - Enforces team-capacity constraints and duplicate/pending membership protections.
25. Team workflow integration tests added:
   - Join request create + leader approval adds member.
   - Team-full conflict path on join-request approval.
   - Invite acceptance blocked until required approval.
   - Invite approval + acceptance adds member.
26. Comment/rating moderation APIs implemented:
   - `PUT /api/admin/moderation/comments/{commentId}?ideaId=...&hackathonId=...` for admin moderation toggling.
   - `PUT /api/admin/moderation/ratings/{ratingId}?ideaId=...&hackathonId=...` for admin moderation toggling.
   - Adds moderation metadata fields (`isModerated`, reason, moderator id, timestamp) to comment/rating domain + persistence mapping.
27. Moderation integration tests added:
   - Admin comment moderation success path.
   - Non-admin comment moderation forbidden path.
   - Admin rating moderation success path.
   - Rating-missing not-found path.
28. Participant/judge-facing moderated read APIs implemented:
   - `GET /api/ideas/{ideaId}/comments?hackathonId=...` with moderation-aware filtering.
   - `GET /api/ideas/{ideaId}/ratings?hackathonId=...` with moderation-aware filtering.
   - Admin receives full datasets; non-admin users receive only non-moderated records.
29. Moderation visibility integration tests added:
   - Participant comment feed excludes moderated comments.
   - Admin comment feed includes moderated comments.
   - Judge rating view excludes moderated ratings.
   - Admin rating view includes moderated ratings.
30. Participant comment write APIs implemented:
   - `POST /api/ideas/{ideaId}/comments?hackathonId=...` to create top-level comments and single-depth replies.
   - `PUT /api/ideas/{ideaId}/comments/{commentId}?hackathonId=...` to update comment content.
   - Enforces non-empty content, parent existence, and max thread depth of one reply level.
   - Enforces update ownership (author or admin).
31. Comment write integration tests added:
   - Top-level comment create success path.
   - Reply-to-top-level success path.
   - Reply-to-reply depth rejection path.
   - Author update success path.
   - Non-author participant update forbidden path.
32. Moderation-aware leaderboard aggregate API implemented:
   - Added `GET /api/leaderboard?hackathonId=...` for authenticated users.
   - Aggregates idea ranking by average weighted rating and rating count.
   - Excludes moderated ratings for non-admin users; includes all ratings for admins.
33. Leaderboard integration tests added:
   - Participant leaderboard excludes moderated ratings from averages/counts.
   - Admin leaderboard includes moderated ratings in averages/counts.
   - Leaderboard ordering verified by descending average score.
34. Leaderboard pagination/filtering enhancements implemented:
   - Added optional `trackId` query filter.
   - Added `offset` and `limit` query parameters with bounds validation.
   - Response now includes `total`, `offset`, `limit`, and selected `trackId` metadata.
35. Leaderboard pagination/filter integration tests added:
   - Track-filter path returns only matching ideas.
   - Offset/limit path returns expected page slice with stable ordering.
36. Leaderboard minimum-rating threshold support implemented:
   - Added optional `minRatingCount` query parameter.
   - Enforces `minRatingCount >= 1` validation.
   - Filters leaderboard rows before pagination when visible rating count is below threshold.
   - Returns threshold value in response metadata.
37. Leaderboard threshold integration tests added:
   - `minRatingCount` includes only ideas meeting the required rating count.
   - Invalid threshold request returns `400 Bad Request`.
38. Deterministic leaderboard tie-break ordering implemented:
   - Added stable final ordering key on `IdeaId` when score and rating-count ties occur.
39. Tie-break integration test added:
   - Equal-score/equal-count ideas now return deterministic, repeatable order.
40. Leaderboard sort-mode parameter implemented:
   - Added `sortMode` query parameter with validated values: `score`, `count`, `recent`.
   - `score`: average weighted score desc, then rating count desc, then idea id.
   - `count`: rating count desc, then average weighted score desc, then idea id.
   - `recent`: latest visible rating update desc, then score/count tie-breaks.
   - Response now includes normalized `sortMode` metadata.
41. Leaderboard sort-mode integration tests added:
   - `count` mode ordering behavior.
   - `recent` mode ordering behavior.
   - Invalid `sortMode` request returns `400 Bad Request`.
42. Leaderboard read caching and conditional requests implemented:
   - Added short-lived in-memory cache for leaderboard query results by normalized query key.
   - Added ETag generation for leaderboard responses.
   - Added `If-None-Match` handling returning `304 Not Modified` when tags match.
   - Added `Cache-Control: private, max-age=30` response header.
43. Leaderboard caching integration test added:
   - First request emits ETag.
   - Repeated request with matching `If-None-Match` receives `304 Not Modified`.
44. Confidence-weighted leaderboard variant implemented:
   - Added `sortMode=confidence`.
   - Added configurable `confidencePivot` parameter with validation (`> 0`).
   - Confidence score computed from average weighted score scaled by rating-count confidence factor.
   - Cache key/ETag canonicalization and response metadata expanded to include confidence parameters.
45. Confidence-variant integration tests added:
   - Confidence mode ranks higher-confidence ideas above single-rating outliers.
   - Invalid `confidencePivot` returns `400 Bad Request`.
46. Per-track leaderboard rollup endpoint implemented:
   - Added `GET /api/leaderboard/tracks?hackathonId=...`.
   - Reuses moderation-aware ranking logic and supports `sortMode`, `minRatingCount`, and `confidencePivot`.
   - Adds `perTrackLimit` to cap returned items per track bucket.
47. Per-track rollup integration tests added:
   - Verifies grouping by track and ordering within each track.
   - Verifies per-track limit behavior and participant/admin moderation visibility differences.
48. Leaderboard rank metadata implemented:
   - Added `rank` and `deltaFromTop` to leaderboard items.
   - Rank/delta are computed after sorting and before pagination for stable page slices.
   - Rank metadata is included for both overall and per-track leaderboard endpoints.
49. Rank metadata integration tests added:
   - Verifies overall leaderboard rank and delta values.
   - Verifies per-track rollup rank and delta values.
50. Leaderboard percentile metadata implemented:
   - Added `percentile` to leaderboard items for both overall and per-track responses.
   - Percentile is computed from rank position using the full ordered result set prior to pagination.
51. Percentile integration tests added:
   - Verifies percentile values in overall leaderboard ordering assertions.
   - Verifies percentile values in per-track rollup ordering assertions.
52. Percentile band-label metadata implemented:
   - Added `band` field to leaderboard items derived from percentile thresholds.
   - Current thresholds: `platinum` (>=90), `gold` (>=75), `silver` (>=50), `bronze` (<50).
   - Band metadata included in overall and per-track leaderboard payloads.
53. Band-label integration tests added:
    - Verifies top and bottom band assignment in overall leaderboard assertions.
    - Verifies top and bottom band assignment in per-track rollup assertions.
54. Hackathon-configurable leaderboard band thresholds implemented:
    - Added optional `LeaderboardBands` settings on hackathon domain/persistence models.
    - Leaderboard and per-track endpoints now apply configured percentile thresholds for band labels.
    - Leaderboard response metadata now includes effective band-threshold values.
    - Added integration coverage for configured-threshold band assignment.
55. Resource-level judging authorization policy handler implemented:
    - Added `AssignedJudgeOrAdminRequirement`/handler for idea-scoped judge assignment checks.
    - `POST /api/judging/ratings` now uses `IAuthorizationService` with idea resource authorization.
    - Added unit tests covering assigned-judge, admin, and unassigned-judge authorization paths.

## Implemented Frontend Steps
1. Role and auth state models/services created.
2. `authGuard` and `roleGuard` created.
3. Role-based nav config created.
4. App shell component created with role-filtered navigation.
5. Route placeholders created for all primary pages from spec.
6. Dashboard and placeholder feature components created.
7. App bootstrap config and root component created.

## Remaining Execution Plan
### Backend
- Expand feature controllers/use-cases (judging/admin) and policy handlers.
- Expand policy handlers for fine-grained ownership/assignment checks at resource level.
- Add configurable tier-threshold settings per hackathon (admin-defined leaderboard band cutoffs).

### Frontend
- Initialize full Angular workspace runtime files (`angular.json`, `package.json`) once Node is available.
- Replace placeholders with SKY UX pages by role.
- Add API client layer and auth session integration.
- Build out admin tabs, judging flows, and analytics views.

## Environment Constraints Encountered
- Node/NPM is not installed in current environment, so Angular CLI generation and frontend build verification could not be executed here.
- CodeRabbit CLI is not installed in current environment.
