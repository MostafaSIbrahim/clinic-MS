# Slice D2a: sign-in keys on disk, and a readiness page

Stage 1 of the approved deployment plan (`D2-fresh-deployment-plan.md`).

**Claude implemented this slice at the owner's request.** The owner runs one command, does the manual check, and pushes.

| | |
|---|---|
| Branch | `feature/d2a-keys-readiness` (from an up-to-date `main`) |
| Behaviour change | **1. Sign-in keys are kept on disk**, in `App_Data/keys`. ASP.NET uses these keys to protect sign-in cookies and form tokens. Until now they could live only in memory on the host, so a restart or an app-pool recycle could sign everyone out and make open forms fail with "bad request". `App_Data` isn't served to browsers, isn't in git, and isn't removed by publishing. **2. A Site readiness page** at `/System/Readiness`, for Admin only (it needs *Manage users*). It checks: environment (Production); HTTPS; database migrations; that sign-in keys and attachments can be written; today's log file; that the first-admin password setting has been removed; that the clinic details on prescriptions aren't the example values; that priced visit types exist; the number of active administrators (1–3 is fine); and the clinic time. Each check shows OK, Review or Problem, with what to do. Nothing on the page changes the site |
| Not in the menu | Open it by its address. It's a deployment tool, not a daily screen |
| Database | None |
| Tests | +6 (the readiness rules: a correctly set-up site passes; missing HTTPS, pending migrations, unwritable folders, no priced visit types or no admin are problems; leftover setup values are flagged; example or missing clinic details are flagged), plus 1 line in the access baseline |

## Files

| Action | Path |
|---|---|
| Edit | `Web/Program.cs` (keys in `App_Data/keys`) |
| Add | `Web/Startup/Readiness.cs`, `Web/Controllers/SystemController.cs`, `Web/Views/System/Readiness.cshtml`, `tests/.../Web/ReadinessTests.cs` |
| Edit | `tests/.../Web/Security/access-matrix.txt` (+ `System.Readiness GET: Admin`) |
| Add | `Docs/deployment/D2-fresh-deployment-plan.md`, this file, `D2b-rebuild-the-live-site.md` |

## Rollback

Revert the PR's merge commit. Keys already written stay in `App_Data/keys` and are harmless.

---

## Your steps

```powershell
git switch main
git pull
.\.assessment\slice.ps1 -Branch feature/d2a-keys-readiness `
  -Message 'D2a: keep sign-in keys in App_Data/keys; Site readiness page for Admin'
```

### Manual check (while the app runs locally)

1. Sign in as Admin and open `/System/Readiness`. Locally you'll see **Review** for Environment (Development) and Clinic details (example values), and **Problem** for HTTPS if you use the http address. That's expected on your PC. The database, keys, attachments, visit types and administrators are **OK**.
2. In the project folder, `SafyaClinic\SafyaClinic.Web\App_Data\keys` now holds a `key-….xml` file.
3. **Keys survive a restart:** sign in, stop the app (Enter in the script, or close its window), start it again (`dotnet run` in `SafyaClinic.Web`), and reload the page. You're still signed in.
4. As Reception, `/System/Readiness` is refused (access denied).

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result. Then follow `D2b-rebuild-the-live-site.md`.
