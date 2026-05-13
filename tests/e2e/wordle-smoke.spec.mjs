import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

test("solves a constrained game without mobile overflow", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Wordle Solver" })).toBeVisible();

  const accessibilityScan = await new AxeBuilder({ page }).analyze();
  expect(accessibilityScan.violations).toEqual([]);

  await page.keyboard.press("Tab");
  await expect(page.getByLabel("Guess")).toBeFocused();

  await page.getByLabel("Guess").fill("crane");

  const feedbackTiles = page.getByRole("button", { name: /Feedback position/ });
  await expect(feedbackTiles).toHaveCount(5);

  for (let index = 0; index < 5; index += 1) {
    await feedbackTiles.nth(index).click();
    await expect(feedbackTiles.nth(index)).toHaveText("B");
  }

  await page.getByLabel("Optional candidate words").fill("crane sloth trace");
  await page.getByRole("button", { name: "Solve" }).click();

  await expect(page.getByText("1", { exact: true })).toBeVisible();
  await expect(page.getByText("sloth", { exact: true })).toBeVisible();
  await expect(page.getByText("crane", { exact: true })).toHaveCount(0);

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
  expect(overflow).toBe(false);
});
