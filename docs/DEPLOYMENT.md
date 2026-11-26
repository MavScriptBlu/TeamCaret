# Deploying CCOS

How to run CCOS somewhere other than your own computer (CAR-84). It doesn't depend on a
particular host. Troy picked Azure (CAR-87), and the step by step for that is in
[DEPLOYMENT_AZURE.md](./DEPLOYMENT_AZURE.md).

CCOS is two apps and a database:

| Piece | Project | Needs |
|---|---|---|
| API | `backend/CCOS.Api` | A PostgreSQL database |
| Website | `frontend/CCOS.Web` | The API's address |
| Database | PostgreSQL 17 | Nothing else |

Both apps are .NET 10 and each has a Dockerfile (`backend/CCOS.Api/Dockerfile`,
`frontend/CCOS.Web/Dockerfile`), so any host that runs .NET 10 or containers works. Build both
images from the repo root, since the API and website both need `shared/`.

## 1. Settings

Nothing host-specific is in the code. Everything comes from settings, which a host sets as
environment variables. Environment variables use a double underscore where the setting name has
a colon (`ConnectionStrings:CCOS` becomes `ConnectionStrings__CCOS`).

### API

| Environment variable | Required | What it is |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` on a real host. `Development` adds sample data and logins, so don't use it there. |
| `ConnectionStrings__CCOS` | Yes | The PostgreSQL connection string, for example `Host=...;Port=5432;Database=...;Username=...;Password=...`. The API won't start outside Development without it. |
| `Database__MigrateOnStartup` | No | `true` makes the API create and update the tables each time it starts (see step 2). Default `false`. |

### Website

| Environment variable | Required | What it is |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` on a real host. |
| `Api__BaseUrl` | Yes | The API's address, ending in `/`, for example `https://ccos-api.example.com/`. The site won't start without it. |
| `DataProtection__KeysPath` | Recommended | A folder that survives restarts. The site keeps the keys for its sign-in cookies there. Without it, every restart signs everyone out. |

Keep the connection string and passwords in the host's secret settings, never in the repo.

## 2. Database tables

Outside Development nothing creates the tables by itself. Pick one:

- **From your computer** (needs network access to the database):

  ```
  dotnet tool restore
  $env:CCOS_CONNECTION_STRING = "Host=...;Port=5432;Database=...;Username=...;Password=..."
  dotnet ef database update --project backend/CCOS.Data
  ```

  (On macOS or Linux: `export CCOS_CONNECTION_STRING="..."`.) This is the same as `task migrate`,
  pointed at the hosted database.

- **On startup:** set `Database__MigrateOnStartup=true` on the API. It applies any new migrations
  each time it starts, which is easiest when the database can't be reached from outside the host.

Both are safe to run again; migrations that are already applied are skipped.

Outside Development there's no sample data and no sample logins. Accounts are created through
the website's Create account page.

## 3. Start the apps

Start the API first, then the website with `Api__BaseUrl` pointing at the API. With the
Dockerfiles, both listen on port 8080 inside the container.

## 4. Check it works

Both apps have a health check that needs no sign-in:

| URL | Healthy (200) means |
|---|---|
| `<api address>/health` | The API is up and can reach the database. |
| `<website address>/health` | The website is up and the API's `/health` is healthy. |

Each answers `Healthy`, or `Unhealthy` with status 503. Point the host's health check or uptime
monitor at the website's `/health`, since it checks all three pieces at once.

Then open the website, create an account, and log in.

## 5. Our live setup on Azure (CAR-98, CAR-53)

This is what's actually running right now, built from the steps in
[DEPLOYMENT_AZURE.md](./DEPLOYMENT_AZURE.md). If you change anything in Azure, update this table
too so the next person isn't guessing.

### What's where

| Piece | Azure name | Details |
|---|---|---|
| Resource group | `ccos-rg` | Holds everything below. |
| Database server | `ccos-db-caret26` | Azure Database for PostgreSQL flexible server, PostgreSQL 17, Burstable B1ms, 32 GB, **Central US** |
| Database | `ccos` | Inside that server. Admin user is `ccosadmin`. |
| App Service plan | `ccos-plan` | Linux, **Free F1**, **West US 2** |
| API | `ccos-api-caret26` | `backend/CCOS.Api`, runtime `DOTNETCORE:10.0`, HTTPS only |
| Website | `ccos-web-caret26` | `frontend/CCOS.Web`, runtime `DOTNETCORE:10.0`, HTTPS only |

Why the plan is in a different region than the database: our student subscription had zero
quota for B1 or F1 plans in Central US, North Central US, East US 2 and East US. West US 2 was
the first one that let us in. The apps still reach the database fine, it's just a little slower
than if they sat next to each other.

What Free F1 means for us:

- The apps fall asleep after about 20 minutes with no visitors. The first click after a nap is
  slow, so open the site a minute before you demo it.
- There's a daily CPU limit. Plenty for testing, not for a big crowd.
- No "Always On". That setting needs a paid plan. If we move up to B1 later it's one click
  (plan > **Scale up**), and then Always On can be turned on.

### Settings

Set in each app under **Settings** > **Environment variables** > **App settings**. These are the
real names. The values with passwords live in Azure only, never in the repo.

**API (`ccos-api-caret26`):**

| Setting | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__CCOS` | `Host=ccos-db-caret26.postgres.database.azure.com;Port=5432;Database=ccos;Username=ccosadmin;Password=<in Azure only>;SSL Mode=Require` |
| `Database__MigrateOnStartup` | `true`, so new migrations go up on their own when the API starts |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true`. Azure does the https part in front of the app, so without this the app thinks everyone is on plain http and the https redirect and secure cookies get weird |
| `WEBSITE_WEBDEPLOY_USE_SCM` | `true`, so the GitHub deploys can use the publish profile |

**Website (`ccos-web-caret26`):**

| Setting | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Api__BaseUrl` | `https://ccos-api-caret26.azurewebsites.net/` (keep the `/` on the end) |
| `DataProtection__KeysPath` | `/home/data/keys`, so a restart doesn't sign everyone out |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true`. Azure does the https part in front of the app, so without this the app thinks everyone is on plain http and the https redirect and secure cookies get weird |
| `WEBSITE_WEBDEPLOY_USE_SCM` | `true`, so the GitHub deploys can use the publish profile |

### Is it up?

| Open this | Healthy means |
|---|---|
| <https://ccos-api-caret26.azurewebsites.net/health> | The API is up and can reach the database |
| <https://ccos-web-caret26.azurewebsites.net/health> | The website is up and the API (and so the database) is healthy |

The live site itself: <https://ccos-web-caret26.azurewebsites.net/>

If it says `Unhealthy`, or takes forever the first time, it might just be waking up from an F1
nap. Give it 30 seconds and refresh before you panic.

### Sending new code up by hand

From the repo root in PowerShell, on the branch you want live. Sign in first with `az login`.

```powershell
$rg     = "ccos-rg"
$apiApp = "ccos-api-caret26"
$webApp = "ccos-web-caret26"

# API
dotnet publish backend/CCOS.Api/CCOS.Api.csproj --configuration Release --output publish/api
Compress-Archive -Path publish/api/* -DestinationPath publish/api.zip -Force
az webapp deploy --resource-group $rg --name $apiApp --src-path publish/api.zip --type zip

# Website
dotnet publish frontend/CCOS.Web/CCOS.Web.csproj --configuration Release --output publish/web
Compress-Archive -Path publish/web/* -DestinationPath publish/web.zip -Force
az webapp deploy --resource-group $rg --name $webApp --src-path publish/web.zip --type zip
```

Do the API first so the website never talks to an older API than it expects. Then check both
`/health` pages. The `publish/` folder is just build output, so delete it when you're done
(don't commit it).

### Sending new code up from GitHub

Two workflows do the same thing as above, without the zipping:

- `.github/workflows/deploy-api.yml` sends `backend/CCOS.Api` to `ccos-api-caret26`
- `.github/workflows/deploy-web.yml` sends `frontend/CCOS.Web` to `ccos-web-caret26`

They use the publish profile way (part 5's backup way in the setup doc), since our student
access can't do the one-click identity setup. Each one needs a repo secret holding that app's
publish profile: `AZURE_API_PUBLISH_PROFILE` and `AZURE_WEB_PUBLISH_PROFILE`. Until those
secrets exist, the workflows say so and skip the deploy instead of failing.

`main` is our "in production" branch. A push or merge to `main` that touches an app's code sends
that app live by itself. You can also press **Run workflow** on the Actions tab to send one up any
time. Heads up: the auto deploy only starts once these two workflow files are on `main` (so after
this work merges through `Dev` into `main`).

### Connecting to the database yourself

See [backend/README.md](../backend/README.md#connect-to-the-azure-database), which covers the
firewall rule and the connection string.

## Still to decide

- Whether we stay on Free F1 or move up to B1 before demo day.
- How the club's first officer gets set up on a fresh database. For now it's a bit of SQL by
  hand (part 7 of the setup doc), which Seng or Dylan should look over first. A real screen for
  it could come with the Admins table (CAR-74).
- Whether the demo uses the hosted site with real data, or the Docker setup with sample data.
