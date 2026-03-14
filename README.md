# To-Do List App — Project Summary

A full-stack to-do list application built with **ASP.NET Core (.NET 10)** and a vanilla JavaScript frontend, deployed to **AWS Elastic Beanstalk**.

**Live URL:** http://todolist-env.eba-mzaby3fm.us-east-1.elasticbeanstalk.com

---

## How This Project Was Built (Session Recap)

### Step 1 — Console App (C#)

The project started as a simple C# console application. The goal was to build a to-do list with full **CRUD** operations:

- **Create** — add a new task
- **Read** — view all tasks
- **Update** — edit an existing task
- **Delete** — remove a task

Tasks were stored locally in a JSON file (`data/tasks.json`) using `System.Text.Json`, with a `SemaphoreSlim` for thread-safe async reads and writes.

The initial project targeted **.NET 8**, but since only .NET 10 was installed on the machine, the target framework was updated to `net10.0` in `TodoList.csproj`.

---

### Step 2 — Reminder Feature

A reminder option was added to each task:

- Tasks have a `ReminderAtUtc` field (nullable `DateTime`)
- Users can set or clear a reminder per task
- Due reminders are highlighted in yellow in the console UI
- The reminder value is persisted in `data/tasks.json`

---

### Step 3 — Web Migration (ASP.NET Core Minimal API)

The app was converted from a console app to a browser-accessible web app using **ASP.NET Core Minimal API**:

- Project SDK changed to `Microsoft.NET.Sdk.Web`
- REST endpoints added:
  - `GET /api/tasks` — list all tasks
  - `POST /api/tasks` — create a task
  - `PUT /api/tasks/{id}` — update a task (title, done status, reminder)
  - `DELETE /api/tasks/{id}` — delete a task
- A single-page frontend was created at `wwwroot/index.html` using vanilla JavaScript and the `fetch()` API
- Dark-themed responsive UI with add, edit, delete, toggle, and reminder buttons

---

### Step 4 — AWS Elastic Beanstalk Deployment

#### Why Not GitHub Pages?
GitHub Pages only serves static files — it cannot run a .NET backend. AWS was chosen instead.

#### AWS Setup
- **AWS CLI v2** installed and configured with root account credentials (`us-east-1`, Account: `884058772355`)
- **EB CLI 3.22.1** installed via `pip install awsebcli`
- `PATH` updated to include `C:\Users\rodog\AppData\Roaming\Python\Python314\Scripts`

#### Elastic Beanstalk Initialization
```
eb init todolist --platform "64bit Amazon Linux 2023 v3.9.0 running .NET 10" --region us-east-1
eb create todolist-env
```

#### Deployment Failures & Fixes

**Failure 1 — SDK not found**
EB deployed git source code instead of the published output. The server didn't have the .NET SDK to compile it.
- Fix: Used `dotnet publish -c Release -o ./deploy-output` to produce a self-contained published build.

**Failure 2 — Backslash paths in zip**
Windows `Compress-Archive` creates zip entries with backslash (`\`) path separators. Linux `unzip` rejects them:
```
warning: appears to use backslashes as path separators
```
- Fix: Rebuilt the zip using .NET's `ZipFile` API, replacing all `\` with `/` in entry names.

**Failure 3 — Port not bound**
The app wasn't explicitly listening on `0.0.0.0:5000`, so nginx couldn't proxy to it.
- Fix: Updated `Procfile` to:
  ```
  web: dotnet TodoList.dll --urls http://0.0.0.0:5000
  ```

#### Successful Deployment (app-v4)
After all three fixes, `eb deploy --label app-v4` succeeded:
```
Instance deployment completed successfully.
Environment update completed successfully.
Health: Green | Status: Ready
```

---

## Project Structure

```
todolist/
├── Program.cs                          # ASP.NET Core minimal API + all backend logic
├── TodoList.csproj                     # Project file (net10.0, Web SDK)
├── Procfile                            # EB process definition
├── wwwroot/
│   └── index.html                      # Single-page frontend (vanilla JS)
├── data/
│   └── tasks.json                      # Local task storage (auto-created)
├── deploy-output/                      # dotnet publish output (not committed)
├── deploy.zip                          # EB deployment bundle (not committed)
├── .elasticbeanstalk/
│   └── config.yml                      # EB CLI config (app, env, platform, artifact)
└── .gitignore
```

---

## Running Locally

```bash
dotnet run
```

Open your browser at `http://localhost:5000`.

---

## Deploying to AWS

```bash
# Publish
dotnet publish TodoList.csproj -c Release -o ./deploy-output
Copy-Item Procfile deploy-output\Procfile

# Build zip with forward-slash paths (PowerShell)
Add-Type -Assembly "System.IO.Compression.FileSystem"
$zip = [System.IO.Compression.ZipFile]::Open("deploy.zip", "Create")
Get-ChildItem deploy-output -Recurse -File | ForEach-Object {
    $name = $_.FullName.Substring((Resolve-Path deploy-output).Path.Length + 1).Replace("\", "/")
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $name) | Out-Null
}
$zip.Dispose()

# Deploy
eb deploy todolist-env --label app-vX
```

---

## Tech Stack

| Layer       | Technology                        |
|-------------|-----------------------------------|
| Backend     | ASP.NET Core Minimal API (.NET 10)|
| Frontend    | Vanilla JavaScript, HTML, CSS     |
| Storage     | JSON file (`data/tasks.json`)     |
| Hosting     | AWS Elastic Beanstalk             |
| Platform    | Amazon Linux 2023, .NET 10        |
| Web Server  | nginx (reverse proxy → port 5000) |
