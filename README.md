# Dynamic Backend-as-a-Service (BaaS)

A powerful, dynamic Backend-as-a-Service built with **.NET 8** and **Entity Framework Core**. This engine allows users to upload CSV or Excel files, automatically detects schemas, provisions PostgreSQL tables on the fly, and instantly generates complete, secure CRUD API endpoints for their data.

## Features

- **Instant API Generation**: Upload tabular data (CSV/XLSX/TXT), and the engine instantly provisions a PostgreSQL table and prepares dynamic `GET`, `POST`, `PUT`, and `DELETE` endpoints.
- **Dynamic Database Provisioning**: Automatically parses columns, detects the most appropriate PostgreSQL data types, and creates tables dynamically.
- **Custom Authentication**: Uses a robust API-key based authentication system backed by an `AppUsers` table. It operates entirely independently of third-party auth services (like Supabase Auth or Firebase Auth), ensuring there is **no vendor lock-in**.
- **Role-Based Access**: Generates unique `Admin` and `Read-Only` API keys per user for secure sharing or application integration.
- **Built-in UI Portal**: Includes a beautiful, vanilla HTML/JS portal (`wwwroot/`) to manage signups, logins, schema preview, and file uploads.
- **Swagger Integration**: Live OpenApi specifications generated dynamically for every uploaded table.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A running PostgreSQL instance (or [Supabase](https://supabase.com) Postgres connection string).

## Setup & Run

1. **Configure Database**:
   Open `appsettings.json` and replace the `DefaultConnection` string with your PostgreSQL connection string.
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=your_host;Port=5432;Database=your_db;Username=postgres;Password=your_password;SSL Mode=Require;Trust Server Certificate=true"
   }
   ```

2. **Apply Database Migrations**:
   The engine uses Entity Framework Core to manage the `AppUsers` authentication table. Ensure your database is initialized by running:
   ```bash
   dotnet ef database update
   ```
   *(If you don't have the EF Core tools installed, first run `dotnet tool install --global dotnet-ef`)*

3. **Start the Application**:
   ```bash
   dotnet run
   ```
   The application will start, and you can open the browser to the provided `https://localhost:<port>` address.

## How to Use

1. **Access the Portal**: Open the application URL in your browser.
2. **Register**: Switch to the **Register** tab and create a new account. The application will provision your personal API keys.
3. **Upload Data**: Upload any CSV or Excel file. The application will ingest the file, detect the data types, create a dedicated table in Postgres, and import all the rows.
4. **Test Your APIs**: Once uploaded, your live API endpoints will be displayed on-screen. Copy your `Admin API Key` and pass it in the `x-api-key` header when testing the APIs in Postman or cURL.

## Security

Every provisioned dataset is strictly tied to the user who uploaded it. All dynamically generated endpoints verify the incoming `x-api-key` header to ensure the requester is authenticated and has permission (Admin vs Read-Only) to access or modify the underlying table.

## Tech Stack

- **Backend**: C#, ASP.NET Core Web API, .NET 8
- **Database Access**: Entity Framework Core & Npgsql (PostgreSQL provider)
- **Data Parsing**: CsvHelper
- **Frontend**: Vanilla HTML5, CSS3, JavaScript (Served statically via `wwwroot`)
