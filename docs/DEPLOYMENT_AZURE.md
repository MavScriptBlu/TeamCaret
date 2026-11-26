# Putting CCOS on Azure (step by step)

The Azure follow-along for CAR-98. [DEPLOYMENT.md](./DEPLOYMENT.md) explains the settings, the
database tables and the health checks for any host. This page is the "just tell me what to type"
version for Azure.

Troy confirmed Azure (CAR-87), and we have a subscription now: **SA_Caret_Fall26** (CAR-53).
Anything still marked **TBD** below gets filled in once we know it.

> **It's built now.** The real names, region, plan size and settings we ended up with are in
> [DEPLOYMENT.md, part 5](./DEPLOYMENT.md#5-our-live-setup-on-azure-car-98-car-53). Short version:
> our subscription had no quota for B1 in the US regions we tried, so the plan is **Free F1 in
> West US 2**, and Free F1 can't do `--always-on true` in step 6, so skip that part on F1.

## What we're making

Five things, all in one resource group (a folder in Azure that holds everything for one project):

| Thing | Azure name for it | What it does |
|---|---|---|
| Resource group | Resource group | Holds everything below, so deleting it deletes all of it |
| Database | Azure Database for PostgreSQL flexible server | PostgreSQL 17, same as our Docker setup |
| App Service plan | App Service plan | The computer the two apps run on. One plan holds both apps. |
| API | Web App | `backend/CCOS.Api` |
| Website | Web App | `frontend/CCOS.Web` |

> Heads up: the old backlog says "Azure SQL". That's Microsoft's SQL Server database, and our code
> talks to PostgreSQL, so we want **Azure Database for PostgreSQL** instead. Don't let the portal
> talk you into Azure SQL.

## Before you start

- [ ] Access to the **SA_Caret_Fall26** subscription. Its ID is in the Azure portal under
      **Subscriptions** if you ever need it.
- [ ] Heads up: the portal shows our access as **"Specified access"**. That can mean we only got
      some permissions, not full control. If a create step says `AuthorizationFailed` or "does
      not have authorization to perform action", it's a permissions thing, not you. Send the
      error to Troy and ask for the **Contributor** role on the subscription (or on a resource
      group he makes for us).
- [ ] The Azure CLI installed (`az --version` should print something). Get it from Microsoft's
      "Install the Azure CLI" page.
- [ ] The .NET 10 SDK (you already have it if you can build CCOS).
- [ ] The repo pulled and up to date.
- [ ] A password manager or notes app open, because you'll make a database password.

The commands below are for **PowerShell** on Windows, run from the repo root. Each one is a single
line, so copy the whole line.

## Step 1: Sign in

```powershell
az login
az account show --output table
```

`az login` opens a browser so you can sign in. If it asks you to pick a subscription, pick
**SA_Caret_Fall26**. Then make sure it's the one you're using:

```powershell
az account set --subscription "SA_Caret_Fall26"
az account show --output table
```

The table should say `SA_Caret_Fall26`. If `az account set` can't find it, run
`az account list --output table` to see what your login can see. Not in the list means your
account hasn't been given access yet, so check with Troy.

Want to see what we're allowed to do? This lists our roles:

```powershell
az role assignment list --assignee (az ad signed-in-user show --query id --output tsv) --all --output table
```

**Owner** or **Contributor** means we can do every step here. **Reader** means we can only look,
so ask Troy for Contributor before going further.

## Step 2: Pick names

Paste these in once at the start of your PowerShell window. Every step after this uses them.

```powershell
$rg       = "ccos-rg"
$location = "centralus"
$dbServer = "ccos-db-<something unique>"
$dbAdmin  = "ccosadmin"
$dbPass   = "<make a strong password>"
$plan     = "ccos-plan"
$api      = "ccos-api-<something unique>"
$web      = "ccos-web-<something unique>"
```

- `<something unique>` can be anything with lowercase letters, numbers and dashes, like
  `caret26`. The database and both apps need names nobody else in all of Azure is using.
- The password needs 8+ characters with at least three of: uppercase, lowercase, numbers,
  symbols. Skip `;` and `"` since they break the connection string. Save it somewhere safe.
- **TBD:** the final names and region. `centralus` is close to Wisconsin. If a student account says
  a region isn't allowed (`RequestDisallowedByAzure`), try `eastus2` or `northcentralus`.

If you close the PowerShell window, paste this block in again before you carry on.

## Step 3: Make the resource group

```powershell
az group create --name $rg --location $location
```

## Step 4: Make the database

This one takes about 5 to 10 minutes. Go grab a snack.

```powershell
az postgres flexible-server create --resource-group $rg --name $dbServer --location $location --admin-user $dbAdmin --admin-password $dbPass --tier Burstable --sku-name Standard_B1ms --storage-size 32 --version 17 --public-access 0.0.0.0
```

What the extra bits mean:

- `Burstable` / `Standard_B1ms`: the smallest, cheapest size. Plenty for a club app.
- `--storage-size 32`: 32 GB, the smallest option.
- `--public-access 0.0.0.0`: lets our Azure apps reach the database. It does **not** let random
  people on the internet in.

Then make the CCOS database inside the server:

```powershell
az postgres flexible-server db create --resource-group $rg --server-name $dbServer --database-name ccos
```

Optional: let your own computer reach the database too (handy for running migrations or poking
around with a database tool). Google "what is my ip" and use that number for both spots:

```powershell
az postgres flexible-server firewall-rule create --resource-group $rg --name $dbServer --rule-name blu-laptop --start-ip-address <your ip> --end-ip-address <your ip>
```

## Step 5: Make the App Service plan and the two apps

```powershell
az appservice plan create --resource-group $rg --name $plan --location $location --sku B1 --is-linux
az webapp create --resource-group $rg --plan $plan --name $api --runtime "DOTNETCORE:10.0"
az webapp create --resource-group $rg --plan $plan --name $web --runtime "DOTNETCORE:10.0"
```

- `B1` is the cheapest paid size. The free one (`F1`) falls asleep and has a daily time limit,
  which is no fun in the middle of a demo.
- If `DOTNETCORE:10.0` gets an error, run `az webapp list-runtimes --os linux` and use the .NET 10
  line it shows.

Now grab the real web addresses Azure gave each app (save these too):

```powershell
$apiUrl = "https://" + (az webapp show --resource-group $rg --name $api --query defaultHostName --output tsv) + "/"
$webUrl = "https://" + (az webapp show --resource-group $rg --name $web --query defaultHostName --output tsv) + "/"
$apiUrl
$webUrl
```

## Step 6: Settings

These are the same settings as [DEPLOYMENT.md](./DEPLOYMENT.md), set the Azure way. Azure keeps them
hidden in the app, so the password never goes in the repo.

**API:**

```powershell
$conn = "Host=$dbServer.postgres.database.azure.com;Port=5432;Database=ccos;Username=$dbAdmin;Password=$dbPass;SSL Mode=Require"
az webapp config appsettings set --resource-group $rg --name $api --settings ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_FORWARDEDHEADERS_ENABLED=true Database__MigrateOnStartup=true "ConnectionStrings__CCOS=$conn"
```

**Website:**

```powershell
az webapp config appsettings set --resource-group $rg --name $web --settings ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_FORWARDEDHEADERS_ENABLED=true "Api__BaseUrl=$apiUrl" DataProtection__KeysPath=/home/data/ccos-keys
```

Why these:

- `SSL Mode=Require`: Azure only lets you into the database over an encrypted connection.
- `Database__MigrateOnStartup=true`: the API makes the tables by itself the first time it starts,
  so you don't have to run migrations from your computer.
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`: Azure handles the https part in front of our apps,
  so the apps only see plain http. This tells them the visitor really is on https, so the https
  redirect and the secure sign-in cookie work right.
- `DataProtection__KeysPath=/home/data/ccos-keys`: `/home` is the one folder on App Service that
  survives restarts, so people stay logged in after a restart or a new deploy.

Last bit of setup: https only, always on, and tell Azure how to start each app.

```powershell
az webapp update --resource-group $rg --name $api --https-only true
az webapp update --resource-group $rg --name $web --https-only true
az webapp config set --resource-group $rg --name $api --always-on true --startup-file "dotnet CCOS.Api.dll"
az webapp config set --resource-group $rg --name $web --always-on true --startup-file "dotnet CCOS.Web.dll"
```

## Step 7: Build and send the code up

From the repo root. This builds each app, zips it, and ships it.

**API first:**

```powershell
dotnet publish backend/CCOS.Api/CCOS.Api.csproj --configuration Release --output publish/api
Compress-Archive -Path publish/api/* -DestinationPath publish/api.zip -Force
az webapp deploy --resource-group $rg --name $api --src-path publish/api.zip --type zip
```

**Then the website:**

```powershell
dotnet publish frontend/CCOS.Web/CCOS.Web.csproj --configuration Release --output publish/web
Compress-Archive -Path publish/web/* -DestinationPath publish/web.zip -Force
az webapp deploy --resource-group $rg --name $web --src-path publish/web.zip --type zip
```

- Zip what's **inside** the publish folder, not the folder itself. The `/*` handles that.
- `publish/` is just a build folder, and `.gitignore` already skips it.
- To ship a new version later, run these same three lines again.

## Step 8: Check it works

Give the API a minute after its first deploy, since it's making the tables. Then:

```powershell
curl.exe "$($apiUrl)health"
curl.exe "$($webUrl)health"
```

Both should say `Healthy`. Then open the website and click around:

```powershell
start $webUrl
```

1. Create an account.
2. Log out and log back in.
3. Open Merch and Events. They'll be empty, since there's no sample data outside Development.

Last thing: in the Azure portal open the **website** app, go to **Monitoring > Health check**,
turn it on, and set the path to `/health`. Now Azure keeps an eye on all three pieces for us.

## If something's broken

| What you see | Probably | Try |
|---|---|---|
| `/health` says `Unhealthy` on the API | It can't reach the database | Check the password in `ConnectionStrings__CCOS`, and that step 4 used `--public-access 0.0.0.0` |
| `/health` says `Unhealthy` on the website | It can't reach the API | Check `Api__BaseUrl` ends in `/` and matches `$apiUrl`, and that the API's `/health` is healthy |
| "Application Error" page | The app crashed on start | Watch the logs (below). A missing setting is the usual suspect. |
| Page keeps redirecting forever | The https setting is missing | Check `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` on both apps |
| Everyone gets logged out after a deploy | Keys folder not set | Check `DataProtection__KeysPath` on the website |

Watch an app's logs live (Ctrl+C to stop):

```powershell
az webapp log config --resource-group $rg --name $api --docker-container-logging filesystem
az webapp log tail --resource-group $rg --name $api
```

Swap `$api` for `$web` to watch the website.

## What it costs

Rough guess, so check the Azure pricing pages before you trust it: the B1 plan is around
$13 a month and the B1ms database around $15 to $20 a month with storage. Azure for Students comes
with credit that covers this for a semester. **TBD:** who pays for SA_Caret_Fall26 and how much
credit it has. It showed $0.00 spent when we got it. Check **Cost Management** in the portal now
and then so nothing sneaks up on us.

To stop the meter when we're done with it, delete everything in one go. This can't be undone and
wipes the database too:

```powershell
az group delete --name $rg
```

## Still to decide

- Whether we stay on Free F1 or move up to B1 before demo day.
- How the club's first officer gets set up on a fresh database. Same open item as
  [DEPLOYMENT.md](./DEPLOYMENT.md).
- Which branch is the live one. GitHub deploys are set up (`deploy-api.yml` and `deploy-web.yml`)
  and use `BJ/Azure` for now. Step 7 by hand still works too.
