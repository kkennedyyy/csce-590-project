# CSCE 590-Class Finder App

## 📂 Project Structure

```bash
csce-590-project/
│
├── backend/                 # Azure Functions (.NET 8 Isolated API Layer)
│   ├── DropClass.cs
│   ├── GetClassDetails.cs
│   ├── GetStudentDashboard.cs
│   ├── JoinStudentGroup.cs
│   ├── Negotiate.cs
│   ├── Program.cs
│   ├── host.json
│   ├── local.settings.json   # (DO NOT commit to production)
│   └── csce-590-project.csproj
│
├── frontend/ui-class/       # React (Vite) Frontend
│   ├── src/
│   ├── package.json
│   ├── vite.config.js
│   └── index.html
│
├── database/
│   ├── schema/              # Table creation scripts
│   ├── seed/                # Insert seed data scripts
│   └── stored_procedures/   # Optional future logic
│
└── README.md
```

## Git Workflow Reminders
- Always Pull Before Starting
- Create a Seperate Branch
- Push Your Branch, Then Open a Pull Request

## Required Software
1. Node.js
2. .NET 8 SDK
3. Azure Functions Core Tools v4
4. Azure Data Studio (For SQL Queries)

## VS Code Extensions
Make sure you install:
- Azure Functions
- Azure Resources
- C#
- SQL Server (mssql)

## Frontend Setup (React + Vite)
- Navigate to frontend (cd frontend then cd ui-classfinder)
- Install dependencies (npm install)
- Run frontend (npm run dev )
- Check Google Doc for how Frontend API calls should be structured

## Backend Setup (Azure Functions)
- Navigate to backend (cd backend)
- Restore packages (dotnet restore)
- Run backend (func start)
- You should see: "Functions: GetStudentDashboard: [GET]..." and "GetClassDetails: [GET]..."

## local.settings.json Setup
- Check Google Doc in Discord

## Connecting to Azure SQL
- Open Azure Data Studio
- Connect to Azure SQL Server
- Select correct database (NOT master)
- Run queries

## Testing Backend Directly
- Check Google Doc in Discord



