# CAM Process Solutions



## Architecture

- **Frontend (React)**: User interface for order processing
- **Backend (ASP.NET Core)**: Business logic and data processing
- **Database (SQL Server)**: Historical data storage

## Project Structure

### CAMProcessSolutions.Web
ASP.NET Core Web API project with the following features:
- **Framework**: .NET 8.0
- **HTTPS**: Configured for secure communication
- **OpenAPI/Swagger**: API documentation and testing interface
- **Controllers**: RESTful API endpoints

## Getting Started

### Prerequisites
- .NET 8.0 SDK or later
- Visual Studio 2022 (recommended) or VS Code

### Build and Run

1. Navigate to the project directory:
   ```bash
   cd CAMProcessSolutions.Web
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the project:
   ```bash
   dotnet build
   ```

4. Run the application:
   ```bash
   dotnet run
   ```

5. Access the Swagger UI:
   - HTTPS: https://localhost:7078/swagger
   - HTTP: http://localhost:5173/swagger

## Development

### Phase 1: Project Setup ✓
- [x] Created ASP.NET Core Web API project
- [x] Configured .NET 8.0 framework
- [x] Enabled HTTPS support
- [x] Enabled OpenAPI/Swagger support
- [x] Set up project structure

### Next Steps
- Phase 2: Database setup with SQL Server
- Phase 3: Business logic implementation
- Phase 4: React frontend development
