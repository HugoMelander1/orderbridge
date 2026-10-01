import { test, expect } from "@playwright/test";
test("temporary failure recovers with exactly one reservation", async ({
  page,
  request,
}) => {
  await request.post("http://localhost:5080/api/demo", {
    data: { mode: "TemporaryError" },
  });
  try {
    await page.goto("/");
    await page.getByRole("button", { name: "New order" }).click();
    const customer = "E2E " + Date.now();
    await page.getByLabel("Customer name").fill(customer);
    await page.getByLabel("Mechanical keyboard quantity").fill("1");
    await page
      .getByRole("button", { name: "Create order", exact: false })
      .click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByText(/Retry 1 scheduled/)).toBeVisible({
      timeout: 30000,
    });
    await request.post("http://localhost:5080/api/demo", {
      data: { mode: "Normal" },
    });
    await expect(dialog.locator(".badge")).toHaveText("Reserved", {
      timeout: 60000,
    });
    await expect(dialog.getByText("Successful reservations:")).toHaveText(
      "Successful reservations: 1",
    );
  } finally {
    await request.post("http://localhost:5080/api/demo", {
      data: { mode: "Normal" },
    });
  }
});
test("exhausted retries create a dead letter and replay succeeds", async ({
  page,
  request,
}) => {
  await request.post("http://localhost:5080/api/demo", {
    data: { mode: "TemporaryError" },
  });
  try {
    await page.goto("/");
    await page.getByRole("button", { name: "New order" }).click();
    await page.getByLabel("Customer name").fill("Replay " + Date.now());
    await page.getByLabel("USB-C dock quantity").fill("1");
    await page
      .getByRole("button", { name: "Create order", exact: false })
      .click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.locator(".badge")).toHaveText("Failed", {
      timeout: 90000,
    });
    await expect(dialog.getByText(/routed to orders.dead/)).toBeVisible();
    const orderId = (await dialog.locator("p.mono").textContent())!;
    const dead = await request.get(
      "http://localhost:15672/api/queues/%2F/orders.dead",
      {
        headers: {
          Authorization:
            "Basic " +
            Buffer.from("orderbridge:orderbridge-local").toString("base64"),
        },
      },
    );
    expect(dead.ok()).toBeTruthy();
    await expect
      .poll(
        async () => {
          const r = await request.get(
            "http://localhost:15672/api/queues/%2F/orders.dead",
            {
              headers: {
                Authorization:
                  "Basic " +
                  Buffer.from("orderbridge:orderbridge-local").toString(
                    "base64",
                  ),
              },
            },
          );
          return (await r.json()).messages;
        },
        { timeout: 15000 },
      )
      .toBeGreaterThan(0);
    await request.post("http://localhost:5080/api/demo", {
      data: { mode: "Normal" },
    });
    await dialog.getByRole("button", { name: "Replay order" }).click();
    await expect(dialog.locator(".badge")).toHaveText("Reserved", {
      timeout: 30000,
    });
    expect(
      (
        await (
          await request.get("http://localhost:5080/api/reservations/" + orderId)
        ).json()
      ).count,
    ).toBe(1);
  } finally {
    await request.post("http://localhost:5080/api/demo", {
      data: { mode: "Normal" },
    });
  }
});
