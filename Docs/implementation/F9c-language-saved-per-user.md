# Slice F9c: language saved per user

Roadmap item **F9** (localisation plumbing), slice c. This slice **closes F9**.

**Claude implemented this slice at the owner's request.** The owner generates and reviews the migration, approves applying it to the dev database, then builds, tests, checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f9c-user-language` (from the working copy, after PR #35 is merged) |
| Behaviour change | 1. **A signed-in user's language choice is saved to their account.** On any browser, signing in brings back their language. 2. **Before signing in**, the switch on the sign-in page changes only that browser, because there's no account yet. 3. **On sign-in, the saved choice wins.** If the user has never chosen, the browser's current language stays (English by default). 4. Changing the language **doesn't** sign the user out elsewhere and isn't written to the audit log, because it's a personal setting, not a security event |
| Database | ⚠ **One schema change: a new nullable column `Users.PreferredLanguage nvarchar(5)`.** Existing rows get `NULL`, which means "not chosen", so English applies. No data is changed and nothing is removed. **This needs your approval** (Step 3) |
| Tests | +12 (about **302**). They cover: a signed-in choice is saved, and a sign-in-page choice isn't; the language is stored in lower case; other languages are refused and nothing changes; an unknown user is refused; the security stamp is untouched; sign-in returns the saved language; new users have none; the switch and the account offer the same languages; the column is optional, with 5 characters |

## Design

| Decision | Choice |
|---|---|
| Where it's stored | `User.PreferredLanguage` (`"en"`, `"ar"` or `NULL`) |
| The list of languages | `Application/Common/UserLanguages` is now the one list. `AppCultures` (Web) uses it, so the switch and the account can't disagree |
| Saving | A new `IUserPreferenceService.SetLanguageAsync`, called by `Language/Set` when the user is signed in. If saving fails, the browser still switches and a warning is logged |
| Restoring | `AuthService.LoginAsync` returns `PreferredLanguage`. `AuthController.Login` writes the culture cookie from it (`AppCultures.AppendCookie`, shared with the switch) |

## Files

| Action | Path |
|---|---|
| Edit | `SafyaClinic.Domain/Identity/User.cs` (+ `PreferredLanguage`) |
| Edit | `SafyaClinic.Infrastructure/Data/Configurations/UserConfiguration.cs` (max length 5) |
| Add (you, Step 3) | `SafyaClinic.Infrastructure/Migrations/<timestamp>_AddUserPreferredLanguage.cs` + `.Designer.cs`; edit the model snapshot |
| Add | `Application/Common/UserLanguages.cs`, `Application/Interfaces/Services/IUserPreferenceService.cs`, `Application/Services/UserPreferenceService.cs` |
| Edit | `Application/DTOs/Auth/AuthDtos.cs` (`LoginResponse.PreferredLanguage`), `Application/Services/AuthService.cs` |
| Edit | `Infrastructure/DependencyInjection/ApplicationServiceExtensions.cs` (registration) |
| Edit | `Web/Controllers/LanguageController.cs` (saves for signed-in users), `Web/Controllers/AuthController.cs` (restores on sign-in), `Web/Localization/AppCultures.cs` (`AppendCookie`; one list) |
| Edit | `tests/.../Web/LocalisationTests.cs`; add `tests/.../Web/PreferredLanguageTests.cs` |

## Rollback

- **Code:** revert the PR's merge commit.
- **Database (dev):**
  ```powershell
  dotnet ef database update <previous migration name> --project SafyaClinic\SafyaClinic.Infrastructure --startup-project SafyaClinic\SafyaClinic.Web
  ```
  The previous name is the one before `AddUserPreferredLanguage` in `dotnet ef migrations list`. This drops the column. Only users' saved language choices are lost.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1: Branch

Merge **PR #35** first. Then:

```powershell
git switch -c chore/f9c-user-language
git status
```

### Step 2: Build and test (before the migration)

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: about **302 passed**. The tests use an in-memory database, so they don't need the migration.

### Step 3: Migration (needs your approval)

**3a. Generate it:**

```powershell
Push-Location SafyaClinic\SafyaClinic.Web
dotnet ef migrations add AddUserPreferredLanguage --project ..\SafyaClinic.Infrastructure
Pop-Location
```

**3b. Review it.** Open the new `…_AddUserPreferredLanguage.cs`:
- `Up` must contain **only** one `AddColumn<string>` named `PreferredLanguage` on table `Users`, with `type: "nvarchar(5)"`, `maxLength: 5` and `nullable: true`.
- `Down` must contain only the matching `DropColumn`.

If it contains anything else (for example changes to other tables), **stop and send me the file**.

**3c. Check the model and migrations agree:**

```powershell
dotnet run --project SafyaClinic\tests\DeploymentChecks -- D:\MyDevWork\migrations-idempotent.sql
```

It must print `Model changes without a migration: False`.

**3d. Back up, then start the app:**

```powershell
powershell -ExecutionPolicy Bypass -File .assessment\backup-db.ps1
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

**Running 3d is your explicit approval** for this change to the **dev** database: one new nullable column, `Users.PreferredLanguage`, with no data changed. In Development the app applies the migration when it starts, as set in S8. If the migration fails, nothing changes; send me the error.

**3e. Read-only check (optional),** in SSMS against `SafyaClinicDb`:

```sql
SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;   -- …_AddUserPreferredLanguage
SELECT COUNT(*) AS Users, COUNT(PreferredLanguage) AS WithLanguage FROM Users;   -- WithLanguage = 0
```

### Step 4: Check the behaviour

With the app running:

1. **As Admin, switch to العربية.** The app is in Arabic.
2. **Sign out.** The sign-in page stays Arabic, because this browser still remembers it.
3. **Open a private window**, which starts in English, and sign in as **Admin**. The app opens **in Arabic**: the saved choice followed you.
4. In that private window, **switch back to English**, sign out, and sign in again in the normal window. It's now **English**.
5. **Sign in as a user who never chose** (e.g. Reception) in a private window. The app is in English.
6. Optional: `SELECT FullName, PreferredLanguage FROM Users WHERE PreferredLanguage IS NOT NULL;` shows Admin with `en` or `ar`.

### Step 5: Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F9c: language saved per user and restored on sign-in (Users.PreferredLanguage)"
git push -u origin chore/f9c-user-language
```

`git status` should include the new migration, its `.Designer.cs` and the changed `SafyaDbContextModelSnapshot.cs`. CI checks that the model and migrations agree and produces `migrations-idempotent.sql` for the hosted servers.

Send me the PR number and the CI result.

## Hosted servers

Nothing to do now. The planned **fresh deployment before the pilot** builds those databases from all migrations, including this one.

## Where we are after this

**F9 (localisation) is complete.** Group 2 has one item left:
- **F4d-3:** a reviewed migration that drops the old `Roles` / `UserRoles` tables and the role seeding.

That migration removes tables, so I'll show you exactly what it removes, with a backup step first. Then the **pilot** (Group 3) starts.
