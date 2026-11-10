# D2: go-live gate result (hosting review)

Stage 2 of `D2-fresh-deployment-plan.md`. The owner's answers are from 10 Nov 2026.

| # | Question | Answer | Gate |
|---|---|---|---|
| H1 | Plan | **Free** | ⚠ Free plans can be suspended or removed, with no service promise |
| H2 | Automatic database backups | **None** | ❌ The gate needs daily backups, or our own routine instead |
| H3 | Download a backup (.bak) | Yes | ✓ Makes our own routine possible |
| H4 | Valid HTTPS | Yes | ✓ |
| H5 | Server location | **Europe** | ⚠ Health data would be stored outside Egypt. Needs the lawyer check |
| H6 | Panel access | Owner only, **no two-factor sign-in** | ⚠ Anyone with the panel password can read or delete every record |
| H7 | `App_Data` writable and kept | Yes | ✓ |

## Result

**The gate is not passed yet.** Three things stand between the site and real patients:

1. **Backups (H1, H2).** With no automatic backups on a free plan, one fault or a suspended account could lose every record.
2. **Data abroad (H5).** Egypt's Personal Data Protection Law (No. 151 of 2020) treats health data as sensitive, and limits sending personal data outside Egypt. This is not legal advice. Confirm with a lawyer whether storing it in Europe is allowed for the clinic, and on what conditions.
3. **Panel access (H6).** The control panel has no second sign-in factor. The panel password protects every record.

## Options

| | A. Paid hosting with automatic backups (recommended) | B. Stay on the free plan, with a manual routine |
|---|---|---|
| What | Move to a paid plan, at MonsterASP if theirs includes daily automatic backups (ask them), or at another host. Ideally one with two-factor sign-in, and in Egypt or wherever the lawyer approves | Download a `.bak` every day the clinic works. Keep copies encrypted in two places. Test a restore once a month |
| Data at risk | Up to one day, restored by the host | Up to one day, **if the routine is never missed**. If the account is suspended, everything since the last download |
| Effort | Cost of the plan; one move (backup → restore, about 30 minutes) | About 5 minutes a day, done by a person |
| Fits medical records | Yes | Only as a short bridge |

Either way:
- Use a **long, unique panel password**, kept in a password manager.
- Turn on two-factor sign-in for the **email account** that can reset the panel password.

## What can go ahead now

- **Stage 4 (set up for the clinic):** clinic details, hours, prices, users and calendars. A move to another host carries this data along in the backup.
- **Stage 5 (restore test):** download a `.bak` and restore it into a scratch database. It proves the backups work, and it is the same procedure a move to another host uses.
- **No real patients** until the owner picks A or B and the lawyer check is done.
