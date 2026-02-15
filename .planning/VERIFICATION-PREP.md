# Phase 1 Verification Preparation - Changes Summary

**Date:** 2026-02-13
**API Key Generated:** `96af4c29-7407-4eb3-bad4-0c18b6a26116`

## Changes Made

### 1. Blazor App Configuration (Dev3)

**File:** `src/SignaturPortal.Web/appsettings.json`

**Changes:**
- ✓ Updated `RemoteAppUri` from `https://localhost:44300` → `http://localhost:1500`
- ✓ Set `RemoteAppApiKey` to `96af4c29-7407-4eb3-bad4-0c18b6a26116`
- ✓ Updated YARP proxy cluster address to `http://localhost:1500`

**Committed:** `739cbd9` - config: update RemoteAppUri to http://localhost:1500 and set API key

---

### 2. Legacy WebForms App Configuration (Dev3Org/AtlantaSignatur)

**Location:** `Web/MainSite/`

#### 2.1 NuGet Package Installation

**File:** `packages.config`
- ✓ Added `Microsoft.AspNetCore.SystemWebAdapters.FrameworkServices` version 2.3.0
- ✓ Package restored successfully to `AtlantaSignatur/packages/`

**File:** `MainSite.csproj`
- ✓ Added assembly reference to `Microsoft.AspNetCore.SystemWebAdapters.FrameworkServices.dll`

#### 2.2 Web.config Configuration

**Changes:**

1. **AppSettings** (Session/Auth sharing configuration):
   ```xml
   <add key="SystemWebAdapters:ApiKey" value="96af4c29-7407-4eb3-bad4-0c18b6a26116"/>
   <add key="SystemWebAdapters:ConnectedAppUrl" value="http://localhost:5219"/>
   ```

2. **HTTP Module** (IIS Module registration):
   ```xml
   <system.webServer>
     <modules>
       <add name="SystemWebAdapters" type="Microsoft.AspNetCore.SystemWebAdapters.FrameworkServices.SystemWebAdaptersModule, Microsoft.AspNetCore.SystemWebAdapters.FrameworkServices"/>
     </modules>
   </system.webServer>
   ```

3. **Database Connection Strings** (consistency with Blazor app):
   - ✓ Updated all connection strings from `Data Source=LAPTOP920` → `Data Source=.`
   - Database name remains `SignaturAnnoncePortal` (same as Blazor app)

---

## Session Keys Registered

The following session keys are configured for sharing between apps:

| Key | Type | Purpose |
|-----|------|---------|
| `UserId` | int | Core authentication identifier |
| `SiteId` | int | Multi-tenancy site context |
| `ClientId` | int | Multi-tenancy client context |
| `UserName` | string | User display name |
| `UserLanguageId` | int | Localization context |

These keys were audited from the legacy codebase and registered in:
- **Blazor app:** `Program.cs` → `AddJsonSessionSerializer()`
- **Legacy app:** Will be shared via System.Web Adapters module

---

## Verification Steps

### Prerequisites

1. **Database Connection:**
   - Ensure SQL Server is running locally
   - Database `SignaturAnnoncePortal` is accessible at `Data Source=.` (local server)

2. **Application Ports:**
   - Legacy WebForms app: `http://localhost:1500`
   - New Blazor app: `http://localhost:5219` or `https://localhost:7095`

### Step 1: Start Legacy WebForms App

```bash
# Open solution in Visual Studio
C:\Users\it\Documents\Dev\CustomersAndProjects\Signatur\Dev3Org\AtlantaSignatur\AtlantaSignatur\AtlantaSignatur.sln

# Or run with IIS Express
# Ensure it starts on port 1500 (configured in applicationhost.config)
```

**Verify:**
- Legacy app serves at `http://localhost:1500`
- Login page loads successfully
- Can authenticate and access E-recruitment section

### Step 2: Start Blazor App

```bash
cd C:\Users\it\Documents\Dev\CustomersAndProjects\Signatur\Dev3
dotnet run --project src/SignaturPortal.Web
```

**Verify:**
- Blazor app serves at `http://localhost:5001`
- Home page displays "SignaturPortal - Infrastructure Shell"
- Top navigation bar renders with links

### Step 3: Session Sharing End-to-End Test

1. **Login to Legacy App:**
   - Navigate to `http://localhost:1500`
   - Login with valid credentials
   - Verify session is established (check Session["UserId"], Session["SiteId"])

2. **Navigate to Blazor App:**
   - Click link or manually navigate to `http://localhost:5219`
   - Session should persist - user remains authenticated
   - **Expected:** Session variables accessible in Blazor app via `IHttpContextAccessor.HttpContext.Session`

3. **Navigate Back to Legacy:**
   - Click legacy .aspx link in Blazor nav menu (e.g., `/Responsive/AdPortal/Default.aspx`)
   - YARP should proxy the request to `http://localhost:1500`
   - User should see legacy page without re-authentication

### Step 4: YARP Proxy Routing Test

In Blazor app navigation menu:
- Click "Job Ads" link → routes to `/Responsive/AdPortal/Default.aspx`
- Click "Onboarding" link → routes to `/Responsive/OnBoarding/Default.aspx`

**Expected:**
- Browser navigates to legacy WebForms pages via YARP proxy
- No CORS errors
- Session persists across proxy boundary

### Step 5: Repository Database Query Test

Add temporary test code to Blazor app (e.g., in Home.razor.cs):

```csharp
@inject IDbContextFactory<SignaturDbContext> DbContextFactory

protected override async Task OnInitializedAsync()
{
    await using var context = await DbContextFactory.CreateDbContextAsync();
    var clients = await context.Client.Take(5).ToListAsync();
    // Log or display clients to verify database access works
}
```

**Expected:**
- Query returns Client records from SignaturAnnoncePortal database
- No connection errors

### Step 6: Navigation Shell Visual Match

Compare Blazor top nav bar (`http://localhost:5219`) to legacy portal nav:
- **Color:** Dark blue `#1a237e` matches legacy theme
- **Layout:** Horizontal top nav with brand, links, user section
- **Links:** Match legacy portal structure

**Expected:** Visual similarity sufficient for Phase 1 (exact styling deferred to Phase 3 with MudBlazor)

---

## Troubleshooting

### Issue: System.Web Adapters module not loading

**Symptoms:** Legacy app throws error on startup about missing module

**Solution:**
1. Verify `Microsoft.AspNetCore.SystemWebAdapters.FrameworkServices.dll` exists in:
   ```
   C:\Users\it\Documents\Dev\CustomersAndProjects\Signatur\Dev3Org\AtlantaSignatur\AtlantaSignatur\packages\Microsoft.AspNetCore.SystemWebAdapters.FrameworkServices.2.3.0\lib\net48\
   ```
2. Rebuild legacy app in Visual Studio to copy DLL to `bin/`

### Issue: Session not shared between apps

**Symptoms:** Login in legacy app, but Blazor app shows unauthenticated

**Checklist:**
- ✓ API keys match in both apps: `96af4c29-7407-4eb3-bad4-0c18b6a26116`
- ✓ Blazor `RemoteAppUri` points to `http://localhost:1500`
- ✓ Legacy `ConnectedAppUrl` points to `http://localhost:5219`
- ✓ Both apps are running on configured ports
- ✓ Session keys registered in Blazor app match those used in legacy code

### Issue: YARP proxy not routing

**Symptoms:** Clicking legacy links returns 404 or stays on Blazor app

**Checklist:**
- ✓ Legacy app is running at `http://localhost:1500`
- ✓ YARP cluster address is `http://localhost:1500` in `appsettings.json`
- ✓ MapForwarder has `Order = int.MaxValue` (lowest precedence)
- ✓ Legacy route paths are correct (e.g., `/Responsive/AdPortal/Default.aspx`)

### Issue: Database connection errors

**Symptoms:** EF Core throws connection errors, repository queries fail

**Checklist:**
- ✓ SQL Server is running
- ✓ Database `SignaturAnnoncePortal` exists
- ✓ Connection string uses `Data Source=.` (local server)
- ✓ `Trusted_Connection=True` allows Windows authentication
- ✓ Current user has permissions on database

---

## Next Steps After Verification

Once all 4 verification items pass:

1. **Mark Phase 1 as Complete:**
   - Type "approved" in response to verification checkpoint
   - Proceed to Phase 2: Multi-Tenancy & Security Foundation

2. **Phase 2 Will Add:**
   - Tenant isolation via EF Core query filters
   - Role/permission enforcement
   - Session-based tenant context
   - Cross-tenant integration tests

3. **Recommended:**
   - Run `/gsd:plan-phase 2` to create Phase 2 execution plans
   - Use `/clear` first to reset context window

---

## Files Modified Summary

### Blazor App (Dev3)
- `src/SignaturPortal.Web/appsettings.json` ✓

### Legacy App (Dev3Org/AtlantaSignatur)
- `Web/MainSite/packages.config` ✓
- `Web/MainSite/MainSite.csproj` ✓
- `Web/MainSite/Web.config` ✓

### Packages Installed
- `Microsoft.AspNetCore.SystemWebAdapters.FrameworkServices` 2.3.0 ✓

---

**Ready for Verification:** All configuration changes complete. Start both apps and execute verification steps.
