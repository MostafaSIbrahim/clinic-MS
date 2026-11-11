// P7: the pilot front-desk visit, end to end, as the real roles:
// register a patient (with an allergy) → book a free time → check in → doctor starts the consultation →
// record → prescription with the allergy warning → complete visit → collect part → supervisor writes off the rest.
// Every screen gets an axe check; the key screens are then checked at 1366, 1024 and 390 px.
const { test, expect } = require('@playwright/test');
const { USERS, newSession, clickCounter, checkA11y, checkNoSidewaysScroll, recordMetric, expectMovedTo, saveScreens, loadScreens, writeResults } = require('../lib/helpers');

// Not 'serial': the screen-size test runs even when the flow reports accessibility findings.

const stamp = Date.now().toString().slice(-6);
const patient = {
    first: 'E2E',
    last: `Pilot ${stamp}`,
    phone: `010${Date.now().toString().slice(-8)}`,
};
const patientName = `${patient.first} ${patient.last}`;
const screens = {};   // name → path, for the screen-size checks

test.afterAll(() => writeResults());
test.afterEach(() => { if (Object.keys(screens).length) saveScreens(screens); });

test('pilot visit: book, check in, consult, prescribe, complete, collect, write off', async ({ browser }) => {
    const reception = await newSession(browser, USERS.reception);
    const page = reception.page;
    const clicks = clickCounter();
    let patientId;

    await test.step('Reception registers the patient (allergic to penicillin)', async () => {
        await page.goto('/Patients/Create');
        await checkA11y(page, 'Register patient');
        await page.locator('input[name="FirstName"]').fill(patient.first);
        await page.locator('input[name="LastName"]').fill(patient.last);
        await page.locator('input[name="Phones[0].PhoneNumber"]').fill(patient.phone);
        await page.locator('textarea[name="Allergies"]').fill('Penicillin');
        await page.getByRole('button', { name: 'Register Patient' }).click();
        await expectMovedTo(page, /\/Patients\/Details\/\d+/, 'Registering the patient');
        patientId = Number(page.url().match(/Details\/(\d+)/)[1]);
        screens['Patient page'] = new URL(page.url()).pathname;
        await checkA11y(page, 'Patient page');
    });

    await test.step('Reception books the first free time with the doctor', async () => {
        clicks.reset();
        await clicks.click(page.getByRole('link', { name: 'Book appointment' }));
        await expect(page.getByText(`Booking for ${patientName}`)).toBeVisible();
        screens['Appointments'] = '/Appointments';
        // A priced visit (800), so there is a balance to collect and write off later.
        const visit = page.locator('#apptType');
        const options = await visit.locator('option').allInnerTexts();
        const consultation = options.find(o => o.startsWith('General Consultation'));
        if (!consultation) throw new Error(`No "General Consultation" visit type. The list has: ${options.join(' | ')}`);
        await visit.selectOption({ label: consultation });
        await clicks.click(page.getByRole('button', { name: 'Show', exact: true }));
        await expect(page.getByText(`Booking for ${patientName}`)).toBeVisible();
        await checkA11y(page, 'Appointments');
        const slot = page.getByRole('link', { name: new RegExp(`^\\d\\d:\\d\\d with ${USERS.doctor.name}$`) }).first();
        await expect(slot, 'a free time today (none are left after about 23:30 clinic time)').toBeVisible();
        await clicks.click(slot);
        await checkA11y(page, 'Book appointment');
        await clicks.click(page.getByRole('button', { name: 'Book', exact: true }));
        await expect(page.locator('.app-day')).toContainText(patientName);
        recordMetric('Clicks to book a known patient (patient page → booked)', clicks.count);
    });

    await test.step('The patient workspace lists the booking on its Visits tab', async () => {
        // M1a: the front desk opens a patient on Visits; the Overview tab holds the details.
        await page.goto(`/Patients/Details/${patientId}`);
        await expect(page.getByRole('link', { name: 'Visits' })).toHaveAttribute('aria-current', 'page');
        await expect(page.locator('main')).toContainText(USERS.doctor.name);
        screens['Patient visits'] = `/Patients/Details/${patientId}?tab=visits`;
        screens['Patient overview'] = `/Patients/Details/${patientId}?tab=overview`;
        await page.getByRole('link', { name: 'Overview' }).click();
        await expect(page.getByRole('link', { name: 'Overview' })).toHaveAttribute('aria-current', 'page');
        await checkA11y(page, 'Patient overview');
    });

    await test.step('Patient arrives: reception checks them in (from the dashboard)', async () => {
        await page.goto('/');
        screens['Dashboard'] = '/';
        await checkA11y(page, 'Dashboard');
        clicks.reset();
        // P8a: today's list on the dashboard has the check-in button.
        await clicks.click(page.getByRole('button', { name: `Check in ${patientName}` }));
        await expect(page.locator('main')).toContainText('Waiting');
        recordMetric('Clicks from "patient arrives" to "checked in" (existing booking, from the dashboard)', clicks.count);
    });

    const doctor = await newSession(browser, USERS.doctor);
    const dpage = doctor.page;
    let recordPath;

    await test.step('Doctor sees the patient waiting and starts the consultation', async () => {
        await dpage.goto('/Reservations/Queue');
        screens['Queue'] = '/Reservations/Queue';
        await checkA11y(dpage, 'My queue');
        const row = dpage.locator('tr', { hasText: patientName });
        await expect(row).toContainText('Waiting');
        await row.getByRole('button', { name: 'Start Consultation' }).click();
        await checkA11y(dpage, 'New medical record');
        await dpage.locator('textarea[name="Diagnosis"]').fill('Acute pharyngitis');
        await dpage.locator('form:has(textarea[name="Diagnosis"]) button[type="submit"]').click();
        await expectMovedTo(dpage, /\/MedicalRecords\/Details\/\d+/, 'Saving the medical record');
        recordPath = new URL(dpage.url()).pathname;
        screens['Consultation workspace'] = recordPath;
        await expect(dpage.locator('.app-patient-header')).toContainText('Penicillin');
        await checkA11y(dpage, 'Consultation workspace');
    });

    await test.step('Doctor prescribes: the allergy warning needs "Prescribe anyway"', async () => {
        await dpage.getByRole('tab', { name: /^Prescriptions/ }).click();
        await dpage.getByRole('link', { name: 'New prescription' }).click();
        await checkA11y(dpage, 'New prescription');
        await dpage.locator('input[name="Items[0].MedicationName"]').fill('Penicillin V 500mg');
        await dpage.keyboard.press('Escape');   // close the medicine suggestions, if any
        const warning = dpage.locator('[data-allergy-confirm]');
        await expect(warning).toBeVisible();
        await expect(warning).toContainText('penicillin');
        await checkA11y(dpage, 'New prescription (allergy warning)');
        await dpage.getByRole('button', { name: 'Save Prescription' }).click();
        await expect(dpage).toHaveURL(/CreatePrescription/);   // blocked until confirmed
        await dpage.locator('[data-allergy-override]').check();
        await dpage.getByRole('button', { name: 'Save Prescription' }).click();
        await expectMovedTo(dpage, /tab=prescriptions/, 'Saving the prescription');
        await expect(dpage.getByRole('tabpanel')).toContainText('Prescription #');
    });

    await test.step('Doctor completes the visit', async () => {
        await dpage.getByRole('button', { name: 'Complete visit' }).click();
        await dpage.locator('[data-confirm-ok]').click();
        await expect(dpage).toHaveURL(/\/Reservations\/Queue/);
        await expect(dpage.locator('main')).not.toContainText(patientName);
    });

    await test.step('The doctor opens the patient on the Records tab, with the allergies in view', async () => {
        // M1b: doctors start on Records; the Lab tab lists lab requests. Both show the patient header.
        await dpage.goto(`/Patients/Details/${patientId}`);
        await expect(dpage.getByRole('link', { name: 'Records', exact: true })).toHaveAttribute('aria-current', 'page');
        await expect(dpage.locator('main')).toContainText('Acute pharyngitis');
        await expect(dpage.locator('.app-patient-header')).toContainText('Penicillin');
        screens['Patient records'] = `/Patients/Details/${patientId}?tab=records`;
        await checkA11y(dpage, 'Patient records');
        await dpage.getByRole('link', { name: 'Lab', exact: true }).click();
        await expect(dpage.getByRole('link', { name: 'Lab', exact: true })).toHaveAttribute('aria-current', 'page');
        screens['Patient lab'] = `/Patients/Details/${patientId}?tab=lab`;
        await checkA11y(dpage, 'Patient lab');
        // The old records page lands on the tab.
        await dpage.goto(`/MedicalRecords/PatientRecords?patientId=${patientId}`);
        await expect(dpage).toHaveURL(new RegExp(`/Patients/Details/${patientId}\\?tab=records`));
    });

    await test.step('Reception collects part of the balance', async () => {
        // M1c: the old payment summary address lands on the patient's Payments tab.
        await page.goto(`/Payments/PatientSummary?patientId=${patientId}`);
        await expect(page).toHaveURL(new RegExp(`/Patients/Details/${patientId}\\?tab=payments`));
        await expect(page.getByRole('link', { name: 'Payments', exact: true })).toHaveAttribute('aria-current', 'page');
        screens['Patient payments'] = `/Patients/Details/${patientId}?tab=payments`;
        screens['Patient nutrition'] = `/Patients/Details/${patientId}?tab=nutrition`;
        await checkA11y(page, 'Patient payments');
        // Icon fonts can add a character to a link's name, so match the end of it.
        const collect = page.locator('main').getByRole('link', { name: /Collect$/ }).first();
        try {
            await collect.waitFor({ timeout: 10_000 });
        } catch {
            const text = (await page.locator('body').innerText()).replace(/\s+/g, ' ').slice(0, 2000);
            throw new Error(`No Collect button for the completed visit. The page says: ${text}`);
        }
        await collect.click();
        await checkA11y(page, 'Collect payment');
        await page.locator('input[name="Amount"]').fill('500');
        await page.getByRole('button', { name: 'Collect Payment' }).click();
        await expect(page.locator('body')).toContainText('Payment of');
        await expect(page.locator('main').getByRole('link', { name: /Write off$/ })).toHaveCount(0);   // the front desk can't write off
    });

    const supervisor = await newSession(browser, USERS.supervisor);
    const spage = supervisor.page;

    await test.step('Supervisor writes off the rest, with a reason', async () => {
        // P8a: a supervisor needs no other group to work the front desk.
        await spage.goto('/');
        await expect(spage.getByRole('link', { name: 'Book appointment' })).toBeVisible();
        await expect(spage.locator('main').getByRole('link', { name: new RegExp(`^${patientName}$`) })).toBeVisible();
        await spage.goto(`/Payments/PatientSummary?patientId=${patientId}`);
        await spage.locator('main').getByRole('link', { name: /Write off$/ }).first().click();
        await checkA11y(spage, 'Write off balance');
        await spage.locator('select[name="Reason"]').selectOption('Discount');
        await spage.getByRole('button', { name: 'Write off', exact: true }).click();
        await expect(spage.locator('body')).toContainText('written off for reservation');
        await expect(spage.locator('main')).toContainText('Written off');
        await expect(spage.locator('main').getByRole('link', { name: /Write off$/ })).toHaveCount(0);   // nothing left to write off
    });

    for (const s of [reception, doctor, supervisor]) await s.context.close();
});

test('pilot screens at 1366, 1024 and 390 px', async ({ browser }) => {
    const visited = loadScreens();
    test.skip(Object.keys(visited).length === 0, 'needs the pilot visit to have run');
    const admin = await newSession(browser, USERS.admin);
    for (const width of [1366, 1024, 390]) {
        await admin.page.setViewportSize({ width, height: width === 390 ? 844 : 768 });
        for (const [name, path] of Object.entries(visited)) {
            await admin.page.goto(path);
            await checkNoSidewaysScroll(admin.page, name);
            await checkA11y(admin.page, `${name} @${width}`);
        }
    }
    await admin.context.close();
});
