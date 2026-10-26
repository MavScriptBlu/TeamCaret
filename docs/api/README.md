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
| POST | `/events` | Create event (TODO) |
| PUT | `/events/{id}` | Update event (TODO) |

### Venues
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/venues` | Active venues by name, for the event form dropdown. |
| GET | `/venues/{id}` | Get venue by ID (TODO) |
| POST | `/venues` | Create venue (TODO) |
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
