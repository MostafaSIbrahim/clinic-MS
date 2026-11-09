// P7: shared helpers: signing in, counting clicks, axe checks and screen-size checks, and the results files.
const fs = require('node:fs');
const path = require('node:path');
const { expect } = require('@playwright/test');
const AxeBuilder = require('@axe-core/playwright').default;

const RESULTS = path.join(__dirname, '..', 'results');

// Test accounts. Passwords come from the environment (generated per CI run), never from the repository.
const password = () => {
    const value = process.env.E2E_USER_PASSWORD;
    if (!value) throw new Error('Set E2E_USER_PASSWORD (at least 10 characters).');
    return value;
};
const USERS = {
    admin: { phone: process.env.E2E_ADMIN_PHONE || '0100', password: () => {
        const value = process.env.E2E_ADMIN_PASSWORD;
        if (!value) throw new Error('Set E2E_ADMIN_PASSWORD (the Seed:AdminPassword the app was started with).');
        return value;
    } },
    reception: { name: 'E2E Reception', phone: '01000000201', group: 'Reception', password },
    doctor: { name: 'E2E Doctor', phone: '01000000202', group: 'Doctors', clinician: 'Doctor', password },
    supervisor: { name: 'E2E Supervisor', phone: '01000000203', group: 'Reception supervisors', password },
};

async function signIn(page, user) {
    await page.goto('/Auth/Login');
    await page.locator('input[name="PhoneNumber"]').fill(user.phone);
    await page.locator('input[name="Password"]').fill(user.password());
    // The language switch is a form too, so pick the sign-in button by its name.
    await page.getByRole('button', { name: 'Sign In', exact: true }).click();
    await page.waitForLoadState('domcontentloaded');
    if (/\/Auth\/Login/i.test(page.url()))
        throw new Error(`Sign-in failed for ${user.phone}. Check the test passwords.`);
    if (/ChangePassword/i.test(page.url()))
        throw new Error(`${user.phone} must change their password first; the E2E accounts should not.`);
}

async function newSession(browser, user) {
    const context = await browser.newContext();
    const page = await context.newPage();
    await signIn(page, user);
    return { context, page };
}

// Counts clicks for the workflow measurements (§13): call reset() at the start of a task, then use click().
function clickCounter() {
    let count = 0;
    return {
        reset() { count = 0; },
        get count() { return count; },
        async click(locator) { count++; await locator.click(); },
    };
}

const a11y = [];
const layout = [];
const metrics = {};

/** axe on the current page: no serious or critical violations (WCAG 2.1 A/AA). Soft, so the flow continues. */
async function checkA11y(page, screen) {
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
    const serious = results.violations.filter(v => v.impact === 'serious' || v.impact === 'critical');
    a11y.push({
        screen, url: new URL(page.url()).pathname, viewport: page.viewportSize(),
        violations: serious.map(v => ({
            id: v.id, impact: v.impact, help: v.help, helpUrl: v.helpUrl,
            nodes: v.nodes.slice(0, 10).map(n => ({ target: n.target.join(' '), summary: n.failureSummary })),
        })),
    });
    expect.soft(serious.map(v => `${v.id} (${v.impact}, ${v.nodes.length} element(s))`), `axe: ${screen}`).toEqual([]);
}

/** No sideways scrolling at the current width (D11: 1366, 1024, 390). */
async function checkNoSidewaysScroll(page, screen) {
    const { scrollWidth, innerWidth } = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth, innerWidth: window.innerWidth,
    }));
    layout.push({ screen, url: new URL(page.url()).pathname, width: innerWidth, scrollWidth, ok: scrollWidth <= innerWidth + 1 });
    expect.soft(scrollWidth, `no sideways scroll: ${screen} at ${innerWidth}px`).toBeLessThanOrEqual(innerWidth + 1);
}

function recordMetric(name, value) { metrics[name] = value; }

function writeResults() {
    fs.mkdirSync(RESULTS, { recursive: true });
    fs.writeFileSync(path.join(RESULTS, 'axe.json'), JSON.stringify(a11y, null, 2));
    fs.writeFileSync(path.join(RESULTS, 'layout.json'), JSON.stringify(layout, null, 2));
    fs.writeFileSync(path.join(RESULTS, 'metrics.json'), JSON.stringify({ ...metrics, recordedAt: new Date().toISOString() }, null, 2));
    const failing = a11y.filter(r => r.violations.length > 0);
    const lines = [
        '# P7 pilot checks',
        '',
        `Screens checked with axe: ${a11y.length}; with serious/critical violations: ${failing.length}.`,
        ...failing.map(r => `- ${r.screen} (${r.url}, ${r.viewport.width}px): ${r.violations.map(v => `${v.id} ×${v.nodes.length}`).join(', ')}`),
        '',
        `Screen-size checks: ${layout.length}; with sideways scrolling: ${layout.filter(l => !l.ok).length}.`,
        ...layout.filter(l => !l.ok).map(l => `- ${l.screen} at ${l.width}px (content ${l.scrollWidth}px wide)`),
        '',
        '## Workflow measurements',
        ...Object.entries(metrics).map(([k, v]) => `- ${k}: ${v}`),
    ];
    fs.writeFileSync(path.join(RESULTS, 'summary.md'), lines.join('\n') + '\n');
}

module.exports = { USERS, signIn, newSession, clickCounter, checkA11y, checkNoSidewaysScroll, recordMetric, writeResults };
