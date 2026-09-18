const fs = require('fs');
const path = require('path');
const { chromium } = require(process.env.PLAYWRIGHT_PACKAGE || 'playwright');
const root = path.resolve(__dirname, '..');
const out = path.join(root, 'datos', 'responsive');
fs.mkdirSync(out, { recursive: true });
const access = fs.readFileSync(path.join(root, 'ACCESOS-LOCALES.txt'), 'utf8');
const passwords = [...access.matchAll(/Contraseña: (.+)/g)].map(x => x[1].trim());
const results = [];
function check(ok, description) { if (!ok) throw new Error(description); results.push(description); }
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  try {
    const page = await browser.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', msg => { if (msg.type() === 'error') errors.push(`${msg.text()} ${msg.location().url}`); });
    await page.goto('http://localhost:5088/Login');
    await page.locator('[name=email]').fill('admin@electricistas.local');
    await page.locator('[name=password]').fill(passwords[0]);
    await Promise.all([page.waitForURL('**/Catalogo'), page.getByRole('button', { name: 'Ingresar', exact: true }).click()]);
    await page.goto('http://localhost:5088/Presupuestos');
    const quote = await page.locator('.cards h2 a').first().getAttribute('href');
    const routes = ['/Catalogo', '/EditarProducto', '/UsuariosCatalogo', '/Presupuestos', quote, '/Cuenta'];
    for (const width of [1440, 1280, 1024, 768, 390, 320]) {
      await page.setViewportSize({ width, height: 900 });
      for (const route of routes) {
        const response = await page.goto('http://localhost:5088' + route);
        check(response.status() === 200, `${width}px ${route}: HTTP 200`);
        const layout = await page.evaluate(() => ({
          viewport: innerWidth,
          width: document.documentElement.scrollWidth,
          tableOverflow: [...document.querySelectorAll('.tablewrap')].some(e => e.scrollWidth > e.clientWidth + 1),
          fieldsOutside: [...document.querySelectorAll('input:not([type=hidden]),select,textarea,main button')].filter(e => {
            const r = e.getBoundingClientRect(); return r.width > 0 && (r.left < -1 || r.right > innerWidth + 1);
          }).length
        }));
        check(layout.width <= layout.viewport + 1 && !layout.tableOverflow && layout.fieldsOutside === 0,
          `${width}px ${route}: sin desbordamiento (${JSON.stringify(layout)})`);
        if (width <= 1100) {
          check(!await page.locator('#app-navigation').isVisible(), `${width}px ${route}: menú cerrado inicialmente`);
          await page.getByRole('button', { name: 'Menú' }).click();
          check(await page.locator('#app-navigation').isVisible(), `${width}px ${route}: menú abre`);
          await page.keyboard.press('Escape');
          check(!await page.locator('#app-navigation').isVisible(), `${width}px ${route}: Escape cierra menú`);
        }
        if ([1440, 390].includes(width)) await page.screenshot({ path: path.join(out, `${width}-${route.split('?')[0].slice(1)}.png`), fullPage: false });
      }
    }
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('http://localhost:5088/Catalogo');
    await page.getByRole('button', { name: 'Menú' }).click();
    await page.locator('nav a[href="/Presupuestos"]').click();
    check(page.url().endsWith('/Presupuestos'), 'Navegación móvil a presupuestos');
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.locator('#app-navigation').waitFor({ state: 'visible' });
    check(await page.locator('#app-navigation').isVisible(), 'Menú visible al ampliar a escritorio');
    await page.setViewportSize({ width: 390, height: 844 });
    await page.locator('#app-navigation').waitFor({ state: 'hidden' });
    check(!await page.locator('#app-navigation').isVisible(), 'Menú se contrae al volver a móvil');
    await page.getByRole('button', { name: 'Menú' }).click();
    await page.getByRole('button', { name: 'Cerrar sesión' }).click();
    check(page.url().includes('/Login'), 'Cerrar sesión');
    await page.locator('[name=email]').fill('asesor@electricistas.local');
    await page.locator('[name=password]').fill(passwords[1]);
    await Promise.all([page.waitForURL('**/Catalogo'), page.getByRole('button', { name: 'Ingresar', exact: true }).click()]);
    check(await page.locator('nav a[href="/UsuariosCatalogo"]').count() === 0 && await page.locator('nav a[href="/EditarProducto"]').count() === 0, 'Asesor sin enlaces de administración');
    check(await page.locator('.catalog-actions .button').count() === 0, 'Asesor sin edición de productos');
    check((await page.request.get('http://localhost:5088/DescargarPdf?id=1')).status() === 200, 'Descarga PDF existente');
    check(errors.length === 0, `Sin errores JavaScript/CSP: ${errors.join('; ')}`);
    const guest = await browser.newContext({ viewport: { width: 320, height: 800 } });
    const login = await guest.newPage();
    await login.goto('http://localhost:5088/Login');
    check(await login.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Login de 320px sin desbordamiento');
    await login.screenshot({ path: path.join(out, '320-Login.png') });
    fs.writeFileSync(path.join(out, 'resultado.json'), JSON.stringify({ passed: results.length, checks: results }, null, 2));
    console.log(`${results.length} comprobaciones correctas. Capturas: ${out}`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
