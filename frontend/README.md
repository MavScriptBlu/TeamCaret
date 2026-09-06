# frontend/

The user-facing side of CCOS - the "view" and "API client" layers from the
proposal's flow:

```
view -> API client -> Web API -> repository -> data
```

Planned project (create with `dotnet new webapp` when implementation starts):

- **CCOS.Web** - Razor Pages (or Blazor, per the team's final call) app. Calls
  `backend/CCOS.Api` over HTTP via a typed API client - never talks to the
  database directly. Handles login/register, product and event browsing,
  checkout, order history, and officer admin screens (products, venues,
  events, member dues, officer promotion).
