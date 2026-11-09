// P7: creates the test accounts (front desk, doctor, supervisor) through the Users screen, as the seeded admin.
// Safe to run again on the same database: an account that already exists is left as it is.
const { chromium } = require('@playwright/test');
const { USERS, signIn } = require('./lib/helpers');

module.exports = async (config) => {
    const baseURL = config.projects[0].use.baseURL;
    const browser = await chromium.launch();
    const page = await browser.newPage({ baseURL });
    try {
        await signIn(page, USERS.admin);
        for (const user of [USERS.reception, USERS.doctor, USERS.supervisor]) {
            await page.goto('/Users/Create');
            await page.locator('input[name="FullName"]').fill(user.name);
            await page.locator('input[name="PhoneNumber"]').fill(user.phone);
            await page.locator('input[name="Password"]').fill(user.password());
            await page.locator('input[name="ConfirmPassword"]').fill(user.password());
            if (user.clinician)
                await page.locator('select[name="ClinicianType"]').selectOption({ label: user.clinician });
            await page.getByLabel(user.group, { exact: true }).check();
            await page.getByRole('button', { name: 'Create User' }).click();
            await page.waitForLoadState('domcontentloaded');
            if (/\/Users\/Create/i.test(page.url())) {
                const text = await page.locator('main').innerText();
                if (!/already/i.test(text))
                    throw new Error(`Could not create ${user.name}:\n${text.slice(0, 800)}`);
            }
        }
    } finally {
        await browser.close();
    }
};
