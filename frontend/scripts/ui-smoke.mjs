import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
import { mkdir } from "node:fs/promises";
const browser = await chromium.launch();
try {
  const page = await browser.newPage({
    viewport: { width: 1440, height: 1000 },
  });
  await page.goto("http://localhost:5173");
  await page
    .getByRole("heading", { name: "Every order. Connected." })
    .waitFor();
  await page.getByRole("alert").waitFor();
  await mkdir("../.local/ui", { recursive: true });
  await page.screenshot({ path: "../.local/ui/desktop.png", fullPage: true });
  await page.getByRole("button", { name: "New order" }).click();
  await page.getByRole("dialog").waitFor();
  await page.keyboard.press("Escape");
  assert.equal(await page.getByRole("dialog").count(), 0);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: "../.local/ui/mobile.png", fullPage: true });
  assert.ok(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
    "Mobile page must not overflow horizontally",
  );
  console.log(
    "Desktop/mobile layout and dialog keyboard smoke checks passed. API is offline; no integration results are simulated.",
  );
} finally {
  await browser.close();
}
