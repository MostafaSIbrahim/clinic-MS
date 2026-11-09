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
    // Let fade-ins finish first (e.g. the toast after an action): a half-faded element reads as low contrast.
    await page.waitForFunction(() => document.getAnimations().every(a => a.playState !== 'running'), null, { timeout: 3000 })
        .catch(() => { /* a looping animation; check anyway */ });
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
    const serious = results.violations.filter(v => v.impact === 'serious' || v.impact === 'critical');
    a11y.push({
        screen, url: new URL(page.url()).pathname, viewport: page.viewportSize(),
        violations: serious.map(v => ({
            id: v.id, impact: v.impact, help: v.help, helpUrl: v.helpUrl,
            nodes: v.nodes.slice(0, 10).map(n => ({ target: n.target.join(' '), summary: n.failureSummary })),
        })),
    });
    // The message names each element and why, so the CI log alone is enough to fix it.
    expect.soft(serious.flatMap(v => v.nodes.slice(0, 5).map(n =>
        `${v.id} (${v.impact}) at ${n.target.join(' ')}: ${(n.failureSummary || '').replace(/\s+/g, ' ').trim()}`)),
        `axe: ${screen}`).toEqual([]);
}

/** No sideways scrolling at the current width (D11: 1366, 1024, 390). */
async function checkNoSidewaysScroll(page, screen) {
    const { scrollWidth, innerWidth } = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth, innerWidth: window.innerWidth,
    }));
    // When it does scroll, name the outermost elements that stick out, so the log says what to fix.
    const culprits = scrollWidth <= innerWidth + 1 ? [] : await page.evaluate(() => {
        const describe = el => el.tagName.toLowerCase() + (el.id ? '#' + el.id : '') +
            [...el.classList].slice(0, 4).map(c => '.' + c).join('');
        const wide = [...document.querySelectorAll('body *')].filter(el => {
            const r = el.getBoundingClientRect();
            return r.width > 0 && r.right > window.innerWidth + 1 && getComputedStyle(el).position !== 'fixed';
        });
        const outer = wide.filter(el => !wide.includes(el.parentElement)).slice(0, 5)
            .map(el => `${describe(el)} (${Math.round(el.getBoundingClientRect().width)}px wide, inside ${describe(el.parentElement)})`);
        // The element reaching furthest right, with its ancestors' widths and overflow settings.
        let far = null;
        for (const el of document.querySelectorAll('body *')) {
            const r = el.getBoundingClientRect();
            if (getComputedStyle(el).position === 'fixed' || r.width === 0) continue;
            if (!far || r.right > far.getBoundingClientRect().right) far = el;
        }
        const chain = [];
        for (let el = far; el && el !== document.body && chain.length < 8; el = el.parentElement) {
            const cs = getComputedStyle(el);
            chain.push(`${describe(el)}[w=${Math.round(el.getBoundingClientRect().width)} right=${Math.round(el.getBoundingClientRect().right)} ox=${cs.overflowX} pos=${cs.position} disp=${cs.display}]`);
        }
        return [...outer, `furthest: ${chain.join(' < ')}`];
    });
    layout.push({ screen, url: new URL(page.url()).pathname, width: innerWidth, scrollWidth, ok: scrollWidth <= innerWidth + 1, culprits });
    expect.soft(scrollWidth, `no sideways scroll: ${screen} at ${innerWidth}px; sticking out: ${culprits.join('; ') || 'n/a'}`)
        .toBeLessThanOrEqual(innerWidth + 1);
}

function recordMetric(name, value) { metrics[name] = value; }

// The screens visited by the flow, kept in a file so the screen-size test can reopen them even after the
// flow test has failed (Playwright starts a new worker after a failure).
const SCREENS_FILE = path.join(RESULTS, 'screens.json');
function saveScreens(screens) {
    fs.mkdirSync(RESULTS, { recursive: true });
    fs.writeFileSync(SCREENS_FILE, JSON.stringify(screens, null, 2));
}
function loadScreens() {
    try { return JSON.parse(fs.readFileSync(SCREENS_FILE, 'utf8')); } catch { return {}; }
}

/** Waits for the page to move on; if it doesn't, fails with the messages the page shows (validation, toasts). */
async function expectMovedTo(page, pattern, what) {
    try {
        await expect(page).toHaveURL(pattern);
    } catch (error) {
        const messages = await page.locator('.validation-summary-errors, .alert-danger, .text-danger, [role="alert"], .toast')
            .allInnerTexts().catch(() => []);
        const shown = messages.map(m => m.trim()).filter(Boolean).join(' | ') || '(no message on the page)';
        throw new Error(`${what}: still on ${new URL(page.url()).pathname}. The page says: ${shown}`);
    }
}

function readJson(name, fallback) {
    try { return JSON.parse(fs.readFileSync(path.join(RESULTS, name), 'utf8')); } catch { return fallback; }
}

function writeResults() {
    fs.mkdirSync(RESULTS, { recursive: true });
    // Each test file run (worker) adds to what earlier ones wrote.
    a11y.unshift(...readJson('axe.json', []).filter(r => !a11y.includes(r)));
    layout.unshift(...readJson('layout.json', []));
    Object.assign(metrics, { ...readJson('metrics.json', {}), ...metrics });
    delete metrics.recordedAt;
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

module.exports = { USERS, signIn, newSession, clickCounter, checkA11y, checkNoSidewaysScroll, recordMetric, expectMovedTo, saveScreens, loadScreens, writeResults };
