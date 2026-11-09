# API Documentation

REST API reference for the CCOS backend.

## Base URL

- **Development**: `https://localhost:7001/api`
- **Production**: TBD

## Authentication

Sign-in uses ASP.NET Core Identity in the API (CAR-67, `docs/CAR-52_Login_Approach.md`). Logging in
returns an access token (Identity's own bearer token, not a JWT) and a refresh token. Send the access
token on calls that need a signed-in person:

```
Authorization: Bearer <accessToken>
```

Two access rules (policies) decide what a signed-in person can do. Both read the Members and Officers
tables on every request, on the club's date (America/Chicago), so nothing about membership is cached in
the token:

| Policy | Passes when |
|--------|-------------|
| `Member` | Signed in, and a current member (active, dues paid through today) |
| `Officer` | Signed in, a current member, and holding an officer title. The club's advisor (the `Advisor` role) also passes, without a membership or title. |

Not signed in returns `401`; signed in but not allowed returns `403`. Since CAR-69 the officer endpoints
below need `Officer`, and member prices (`memberPrice`) only go to callers who pass `Member`; everyone
else gets `null`. Browsing (GET) stays open to everyone and only returns active products and active, upcoming events.

## Endpoints

### Auth
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/account/register` | Create an account: `firstName`, `lastName`, `email`, `password`. Creates the Customer and the login together. 400 with the message on `Email` ("An account with that email already exists.") or `Password` (too weak). |
| POST | `/auth/login` | Sign in with `email` and `password`. Returns `accessToken`, `refreshToken` and `expiresIn` (seconds). 401 on a wrong email or password. |
| POST | `/auth/refresh` | Trade a `refreshToken` for a new access token. |
| POST | `/auth/forgotPassword`, `/auth/resetPassword` | Identity's password reset (no email is sent yet). |
| GET | `/me` | Who's signed in: `customerId`, names, `email`, `memberStatus` (Active, Expired, Inactive or null for a guest), `isCurrentMember`, `isCurrentOfficer`, `officerTitle`, `isAdvisor`. 401 when not signed in. |

Identity's own `/auth/register` is turned off (404), because it would create a login without a Customer.

### Products
| Method | Endpoint | Who | Description |
|--------|----------|-----|-------------|
| GET | `/products` | Everyone | Active products. `memberPrice` only for current members. |
| GET | `/products?includeInactive=true` | Officer | Every product, including removed ones, with member prices |
| GET | `/products/{id}` | Everyone | One active product (404 otherwise). `memberPrice` only for current members. |
| POST | `/products` | Officer | Create product |
| PUT | `/products/{id}` | Officer | Update product |
| PATCH | `/products/{id}/reactivate` | Officer | Put a removed product back in the store |
| DELETE | `/products/{id}` | Officer | Remove product (soft delete: `IsActive = false`) |

### Members
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/members` | List all members |
| GET | `/members/{id}` | Get member by ID |
| POST | `/members` | Create member |
| PUT | `/members/{id}` | Update member |

### Events
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/events` | Upcoming events: active, StartsAtUtc >= now, soonest first. Includes venue name and ticket types; ticket `memberPrice` only for current members (null otherwise). |
| GET | `/events/{id}` | One upcoming event (active, starting from now on) with venue name and active ticket types. 404 for unknown, removed or past events. Ticket `memberPrice` only for current members. |
| GET | `/events/officer` | Officer list (CAR-56): every event, removed and past ones too, newest start first. Officer only. |
| GET | `/events/officer/{id}` | One event in any state, so a removed event can be opened and restored. 404 for an unknown id. Officer only. |
| POST | `/events` | Create an event and its "General" ticket type in one save (CAR-55). 201 with the event. 400 for bad fields or a venue that is missing or removed. Officer only. |
| PUT | `/events/{id}` | Update an event and the prices on its ticket type. `IsActive: true` restores a removed event. 400 for bad fields or a capacity below the tickets already sold, 404 for an unknown id, 409 when someone else saved first. Officer only. |
| DELETE | `/events/{id}` | Remove an event (soft delete, `IsActive = false`; nothing is erased). 204, or 404 for an unknown id. Removing twice is fine. Officer only. |

Event request bodies (`CreateEventRequest`, `UpdateEventRequest`) take `name` (required, up to 100),
`description` (up to 500), `startsAtUtc`, `endsAtUtc` (must be after the start), `venueId`,
`ticketCapacity` (at least 1), `membersOnly`, `ticketPrice` and `memberTicketPrice` (0 or more, member
price not above the ticket price). `UpdateEventRequest` adds `isActive` (default true). Times are UTC;
the website converts from club time (America/Chicago).

### Venues
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/venues` | Active venues by name, for the event form dropdown. |
| GET | `/venues/officer` | Officer list (CAR-57): every venue, removed ones too, with its address and `upcomingEvents`, by name. Officer only. |
| GET | `/venues/{id}` | One venue with its address and `upcomingEvents`, removed ones too. Address parts are null for venues saved before addresses existed. 404 for an unknown id. Officer only. |
| POST | `/venues` | Create a venue and its address (CAR-51). 400 for bad fields or a name that is already used. Officer only (CAR-69). |
| PUT | `/venues/{id}` | Update a venue and its address (CAR-57). The name can match the venue's own name. `IsActive: true` restores a removed venue. 404 for an unknown id. Officer only. |
| DELETE | `/venues/{id}` | Remove a venue (CAR-58, soft delete). 204, or 404 for an unknown id. Allowed even when upcoming events are booked there (CAR-79: warn, don't block); those events keep the venue and still show its name. Officer only. |

`upcomingEvents` (CAR-79) lists the venue's active events that haven't started yet, soonest first, as
`{ eventId, name, startsAtUtc }`. The officer Venues page uses it to warn in the "Remove venue?" dialog.

ZIP codes (CAR-63) are 5 digits (`54481`) or ZIP+4 (`54481-1234`) on create and update. The rule lives
in `CCOS.Shared.ZipCodeRules`, so the API and the Razor forms say the same thing.

"Officer only" endpoints need the `Officer` policy (CAR-69): 401 when not signed in, 403 when signed in but not an officer.

### Orders
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/orders` | List all orders |
| GET | `/orders/{id}` | Get order by ID |
| POST | `/orders` | Create order |

## Response Format

All responses follow standard REST conventions:
- `200 OK` - Success
- `201 Created` - Resource created
- `400 Bad Request` - Validation error
- `401 Unauthorized` - Missing/invalid token
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server error
