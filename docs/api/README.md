# API Documentation

REST API reference for the CCOS backend.

## Base URL

- **Development**: `https://localhost:7001/api`
- **Production**: TBD

## Authentication

All endpoints (except login/register) require a JWT Bearer token:

```
Authorization: Bearer <token>
```

## Endpoints

### Auth
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/auth/login` | Login and get JWT |
| POST | `/auth/register` | Register new user |

### Products
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/products` | List all active products |
| GET | `/products/{id}` | Get product by ID |
| POST | `/products` | Create product |
| PUT | `/products/{id}` | Update product |
| DELETE | `/products/{id}` | Soft delete product |

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
| GET | `/events/officer` | Officer list (CAR-56): every event, removed and past ones too, newest start first. |
| GET | `/events/officer/{id}` | One event in any state, so a removed event can be opened and restored. 404 for an unknown id. |
| POST | `/events` | Create an event and its "General" ticket type in one save (CAR-55). 201 with the event. 400 for bad fields or a venue that is missing or removed. |
| PUT | `/events/{id}` | Update an event and the prices on its ticket type. `IsActive: true` restores a removed event. 400 for bad fields or a capacity below the tickets already sold, 404 for an unknown id, 409 when someone else saved first. |
| DELETE | `/events/{id}` | Remove an event (soft delete, `IsActive = false`; nothing is erased). 204, or 404 for an unknown id. Removing twice is fine. |

Event request bodies (`CreateEventRequest`, `UpdateEventRequest`) take `name` (required, up to 100),
`description` (up to 500), `startsAtUtc`, `endsAtUtc` (must be after the start), `venueId`,
`ticketCapacity` (at least 1), `membersOnly`, `ticketPrice` and `memberTicketPrice` (0 or more, member
price not above the ticket price). `UpdateEventRequest` adds `isActive` (default true). Times are UTC;
the website converts from club time (America/Chicago).

### Venues
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/venues` | Active venues by name, for the event form dropdown. |
| GET | `/venues/officer` | Officer list (CAR-57): every venue, removed ones too, with its address, by name. |
| GET | `/venues/{id}` | One venue with its address, removed ones too. Address parts are null for the sample venues. 404 for an unknown id. |
| POST | `/venues` | Create a venue and its address (CAR-51). 400 for bad fields or a name that is already used. |
| PUT | `/venues/{id}` | Update a venue and its address (CAR-57). The name can match the venue's own name. `IsActive: true` restores a removed venue. 404 for an unknown id. |
| DELETE | `/venues/{id}` | Remove a venue (CAR-58, soft delete). 204, or 404 for an unknown id. Events that already use the venue keep showing its name. |

ZIP codes (CAR-63) are 5 digits (`54481`) or ZIP+4 (`54481-1234`) on create and update. The rule lives
in `CCOS.Shared.ZipCodeRules`, so the API and the Razor forms say the same thing.

The officer endpoints have `TODO` comments to lock them to officers once login is wired up (CAR-1).

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
