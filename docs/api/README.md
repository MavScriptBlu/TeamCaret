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
| GET | `/events` | Upcoming events: active, StartsAtUtc >= now, soonest first. Includes venue name and ticket types. |
| GET | `/events/{id}` | One active event with venue name and active ticket types (price & member price). 404 for unknown/inactive. |
| POST | `/events` | Create event (TODO; Officer when added) |
| PUT | `/events/{id}` | Update event (TODO; Officer when added) |

### Venues
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/venues` | Active venues by name, for the event form dropdown. |
| GET | `/venues/{id}` | Get venue by ID (TODO) |
| POST | `/venues` | Create venue and its address. Officer only. |
| PUT | `/venues/{id}` | Update venue (TODO) |

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
