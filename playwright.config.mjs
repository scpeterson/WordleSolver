import { defineConfig, devices } from "@playwright/test";

const baseURL = "http://127.0.0.1:5105";
const webServer = process.env.PLAYWRIGHT_SKIP_WEBSERVER
  ? undefined
  : {
      command: `dotnet run --no-build --configuration Release --no-launch-profile --project WordleSolver/WordleSolver.fsproj --urls ${baseURL}`,
      url: baseURL,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    };

export default defineConfig({
  testDir: "./tests/e2e",
  timeout: 30_000,
  expect: {
    timeout: 5_000,
  },
  use: {
    baseURL,
    trace: "on-first-retry",
  },
  webServer,
  projects: [
    {
      name: "chromium-desktop",
      use: { ...devices["Desktop Chrome"], viewport: { width: 1280, height: 800 } },
    },
    {
      name: "firefox-desktop",
      use: { ...devices["Desktop Firefox"], viewport: { width: 1280, height: 800 } },
    },
    {
      name: "webkit-desktop",
      use: { ...devices["Desktop Safari"], viewport: { width: 1280, height: 800 } },
    },
    {
      name: "chromium-tablet",
      use: { ...devices["Desktop Chrome"], viewport: { width: 820, height: 1180 } },
    },
    {
      name: "firefox-tablet",
      use: { ...devices["Desktop Firefox"], viewport: { width: 820, height: 1180 } },
    },
    {
      name: "webkit-tablet",
      use: { ...devices["Desktop Safari"], viewport: { width: 820, height: 1180 } },
    },
    {
      name: "chromium-mobile",
      use: { ...devices["Pixel 7"], viewport: { width: 390, height: 844 } },
    },
    {
      name: "firefox-mobile",
      use: { ...devices["Desktop Firefox"], viewport: { width: 390, height: 844 } },
    },
    {
      name: "webkit-mobile",
      use: { ...devices["iPhone 15"], viewport: { width: 390, height: 844 } },
    },
  ],
});
