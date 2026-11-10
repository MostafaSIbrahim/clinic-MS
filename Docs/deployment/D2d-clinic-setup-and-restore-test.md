# D2d: set up for the clinic (stage 4) and the restore test (stage 5)

Stages 4 and 5 of `D2-fresh-deployment-plan.md`, on the live site `https://safyaclinic.runasp.net`. **Still no real patients.** The go-live gate (`D2-go-live-gate.md`) is open until you've chosen the hosting option and done the lawyer check.

| | |
|---|---|
| Before you start | D2b done (site running, checks passed); D2a merged (PR #55) |
| You need | The live site as Admin; the MonsterASP panel; SSMS and your local SQL Server (stage 5) |
| Time | Stage 4: 30–60 minutes, depending on the number of staff. Stage 5: about 30 minutes |

Keep passwords to yourself. Each person sets their own password.

---

## Stage 4: set up for the clinic

Do the steps in order. Each line says where in the menu it is.

### 4.1 Your own admin account, then retire `0100`

The site has no screen to rename a user or change a user's phone. So instead of editing the seeded `0100` account, you create your own account and switch `0100` off.

1. **Administration → Users → Create:**
   - your full name and your own mobile number (you'll sign in with it);
   - a strong password;
   - Clinician type: *None*, unless you also see patients.
2. On the **Users** list, add your new account to **Administrators**.
3. Sign out. Sign in with **your** phone and password. Check that **Administration** is in the menu.
4. **Users** → the `0100` account → **Deactivate**.
5. If you didn't do it in D2b Step 10, remove the `"Seed": { … },` block from the server's `appsettings.json` and **restart** the site. The site only uses that setting when the database has no users at all, so removing it is safe.

**Check:** `/System/Readiness` shows **Administrators: 1** and no Review for the seed password.

### 4.2 The clinic

1. **Administration → Clinics** → the seeded clinic → **Edit**. Enter the real name and details. If you have more than one branch, **Create** the others.
2. For each clinic: open it (its name) → **Opening hours** → **Edit opening hours**. Fill in each day's periods (up to two per day, for example 10:00–14:00 and 17:00–21:00). Leave closed days empty.
3. Agreements with patient sources (insurers or companies with a discount) are on the clinic's **Details** page. Add them only if you use them. The sources themselves are in **Administration → Patient Sources**.

**Check:** **Appointments** → today → a doctor shows free times only inside the hours you entered (after 4.4).

### 4.3 Visit types and prices

**Administration → Lookups → Treatment types**:
- Set each visit type to the clinic's **real price**. General Consultation 800 and Follow-up Visit 200 are only the defaults.
- **Deactivate** what you don't offer. Leave the free *nutrition Follow-up* as it is, if you use the nutrition programme.

In **Lookups** you can also review the lab analysis types, injection types and vitamin types used in prescriptions and lab requests.

### 4.4 Staff accounts

For each person, **Users → Create**:
- **one account per person**, never shared;
- their own mobile number;
- a temporary password, which they change at first sign-in (**their name, top right → Change password**).

Then, on the **Users** list:

| Person | Group(s) | Clinician type |
|---|---|---|
| Doctor | Doctors | **Doctor** (they don't appear in booking otherwise) |
| Nutritionist | Nutritionists | **Nutritionist** |
| Front desk | Reception | None |
| Front-desk supervisor (write-offs, doctors' calendars) | Reception + Reception supervisors | None |
| Clinic manager (full payment dashboard, clinic settings) | Reception + Clinic managers | None |
| You | Administrators | None, unless you also see patients |

Keep **Administrators** to 1–3 people. A person who leaves is **deactivated**, never deleted: their name stays on the records they wrote.

Each person can set their language (Arabic or English) with the language switch. It's saved to their account.

### 4.5 Doctors' calendars

**Front desk → Doctor schedules** → each doctor:
- set the days and hours when they see patients, if these differ from the clinic hours;
- add days off or extra days.

Doctors can also maintain their own under **My schedule**.

**Check:** **Appointments** → book → the doctor's free times match their calendar.

### 4.6 A final run-through (test patient only)

Signed in as a front-desk user, then as a doctor:
1. Register a patient named **TEST — delete** with a made-up phone number.
2. Book them with a real doctor at today's first free time, check them in, start and complete the consultation, then collect.
3. Check that the printed prescription shows the real clinic details from `appsettings.json` (`ClinicInfo`).

Test patients are removed before go-live (stage 6).

---

## Stage 5: restore test

This proves that a downloaded backup really contains the database and opens. A backup that has never been restored can't be trusted. You'll restore it on **your own PC**, into a **new scratch database**. Your local `SafyaClinicDb` and the live site aren't touched.

### 5.1 Check the SQL Server versions

A backup only restores on the **same or a newer** SQL Server version. Run this in SSMS on **both** the live database and your local server:

```sql
SELECT SERVERPROPERTY('ProductMajorVersion') AS Major, @@VERSION AS Version;
```

Major 16 is SQL Server 2022, 15 is 2019, 14 is 2017. If your local Major is **lower** than the host's, stop and tell me. The fix is to install SQL Server 2022 Developer or Express locally.

### 5.2 Download a backup

1. Do this right after stage 4, with nobody using the site.
2. Panel → **Databases** → your database → **Backup** (or *Create backup*), then **Download**.
3. You get a `.bak` file, perhaps inside a `.zip`; unzip it.
4. Name it with the date, for example `safyaclinic-2026-11-10.bak`.

### 5.3 Count the rows on the live database

In SSMS, on the **live** database, run this and keep the result (copy it or take a screenshot):

```sql
SELECT t.name AS TableName, SUM(p.rows) AS Rows
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
WHERE t.is_ms_shipped = 0
GROUP BY t.name
ORDER BY t.name;
```

### 5.4 Restore into a scratch database

1. Copy the `.bak` into your SQL Server's backup folder, so SQL Server is allowed to read it. It is usually `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup`; the number and name differ with version and instance.
2. SSMS → connect to your **local** server → right-click **Databases** → **Restore Database…**
3. **Source:** choose **Device** → **…** → **Add** → pick the `.bak` → **OK**.
4. **Destination → Database:** type **`SafyaClinic_RestoreTest`**. Check carefully that it is **not** `SafyaClinicDb`.
5. Page **Files** → tick **Relocate all files to folder**. This stops the restore from reaching for the host's folder paths.
6. Page **Options** → leave *Overwrite the existing database* **unticked**.
7. **OK**. It ends with *Database 'SafyaClinic_RestoreTest' restored successfully*.

If it fails, send me the message. It holds no secrets.

### 5.5 Check the restore

On `SafyaClinic_RestoreTest`, run:
1. the row-count query from 5.3. Every table's count must match the live result;
2. the structure fingerprint query I sent on 9 Nov. It must match the live database's.

### 5.6 Clean up

1. Right-click `SafyaClinic_RestoreTest` → **Delete** → tick *Close existing connections* → **OK**. Check the name before you click.
2. Keep the `.bak` as your first backup.

Once real patients are in the database, every backup file holds medical records. Keep backups only on an **encrypted** disk or in an encrypted archive, never in email or an unencrypted shared folder. The backup routine will spell this out.

---

## Send me

- **Stage 4:** done, or which step gave trouble.
- **Stage 5:**
  - the two Major versions;
  - whether the row counts and fingerprint matched;
  - any error message.
- **Hosting decision:** a paid plan (and MonsterASP's answer on backups), or the manual bridge.

Then comes stage 6, go-live: the usability session, removing the test patients, and the start date. If you chose the bridge, it comes with the daily backup routine.
