const { defineConfig } = require('@playwright/test');
module.exports = defineConfig({
  testDir: './specs', workers: 1, fullyParallel: false, timeout: 60000,
  expect: { timeout: 10000 }, outputDir: './test-results',
  reporter: [['list'], ['html', { open: 'never' }], ['json', { outputFile: './test-results/results.json' }]],
  use: { baseURL: 'http://127.0.0.1:5187', channel: 'chrome',
    trace: 'retain-on-failure', screenshot: 'only-on-failure', viewport: { width: 1366, height: 900 } }
});
