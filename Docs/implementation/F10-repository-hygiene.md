# Slice F10 — Repository hygiene

Roadmap item **F10** (Group 2, foundations). Fixes **F31** and R39. First Group 2 slice — small, so the later foundation slices start from a clean tree.

| | |
|---|---|
| Branch | `chore/f10-hygiene` (from an up-to-date `main`, after PR #11 is merged) |
| Behaviour change | **None.** Only unused files, dead code and repository settings change |
| Database | None |
| Size | XS |

## What goes and why

| Item | Why |
|---|---|
| `backup/Views/` (10 files) | Old copies of views; not part of the build |
| `Views/Reservations/Index-old.cshtml` | Superseded view; nothing links to it |
| `wwwroot/css/site-back.css` | Old stylesheet; no page loads it |
| `Application/Services/EgyptianDrugIndex/HttpEgyptianDrugSource.cs` | Not registered or used anywhere |
| `Domain/Enums/ReservationStatusEnum.cs` | Not used; statuses come from the `ReservationStatuses` table |
| `SafyaDbContext.SeedData(...)` and its commented call | Never called (seed data lives in `DbSeeder` and the migrations). Removing it does not change the EF model — the check in Step 4 proves that |
| `.gitatributes` (misspelled) | Git ignores this name, so its line-ending rules never applied. Its content moves to the real `.gitattributes`, which is empty today. All tracked text files are already LF, so nothing gets rewritten |
| `SafyaClinic/.vscode/` | Personal editor settings; untracked and ignored |
| `SafyaClinic.Web.csproj.user` | Personal Visual Studio settings (the file you have been un-staging every slice); untracked and ignored |
| `P-Analysis/` | ⚠ See the note below; untracked and ignored |
| `Docs/Deployment/` → `Docs/deployment/` | S8 created the folder with a capital *D*; the other `Docs` folders and every link use lower case, and GitHub paths are case-sensitive |

> ⚠ **`P-Analysis/Hala/HalaGalal-Lipid.jpeg`** is a lab-result image named after a person, committed since the first version — and the repository is **public**. This slice stops tracking it, but it stays in git history and in any existing clone or fork. If it is (or could be) a real patient's result, removing it from history needs a history rewrite and force-push — a separate, irreversible step I will not include without your explicit decision (see the end of this guide).

Out of scope: `SafyaClinic/CHANGES_README.md` and the older `SafyaClinic/docs/UIUX-*` guides (documentation; kept), the empty `Middleware` folder from S4 (git does not track empty folders; delete it whenever Windows lets you).

## Rollback

Revert the PR's merge commit (files come back; local `.vscode`, `.csproj.user` and `P-Analysis` copies were never deleted from your disk).

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/f10-hygiene
```

### Step 1 — Delete unused files

```powershell
git rm -r backup
git rm SafyaClinic/SafyaClinic.Web/Views/Reservations/Index-old.cshtml
git rm SafyaClinic/SafyaClinic.Web/wwwroot/css/site-back.css
git rm SafyaClinic/SafyaClinic.Application/Services/EgyptianDrugIndex/HttpEgyptianDrugSource.cs
git rm SafyaClinic/SafyaClinic.Domain/Enums/ReservationStatusEnum.cs
```

(If Windows refuses to remove an emptied folder, answer `n`; empty folders do not matter to git.)

### Step 2 — Remove the unused `SeedData` method

In `SafyaClinic/SafyaClinic.Infrastructure/Data/SafyaDbContext.cs`:

1. Delete these two lines at the end of `OnModelCreating`:

   ```csharp
               // Seed data
               //SeedData(modelBuilder);
   ```

2. Delete the whole method `private void SeedData(ModelBuilder modelBuilder) { … }` — from that line to its closing `}` (it ends just before the class's closing `}`). Keep the class and namespace closing braces.

3. If Visual Studio then shows grey (unused) `using` lines at the top of the file, you may delete them; leave them if unsure.

### Step 3 — Git settings and personal files

**3a.** Fix `.gitattributes` (copy the rules from the misspelled file, then delete it):

```powershell
Copy-Item .gitatributes .gitattributes -Force
git rm .gitatributes
git add .gitattributes
git add --renormalize .
git status --short
```

`git status --short` should list only the files from this slice (no long list of "modified" files). If you see dozens of unrelated files marked `M`, stop and send me the list.

**3b.** Append to `.gitignore`:

```gitignore
# Personal editor / IDE settings (F10)
.vscode/
*.user
# Patient documents are never committed (F10)
P-Analysis/
```

**3c.** Stop tracking (files stay on your disk):

```powershell
git rm -r --cached SafyaClinic/.vscode
git rm --cached SafyaClinic/SafyaClinic.Web/SafyaClinic.Web.csproj.user
git rm -r --cached P-Analysis
```

**3d.** Fix the folder case from S8 (two moves, because Windows ignores case-only renames):

```powershell
git mv Docs/Deployment Docs/deployment-tmp
git mv Docs/deployment-tmp Docs/deployment
```

### Step 4 — Verify

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
dotnet run --project SafyaClinic\tests\DeploymentChecks -- $env:TEMP\migrations-idempotent.sql
git ls-files | Select-String -Pattern "backup/|Index-old|site-back|\.vscode|csproj\.user|P-Analysis|gitatributes"
```

Expected: build 0 errors; **Passed: 98**; DeploymentChecks prints `Model changes without a migration: False`; the last command prints nothing.

Quick smoke test: `dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http`, sign in, open Reservations and a prescription (drug search) — both work as before.

### Step 5 — Commit, push, PR

```powershell
git add .gitignore SafyaClinic/SafyaClinic.Infrastructure/Data/SafyaDbContext.cs Docs
git status
git commit -m "F10: remove unused files and dead code; fix .gitattributes; untrack personal settings and patient documents"
git push -u origin chore/f10-hygiene
```

From now on `git status` no longer shows `SafyaClinic.Web.csproj.user`, so the `git reset …csproj.user` line in the guides is no longer needed.

Open the PR and send me the number and CI result. Do not merge before review.

---

## Decision needed: the lab image in git history

The file remains readable in the public repository's history after this slice. Options:

1. **Leave history as is** — acceptable only if the image is synthetic/test data.
2. **Rewrite history** (e.g. `git filter-repo --path P-Analysis --invert-paths`), force-push every branch, ask GitHub Support to purge cached views, and re-clone everywhere. Irreversible; open PRs and forks keep the old history.
3. **Make the repository private** (GitHub settings) — reduces exposure immediately; can be combined with 2.

Tell me which you choose; if 2, I will write a separate, step-by-step guide.

## Acceptance criteria

- [ ] Listed files and dead code are gone; build and 98 tests green; no EF model change.
- [ ] `.gitattributes` holds the line-ending rules; `.gitatributes` is gone; no unrelated files changed.
- [ ] `.vscode/`, `*.user` and `P-Analysis/` are no longer tracked and are ignored.
- [ ] `Docs/deployment/` is lower case.
