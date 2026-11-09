# D2 plan: fresh deployment, with real patient data

This is the plan for review. **Nothing has been changed on the servers or in the code.** It follows your answers of 9 Nov:
- **real patients from day one** on the rebuilt site;
- the test site's database is **deleted and recreated in the control panel**;
- the **demo site is rebuilt the same way**.

Because the site will hold patients' medical records, the plan adds a go-live gate (stage 2). The site is rebuilt and fully checked first. **No real patient is entered until that gate passes.**

## Stages

| Stage | What | Who | Changes |
|---|---|---|---|
| **1. D2a: small code slice** | Keep the sign-in encryption keys on disk (`App_Data/keys`). Without this, every app restart on the host signs everyone out and breaks open forms. Add a `Production readiness` page for Admin showing environment, migrations, keys and settings | Me, then your usual script | Code only, no migration |
| **2. Hosting review (go-live gate)** | Answer the checklist below from the MonsterASP panel and plan. Decide on the gaps (backups, HTTPS, location) | You, with me | None |
| **3. D2b: rebuild the test site** | New empty database, full migration script, server settings, publish, checks | You, step by step from my guide | Test site and its database |
| **4. Set up for the clinic** | Real clinic details, opening hours, treatment prices, users and groups, doctors' calendars. Remove the seed password | You (Admin), checklist from me | Data on the test site |
| **5. Restore test** | Back up, restore into a scratch database, confirm it opens. Proves the backups work before they're needed | You, guide from me | Scratch database only |
| **6. Go-live** | Usability session on the real site (test patients), then delete the test patients and start | You | — |
| **7. D2c: demo site** | Same rebuild as stage 3, with its own database and no real data | You, same guide | Demo site and its database |

The **test site URL becomes the clinic's live site**. If you'd rather give the clinic a new name or domain, that's part of stage 2.

## Stage 2: hosting review for medical data

Answer from the control panel, MonsterASP's plan page, or their support. I'll turn the answers into the go-live decision.

| # | Question | Why it matters |
|---|---|---|
| H1 | Which plan is the site on (free or paid)? Are there limits on database size, uptime or support? | Free plans can be suspended or wiped |
| H2 | **Automatic database backups:** are there any, how often, how long are they kept, and can you restore one yourself? | Without daily backups, a fault can lose a day or more of records |
| H3 | Can you **download a backup file** (.bak) to keep a copy off the host? | Protects against losing the hosting account |
| H4 | **HTTPS:** is `https://safyaclinic.runasp.net` served with a valid certificate? Can you use your own domain with a certificate? | Passwords and records must only travel encrypted. The app already redirects to HTTPS and sends HSTS |
| H5 | **Where** are the servers and the database (country)? | Rules on keeping health data, and on sending it outside Egypt |
| H6 | Who can sign in to the control panel and the database? Does the panel offer **two-factor sign-in**? | Panel access means access to every record |
| H7 | Can the app write to `App_Data` (attachments, logs, keys)? Is it kept across publishes and restarts? | Attachments and keys live there |

**Legal (not legal advice):** Egypt's Personal Data Protection Law (No. 151 of 2020) treats health data as sensitive personal data. That can mean registration or licensing, consent, and limits on processing or transferring it abroad. Please confirm your obligations with a lawyer before real patients are entered. I can prepare a short summary of what the system stores, and where, for that conversation.

**Go-live gate:** H2 has daily backups (or we schedule our own download routine); H4 serves HTTPS with a valid certificate; H5 and the legal check are acceptable to you; and the stage 5 restore test passed.

## Stage 3: rebuilding the test site (outline; the step-by-step guide follows after approval)

1. `main` is green. Generate the full idempotent migration script (`DeploymentChecks`).
2. Panel → site83728 → **Stop**.
3. Panel → databases → **delete** the test database, then **create** a new empty one. Note the new server, name, user and password (keep them to yourself).
4. SSMS → connect to the new database → run the script. It must end with the last migration (`WriteOffReasons`).
5. File Manager: delete `wwwroot/uploads` and the contents of `App_Data` (old logs and uploads; there are no real files).
6. Server `appsettings.json` (File Manager, never in git):
   - the new connection string;
   - **real** `ClinicInfo` (name, address, phone, email; these go on prescriptions and slips);
   - `Seed:AdminPassword`: a strong password you choose (at least 10 characters), removed after the first sign-in.
7. Visual Studio → Publish → **site83728-WebDeploy**.
8. Panel → **Start**.
9. Checks:
   - `/health` reports Healthy;
   - you can sign in as admin (phone `0100`);
   - the readiness page is all green;
   - visit types and prices are present;
   - a test upload and delete work;
   - today's log file exists;
   - Arabic and English both work.

**Rollback:** nothing on the test site holds data worth keeping today. If the rebuild fails, stop the site and send me the error. The old database is already gone by design (your choice), so rollback means fixing forward.

## Stage 4: setting up for the clinic (checklist)

1. Change the admin's details (name, phone), remove `Seed:AdminPassword` from the server settings, and restart.
2. Clinics: the real clinic(s), **opening hours**, and agreements with patient sources if used.
3. **Visit types and prices:** review the seeded defaults (General Consultation 800, and so on). Edit them to the clinic's real prices, and deactivate what you don't offer.
4. Users:
   - one account per person, never shared;
   - groups: Doctors, Reception, Reception supervisors, Clinic managers, Administrators (as few as possible);
   - every doctor's **clinician type** set to Doctor.
5. Doctors' calendars, where they're not simply the clinic hours.
6. Language per user if needed (Arabic or English).

## Risks

| Risk | Handling |
|---|---|
| Shared hosting outage or data loss | Stage 2 backups plus a stage 5 restore test before go-live, and an off-host copy (H3) |
| Records exposed in transit | HTTPS check (H4), HSTS already sent |
| Someone else uses a staff account | One account per person, lockout after 5 failed attempts (S5), sign-out of deactivated users (S6), and an audit log of sign-ins and sensitive actions |
| Restarts sign everyone out | Fixed by D2a (keys on disk) |
| Legal obligations for health data | Lawyer check before go-live (stage 2) |
| A later release breaks the flow | `e2e` as a required check, reviewed migration scripts (S8), and the database-migrations guide |

## Next

Reply **"approve D2"**, or name what to change. I'll then send D2a (code) right away, and the step-by-step stage 3 guide with it. You can start the stage 2 hosting review in parallel.
