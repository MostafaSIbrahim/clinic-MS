# H1 — Remove sensitive files from git history

One-off operation, decided by the owner on 2026-10-31 (option 2 in the F10 guide). Records finding **F36**.

| | |
|---|---|
| What | Rewrite the **entire** history of `MostafaSIbrahim/clinic-MS` so a patient lab image and other sensitive files no longer exist in any commit, then force-push |
| ⚠ Irreversible | Every commit ID changes. Old clones, forks and GitHub's pull-request references keep the old history until GitHub Support purges them (Step 8). The local backup from Step 2 is the only way back |
| Code / behaviour | Unchanged — the current files are identical after the rewrite |
| When | **After PR #12 (F10) is merged**, with no other open PRs and no unpushed work. Before starting F2 |
| Time | About 30–45 minutes, plus the GitHub Support request |

## What is removed from history

| Item | Where it appears in history | Why |
|---|---|---|
| Lab image, git object `f981a03959e972fa36b07f9fda03f44b2adf39ba` | `P-Analysis/Hala/HalaGalal-Lipid.jpeg`, and the **same image** as `SafyaClinic/SafyaClinic.Web/wwwroot/uploads/analysis/ace36a35-….jpeg`, `…/5680a03d-….jpeg`, `SafyaClinicPublish/wwwroot/uploads/analysis/ace36a35-….jpeg` | Personal health data in a public repository. Removed by object ID, so every copy goes, whatever its name |
| `P-Analysis/` | whole folder | Patient documents |
| `SafyaClinic/SafyaClinic.Web/wwwroot/uploads/` | whole folder | Uploaded clinical files |
| `SafyaClinicPublish/` | whole folder | Publish output (copies of uploads and old settings with the former JWT key) |
| `safyaclinic.runasp.net-WebDeploy.publishSettings` | repository root | Deployment credentials (password already rotated) |
| `*.docx` | repository root (8 documents + 2 Office lock files) | Internal documents you already removed in S1 |

## Before you start

1. Merge PR #12. Make sure there are **no open PRs** and nothing unpushed (`git status`, `git log origin/main..main` shows nothing).
2. Open GitHub → the repository → **Branches**. All `chore/*` and `docs/assessment-phase-1-2` branches are merged. **`p1.10.4.3.3-pagination`** — check whether it is merged; if you still need it, tell me before continuing (it will be rewritten too, which is fine, but any open work in it must be pushed first).
3. GitHub → **Settings → Branches / Rules**: if `main` is protected against force-push, allow force-push temporarily (re-enable in Step 6).
4. Check for **forks**: repository page → *Forks* count. Forks keep the old history; GitHub Support must be told about them (Step 8).
5. Tell anyone else with a clone to stop pushing until Step 7.

---

## Steps

PowerShell. Work in a new folder **outside** your repository.

### Step 1 — Install git-filter-repo

```powershell
python --version
```

If Python is missing: `winget install Python.Python.3.12`, then open a new PowerShell window.

```powershell
python -m pip install --user git-filter-repo
git filter-repo --version
```

If the last command says *"git: 'filter-repo' is not a git command"*, add pip's script folder to this window's PATH and retry:

```powershell
$scripts = (python -c "import sysconfig; print(sysconfig.get_path('scripts', 'nt_user'))")
$env:Path = "$scripts;$env:Path"
git filter-repo --version
```

### Step 2 — Fresh mirror clone and a backup

```powershell
New-Item -ItemType Directory -Force D:\MyDevWork\history-rewrite | Out-Null
Set-Location D:\MyDevWork\history-rewrite
git clone --mirror https://github.com/MostafaSIbrahim/clinic-MS.git clinic-MS.git
Copy-Item clinic-MS.git clinic-MS-BACKUP-before-rewrite.git -Recurse
```

The backup still **contains the sensitive files** — keep it only on this computer and delete it after Step 9.

### Step 3 — Remove the stale merged branches (fewer refs to rewrite)

In the mirror, delete the merged branches so they are not pushed back:

```powershell
Set-Location D:\MyDevWork\history-rewrite\clinic-MS.git
git for-each-ref --format="%(refname:short)" refs/heads
```

For each branch printed **except `main`** (and except `p1.10.4.3.3-pagination` if you decided to keep it):

```powershell
git branch -D <branch-name>
```

(Their copies on GitHub are deleted in Step 6.)

### Step 4 — Rewrite

Create the two input files:

```powershell
@"
P-Analysis/
SafyaClinic/SafyaClinic.Web/wwwroot/uploads/
SafyaClinicPublish/
safyaclinic.runasp.net-WebDeploy.publishSettings
glob:*.docx
"@ | Set-Content -Encoding ascii ..\purge-paths.txt

"f981a03959e972fa36b07f9fda03f44b2adf39ba" | Set-Content -Encoding ascii ..\purge-blobs.txt
```

Run the rewrite:

```powershell
git filter-repo --invert-paths --paths-from-file ..\purge-paths.txt --strip-blobs-with-ids ..\purge-blobs.txt
```

It prints a summary and finishes with *"Completely finished after … seconds."*

### Step 5 — Verify before pushing

```powershell
git rev-list --all --objects | Select-String -Pattern "P-Analysis|wwwroot/uploads|SafyaClinicPublish|publishSettings|\.docx|f981a039"
git log --oneline -5 main
git ls-tree -r --name-only main | Measure-Object
```

- The first command must print **nothing**.
- `git log` shows your recent commit messages (with new IDs).
- Compare the file count with your current repository: in `D:\MyDevWork\clinic-MS` run `git ls-tree -r --name-only main | Measure-Object` — the two counts must be **equal** (nothing current was removed).

If any check fails, stop and send me the output. Nothing has been pushed yet.

### Step 6 — Force-push

```powershell
git remote add origin https://github.com/MostafaSIbrahim/clinic-MS.git
git push origin --force --all
git push origin --force --tags
```

Then delete the old branches on GitHub (they still hold the old history):

```powershell
git ls-remote --heads origin
```

For each branch listed that you deleted in Step 3:

```powershell
git push origin --delete <branch-name>
```

Re-enable branch protection if you relaxed it.

### Step 7 — Bring your working copy in line (in place)

Your repository folder `D:\MyDevWork\clinic-MS` still has the old history. Reset it in place — untracked and ignored files (`.assessment`, `App_Data`, `.vscode`, `*.user`, your local `P-Analysis` copy) are **not** touched:

```powershell
Set-Location D:\MyDevWork\clinic-MS
git status                      # must be clean apart from ignored files
git switch main
git fetch origin --prune
git reset --hard origin/main
git for-each-ref --format="%(refname:short)" refs/heads | Where-Object { $_ -ne "main" } | ForEach-Object { git branch -D $_ }
git reflog expire --expire=now --all
git gc --prune=now
git rev-list --all --objects | Select-String -Pattern "P-Analysis|f981a039|publishSettings"
```

The last command must print nothing. Build and test once (`dotnet test SafyaClinic\SafyaClinic.sln` → 98 passed).

> Your local `P-Analysis` folder stays on disk (ignored). Consider moving it out of the repository folder.

### Step 8 — Ask GitHub to purge what you cannot

Pull-request references (`refs/pull/1` … `refs/pull/12`) and GitHub's caches still point at the old commits; only GitHub can remove them. Open **https://support.github.com/request** → *Remove sensitive or confidential data* (or "Remove cached views"), and include:

- Repository: `MostafaSIbrahim/clinic-MS`
- "History was rewritten with git-filter-repo to remove personal health data and credentials. Please remove cached views and dereference pull requests #1–#12 so the old commits are garbage-collected."
- The removed paths (the table above) — **not** the files themselves.
- The forks count from *Before you start*, if any.

Until GitHub confirms, the old commits remain reachable by their old IDs.

### Step 9 — Clean up

After GitHub confirms (and you are happy with the rewritten repository for a few days):

```powershell
Remove-Item -Recurse -Force D:\MyDevWork\history-rewrite
```

That deletes the mirror and the backup, which still contain the sensitive files.

Send me a short note when Step 7 is done (and Step 5's outputs if anything looked off); I will verify the repository and continue with **F2**.

---

## Related — the hosted sites

The test and demo sites still run code from before S3, so anything under their `wwwroot/uploads` is **downloadable without signing in**. If this image (or any real result) was uploaded there, delete it from the site's `wwwroot/uploads` in the MonsterASP File Manager now, or deploy S3–S8 (see the S8 guide) soon.

## Acceptance criteria

- [ ] `git rev-list --all --objects` (fresh clone) shows none of the purged paths or the image object.
- [ ] `main` has the same files as before; build and 98 tests green.
- [ ] Only `main` (and any branch you chose to keep) exists on GitHub.
- [ ] GitHub Support request sent; backup deleted after confirmation.
