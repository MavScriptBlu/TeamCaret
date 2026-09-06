# shared/

Code shared between `backend/` and `frontend/` so the two projects can't drift
out of sync on what an API request/response looks like.

Planned project (create with `dotnet new classlib` when implementation starts):

- **CCOS.Shared** - DTOs/request models for the API contract (e.g. `ProductDto`,
  `PlaceOrderRequest`) and shared enums (`OrderStatus`, `TicketStatus`).
  Referenced by both `backend/CCOS.Api` and `frontend/CCOS.Web` - never by
  `backend/CCOS.Data` directly, so entity types stay separate from wire types.
