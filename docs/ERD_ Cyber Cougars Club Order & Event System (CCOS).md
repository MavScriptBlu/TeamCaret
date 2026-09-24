erDiagram

    CUSTOMER ||--o| MEMBER : "may be"

    MEMBER ||--o| OFFICER : "may be"

    ADMIN ||--o{ OFFICER : "grants position"

    MEMBER ||--o{ OFFICER_HISTORY : "has history"

    ADMIN ||--o{ OFFICER_HISTORY : "grants position"

    CUSTOMER ||--o{ EMAIL : "has"

    CUSTOMER ||--o{ PHONE : "has"

    CUSTOMER ||--o{ ADDRESS : "has"

    VENUE ||--|| VENUE_ADDRESS : "has"

    VENUE ||--o{ EVENT : hosts

    EVENT ||--o{ PRODUCT : "has ticket products"

    CUSTOMER ||--o{ ORDER : places

    ORDER ||--o{ ORDERLINE : contains

    PRODUCT ||--o{ ORDERLINE : "ordered as"

    TREASURY_SETTINGS ||--o{ EXPENSE : "tracks"

    TREASURY_SETTINGS ||--o{ DEPOSIT : "tracks"

    RECONCILIATION ||--o{ EXPENSE : "reviews"

    RECONCILIATION ||--o{ DEPOSIT : "reviews"

    ADMIN ||--o{ EXPENSE : "logs"

    ADMIN ||--o{ DEPOSIT : "logs"

    ADMIN ||--o{ RECONCILIATION : "performs"


    CUSTOMER {
        int CustomerId PK
        string FirstName
        string LastName
    }

    ADMIN {
        int AdminId PK
        string Username
        string PasswordHash
        bool IsAdmin
        bool IsActive
        datetime CreatedDate
        datetime LastLoginDate
    }

    MEMBER {
        int MemberId PK, FK
        date MemberExpiration
        bool IsCurrent
    }

    OFFICER {
        int OfficerId PK, FK
        string Title
        date GrantedDate
        int GrantedByAdminId FK
    }

    OFFICER_HISTORY {
        int OfficerHistoryId PK
        int MemberId FK
        string Title
        date GrantedDate
        date RevokedDate
        int GrantedByAdminId FK
    }

    EMAIL {
        int EmailId PK
        int CustomerId FK
        string EmailAddress
        bool IsPrimary
    }

    PHONE {
        int PhoneId PK
        int CustomerId FK
        string PhoneNumber
        string PhoneType
        bool IsPrimary
    }

    ADDRESS {
        int AddressId PK
        int CustomerId FK
        string AddressLine1
        string AddressLine2
        string City
        string State
        string ZipCode
        string AddressType
        bool IsPrimary
    }

    VENUE {
        int VenueId PK
        string Name
        int Capacity
    }

    VENUE_ADDRESS {
        int VenueAddressId PK
        int VenueId FK
        string AddressLine1
        string AddressLine2
        string City
        string State
        string ZipCode
    }

    EVENT {
        int EventId PK
        int VenueId FK
        string Name
        string Description
        datetime EventDateTime
        int TicketCapacity
        bool MembersOnly
    }

    PRODUCT {
        int ProductId PK
        int EventId FK
        string Category
        string Name
        string Description
        decimal Price
        decimal MemberPrice
        int StockQuantity
        bool IsActive
    }

    ORDER {
        int OrderId PK
        int CustomerId FK
        datetime OrderDate
        string Status
        decimal TotalAmount
    }

    ORDERLINE {
        int OrderLineId PK
        int OrderId FK
        int ProductId FK
        int Quantity
        decimal UnitPrice
    }

    TREASURY_SETTINGS {
        int TreasurySettingsId PK
        decimal StartingBalance
        date StartingBalanceAsOf
    }

    EXPENSE {
        int ExpenseId PK
        string Type
        date Date
        decimal Amount
        string Description
        int LoggedByAdminId FK
        datetime CreatedAt
    }

    DEPOSIT {
        int DepositId PK
        date Date
        decimal Amount
        string Source
        int LoggedByAdminId FK
        datetime CreatedAt
    }

    RECONCILIATION {
        int ReconciliationId PK
        date PeriodStart
        date PeriodEnd
        decimal StatementEndingBalance
        decimal SystemCalculatedBalance
        string Notes
        int ReconciledByAdminId FK
        datetime ReconciledAt
    }