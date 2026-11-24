// Dev-only: screenshot /dashboard (desktop/phone x light/dark). Usage: node scripts/shots.mjs <outDir>
import { chromium } from "@playwright/test";
const out = process.argv[2] ?? "shots";
const base = process.env.BASE ?? "http://localhost:5174";
const browser = await chromium.launch();
for (const [name, viewport] of [["desktop", { width: 1440, height: 900 }], ["phone", { width: 375, height: 812 }]]) {
    for (const scheme of ["light", "dark"]) {
        const page = await browser.newPage({ viewport, colorScheme: scheme, reducedMotion: "reduce" });
        await page.goto(`${base}/login`);
        await page.locator("input[name=email]").fill("lotte@demo.test");
        await page.locator("input[name=password]").fill("Demo1234!");
        await page.locator("button[type=submit]").click();
        await page.waitForURL(/dashboard/);
        await page.waitForTimeout(800);
        await page.screenshot({ path: `${out}/${name}-${scheme}.png`, fullPage: true });
        await page.close();
    }
}
await browser.close();
