# Backend Documentation

Backend documentation for the CCOS ASP.NET Core API.

## Contents

| Document | Description |
|----------|-------------|
| [database-setup.md](./database-setup.md) | Database configuration and setup instructions |
| [CyberCougar OES Schema.md](./CyberCougar%20OES%20Schema.md) | Database schema overview |
| [ERD](./ERD_%20Cyber%20Cougars%20Club%20Order%20&%20Event%20System%20(CCOS).md) | Entity Relationship Diagram |
| [Soft_Delete_Lifecycle_Data_Rules.md](./Soft_Delete_Lifecycle_Data_Rules.md) | Soft delete and data lifecycle rules |
| [Product_Migration_Scope.md](./Product_Migration_Scope.md) | Migration scope and planning |

## Tech Stack

- **Framework**: ASP.NET Core 10.0
- **Database**: SQL Server with Entity Framework Core
- **Auth**: JWT Bearer tokens
- **Architecture**: Repository pattern with DTOs

## Key Concepts

- **Soft Delete**: Entities use `IsDeleted` flag instead of hard deletes
- **Audit Fields**: All entities track `CreatedAt`, `ModifiedAt`, `CreatedBy`, `ModifiedBy`
- **Worksets**: Multi-entity operations for batch processing
