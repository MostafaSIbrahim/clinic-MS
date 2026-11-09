// P7: the pilot flow end to end, with an accessibility check (axe) on each screen and screen-size checks.
// Runs against a running app (E2E_BASE_URL) with a fresh database; see .github/workflows/ci.yml (job e2e).
const { defineConfig } = require('@playwright/test');

module.exports = defineConfig({
    testDir: './tests',
    timeout: 120_000,
    expect: { timeout: 10_000 },
    fullyParallel: false,
    workers: 1,
    retries: 0,
    outputDir: 'test-results',
    reporter: [
        ['list'],
        ['html', { open: 'never', outputFolder: 'playwright-report' }],
    ],
    globalSetup: require.resolve('./global-setup.js'),
    use: {
        baseURL: process.env.E2E_BASE_URL || 'http://localhost:5070',
        locale: 'en-US',
        timezoneId: 'Africa/Cairo',
        viewport: { width: 1366, height: 768 },
        trace: 'retain-on-failure',
        screenshot: 'only-on-failure',
    },
});
