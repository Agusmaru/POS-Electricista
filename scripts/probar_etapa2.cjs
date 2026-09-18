const fs = require('fs');
const path = require('path');
const { chromium } = require(process.env.PLAYWRIGHT_PACKAGE || 'playwright');
const root = path.resolve(__dirname, '..');
const passwords = [...fs.readFileSync(path.join(root, 'ACCESOS-LOCALES.txt'), 'utf8').matchAll(/Contraseña: (.+)/g)].map(x => x[1].trim());
const fixture = path.join(root, 'datos', 'responsive', '390-Catalogo.png');
const passed = [];
function check(value, name) { if (!value) throw new Error(name); passed.push(name); }
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await page.goto('http://localhost:5088/Login');
    await page.locator('[name=email]').fill('admin@electricistas.local');
    await page.locator('[name=password]').fill(passwords[0]);
    await Promise.all([page.waitForURL('**/Catalogo'), page.getByRole('button', { name: 'Ingresar', exact: true }).click()]);

    const headers = await page.locator('.catalog-table th').allTextContents();
    check(['Producto','Código','Marca','Descripción','Imágenes','Precio','Acciones'].every((x,i) => headers[i].trim() === x), 'Columnas solicitadas');
    check(!await page.locator('.catalog-table').getByText('A consultar', { exact: true }).count(), 'Precios existentes inicializados en cero');
    await page.screenshot({ path: path.join(root, 'datos', 'etapa2-lista.png'), fullPage: false });

    await page.locator('[name=q]').fill('DAISA');
    await Promise.all([page.waitForURL(/q=DAISA/), page.getByRole('button', { name: 'Buscar', exact: true }).click()]);
    check(await page.locator('.catalog-table tbody tr').count() > 0, 'Buscador devuelve resultados por fabricante');
    await page.goto('http://localhost:5088/Catalogo?marca=DAISA');
    const rows = page.locator('.catalog-table tbody tr');
    check(await rows.count() > 0 && await rows.filter({ hasNotText: 'DAISA' }).count() === 0, 'Filtro por fabricante');

    await page.getByRole('link', { name: 'Tarjetas' }).click();
    check(await page.locator('.catalog-mode-tarjetas').count() === 1, 'Cambio a vista de tarjetas');
    check(await page.locator('.catalog-mode-tarjetas .product-image-cell').first().isVisible(), 'Tarjetas muestran el espacio de imagen');
    await page.screenshot({ path: path.join(root, 'datos', 'etapa2-tarjetas.png'), fullPage: false });
    await page.goto('http://localhost:5088/Presupuestos');
    await page.locator('nav a[href="/Catalogo"]').click();
    check(await page.locator('.catalog-mode-tarjetas').count() === 1, 'Preferencia de vista persistente');
    await page.getByRole('link', { name: 'Lista' }).click();
    check(!await page.locator('.catalog-mode-lista .product-image-cell').first().isVisible(), 'Lista oculta las imágenes');
    check(await page.locator('.catalog-mode-lista table').evaluate(e => getComputedStyle(e).display === 'table'), 'Lista conserva formato de tabla en escritorio');

    const targetRow = page.locator('.catalog-table tbody tr').filter({ has: page.locator('.product-image-placeholder') }).first();
    const product = (await targetRow.locator('.product-name strong').textContent()).trim();
    const editUrl = await targetRow.locator('.catalog-actions a').getAttribute('href');
    await page.goto('http://localhost:5088' + editUrl);
    await page.locator('[name=imagen]').setInputFiles(fixture);
    await Promise.all([page.waitForURL('**/Catalogo'), page.getByRole('button', { name: 'Guardar producto' }).click()]);
    const updated = page.locator('.catalog-table tbody tr').filter({ hasText: product }).first();
    check(await updated.locator('img.product-image').count() === 1, 'Imagen visible una sola vez en catálogo');
    const imageUrl = await updated.locator('img.product-image').getAttribute('src');
    check((await page.request.get('http://localhost:5088' + imageUrl)).status() === 200, 'Archivo de imagen accesible');

    await page.goto('http://localhost:5088' + editUrl);
    await page.locator('[name=quitarImagen]').check();
    await Promise.all([page.waitForURL('**/Catalogo'), page.getByRole('button', { name: 'Guardar producto' }).click()]);
    check(await page.locator('.catalog-table tbody tr').filter({ hasText: product }).first().locator('.product-image-placeholder').count() === 1, 'Imagen eliminada desde ABM');
    check((await page.request.get('http://localhost:5088' + imageUrl)).status() === 404, 'Archivo reemplazado eliminado del servidor');

    await page.setViewportSize({ width: 390, height: 844 });
    check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Catálogo móvil sin desbordamiento');
    fs.writeFileSync(path.join(root, 'datos', 'pruebas-etapa2.json'), JSON.stringify({ passed: passed.length, checks: passed }, null, 2));
    console.log(`${passed.length} comprobaciones correctas.`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
