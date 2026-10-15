# **Product Backlog: Cyber Cougars Club Order & Event System (CCOS)**

## **Feature List**

* User registration and login (Guest, Member, Officer roles)  
* Officer management of Members and dues-paid-through status  
* Officer CRUD for Products (with regular price \+ member price, stock quantity, active/inactive)  
* Officer CRUD for Venues  
* Officer CRUD for Events (date/time, venue, capacity, members-only flag, pricing) with ability to update event details after creation  
* Event ticket sales dashboard (tickets sold vs. capacity)  
* Customer browsing of available products with correct pricing shown (member vs. regular)  
* Customer browsing of upcoming events, filtered by membership eligibility  
* Combined checkout: products and event tickets in a single order/transaction  
* Order failure handling for out-of-stock products or sold-out events  
* Customer order and ticket history view  
* Officer view of all orders, with status updates (Pending/Fulfilled/Canceled)  
* Order cancellation that restocks products and releases ticket capacity  
* Server-side enforcement of members-only access and correct pricing  
* Form validation (client- and server-side) across all data entry  
* Database transaction handling for multi-entity order writes (rollback on failure)  
* Azure deployment (App Service \+ Azure SQL) with production error logging

## **User Stories**

### **Authentication & Roles**

* As a visitor, I want to register/log in, so I can order or buy tickets.  
* As a system, I want three access levels (Guest, Member, Officer), so permissions and pricing can be enforced correctly.  
* As an officer, I want to mark a customer as a Member and set their dues-paid-through date, so their membership status is tracked in the system.  
* As a member, I want to see my own current/lapsed dues status, so I know if I qualify for member pricing and members-only events.

### **Product Management (Officer)**

* As an officer, I want to create/edit/delete products, so I can control what's available to order.  
* As an officer, I want to set a regular price and a member price per product, so members automatically see their discount.  
* As an officer, I want to set/update stock quantity, so availability stays accurate.  
* As an officer, I want to deactivate a product without deleting it, so past orders referencing it still display correctly.

### 

### **Venue & Event Management (Officer)**

* As an officer, I want to create/edit/delete venues, so events have a location to reference.  
* As an officer, I want to create/edit an event (name, date/time, venue, capacity, members-only flag, price/member price), so I can control what's open for ticket sales.  
* As an officer, I want to update event details after creation, so I can fix mistakes or change plans.  
* As an officer, I want to see how many tickets have sold vs. capacity per event, so I know when it's close to selling out.

### **Ordering & Ticket Purchasing (Customer)**

* As a customer, I want to browse available products, so I can find what I want to order.  
* As a customer, I want to see member vs. regular pricing on products, so I understand what I'll be charged.  
* As a customer, I want to browse upcoming events (excluding members-only events if I'm not a current member), so I only see what I can actually attend.  
* As a member, I want to see and purchase members-only event tickets, so I get access to member-exclusive events.  
* As a customer, I want to add products and/or event tickets to one order and submit it, so I can complete a purchase in a single transaction.  
* As a customer, I want the system to reject my order (with a clear message) if a product is out of stock or an event is sold out, so I understand why it failed instead of getting a partial order.  
* As a customer, I want to view my own order and ticket history, so I know what I've ordered and what I'm attending.

### **Order Management & Cancellation (Officer)**

* As an officer, I want to see all orders across all customers, so I can plan fulfillment.  
* As an officer, I want to update a product order's status (Pending → Fulfilled/Canceled), so customers know where things stand.  
* As an officer or customer, I want canceling an order to restock any products and free up any ticket capacity, so inventory and event availability stay accurate.

### **Stretch Goals**

* As a customer, I want email/in-app notification when my order status changes or an event I'm attending is updated.  
* As an officer, I want a simple low-stock and near-sellout report/dashboard.  
* As a customer, I want to cancel my own pending order or unattended ticket before the event/fulfillment happens.  
* As an officer, I want a dues-expiring-soon report, so I can follow up with lapsing members.

## 

## 

## 

## **Technical Tasks (non-user-facing, needed to support the above)**

* Scaffold the ASP.NET Core MVC \+ EF Core solution structure.  
* Configure a shared Azure SQL database connection for the team.  
* Create the initial EF migration from the ERD.  
* Wrap order placement in a database transaction across Order, OrderLine, and Ticket writes.  
* Enforce members-only and pricing checks server-side, not just in the UI.  
* Deploy to Azure App Service with the database in Azure SQL.  
* Add basic production error handling/logging.

---

*Next step: as a team, pull these into sprints, add story points, and assign owners.*

