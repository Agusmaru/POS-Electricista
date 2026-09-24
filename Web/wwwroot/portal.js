(() => {
    const toggles = document.querySelectorAll('[data-theme-toggle]');
    const form = document.getElementById('theme-form');
    if (!toggles.length || !form || !window.fetch || !window.FormData) return;
    const root = document.documentElement;
    const applyTheme = theme => {
        const dark = theme === 'dark';
        root.dataset.theme = dark ? 'dark' : 'light';
        toggles.forEach(toggle => {
            toggle.setAttribute('aria-checked', String(dark));
            toggle.setAttribute('aria-label', dark ? 'Activar modo claro' : 'Activar modo oscuro');
            toggle.value = dark ? 'light' : 'dark';
            const icon = toggle.querySelector('.theme-icon');
            if (icon) icon.textContent = dark ? '☀' : '☾';
        });
    };
    toggles.forEach(toggle => toggle.addEventListener('click', async event => {
        event.preventDefault();
        const theme = root.dataset.theme === 'dark' ? 'light' : 'dark';
        toggles.forEach(item => item.disabled = true);
        try {
            const data = new FormData(form);
            data.set('tema', theme);
            const response = await fetch(form.action, { method: 'POST', body: data, headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' } });
            const result = await response.json();
            if (!response.ok || !result.ok) throw new Error(result.error || 'No se pudo cambiar el tema.');
            applyTheme(result.tema);
        } catch (error) {
            window.alert(error.message || 'No se pudo cambiar el tema.');
        } finally {
            toggles.forEach(item => item.disabled = false);
        }
    }));
    applyTheme(root.dataset.theme);
})();

(() => {
    const toggle = document.querySelector('.menu-toggle');
    const navigation = document.getElementById('app-navigation');
    if (!toggle || !navigation) return;
    const mobile = window.matchMedia('(max-width: 1100px)');
    document.documentElement.classList.add('js');
    const setOpen = (open, restoreFocus = false) => {
        navigation.hidden = mobile.matches && !open;
        toggle.setAttribute('aria-expanded', String(open));
        if (restoreFocus) toggle.focus();
    };
    const sync = () => {
        const focusInside = navigation.contains(document.activeElement);
        setOpen(!mobile.matches, mobile.matches && focusInside);
    };
    toggle.addEventListener('click', () => setOpen(toggle.getAttribute('aria-expanded') !== 'true'));
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && mobile.matches && !navigation.hidden) setOpen(false, true);
    });
    document.addEventListener('click', event => {
        if (mobile.matches && !navigation.hidden && !event.target.closest('.app-header')) setOpen(false);
    });
    mobile.addEventListener('change', sync);
    sync();
})();

(() => {
    const buttons = document.querySelectorAll('[data-share-budget]');
    const dialog = document.querySelector('[data-share-dialog]');
    if (!buttons.length || !dialog) return;
    const whatsapp = dialog.querySelector('[data-share-whatsapp]');
    const email = dialog.querySelector('[data-share-email]');
    const download = (blob, filename) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url; link.download = filename; document.body.appendChild(link); link.click(); link.remove();
        window.setTimeout(() => URL.revokeObjectURL(url), 30000);
    };
    buttons.forEach(button => button.addEventListener('click', async () => {
        const originalText = button.textContent;
        button.disabled = true; button.textContent = 'Preparando PDF…';
        try {
            const id = button.dataset.budgetId, name = button.dataset.budgetName;
            const response = await fetch(`/DescargarPdf?id=${encodeURIComponent(id)}`);
            if (!response.ok) throw new Error('No se pudo generar el PDF.');
            const blob = await response.blob();
            const filename = `orden-presupuesto-${String(id).padStart(6, '0')}.pdf`;
            download(blob, filename);
            const file = new File([blob], filename, { type: 'application/pdf' });
            const text = `Te comparto el presupuesto ${name} (Nº ${String(id).padStart(6, '0')}).`;
            if (navigator.share && navigator.canShare && navigator.canShare({ files: [file] })) {
                try { await navigator.share({ title: `Presupuesto ${name}`, text, files: [file] }); }
                catch (error) { if (error.name !== 'AbortError') throw error; }
            } else {
                whatsapp.href = `https://wa.me/?text=${encodeURIComponent(text + ' Adjuntá el PDF descargado en este mensaje.')}`;
                email.href = `mailto:?subject=${encodeURIComponent('Presupuesto ' + name)}&body=${encodeURIComponent(text + '\n\nAdjuntá el PDF descargado a este correo.')}`;
                dialog.showModal();
            }
        } catch (error) {
            window.alert(error.message || 'No se pudo preparar el presupuesto.');
        } finally {
            button.disabled = false; button.textContent = originalText;
        }
    }));
})();

(() => {
    const results = document.getElementById('catalog-results');
    if (!results || !window.fetch || !window.FormData) return;
    const feedback = document.getElementById('catalog-feedback');
    const showFeedback = (message, error = false) => {
        feedback.textContent = message;
        feedback.classList.toggle('error', error);
        feedback.hidden = false;
    };
    results.addEventListener('submit', async event => {
        const form = event.target.closest('.catalog-add');
        if (!form || event.defaultPrevented) return;
        event.preventDefault();
        const button = form.querySelector('button[type="submit"],button:not([type])');
        const originalText = button.textContent;
        button.disabled = true;
        button.textContent = 'Agregando…';
        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
            });
            const data = await response.json();
            if (!response.ok || !data.ok) throw new Error(data.error || 'No se pudo agregar el producto.');
            document.querySelectorAll('[data-cart-count]').forEach(badge => {
                badge.textContent = data.cartCount;
                badge.setAttribute('aria-label', `${data.cartCount} productos en el carrito`);
                badge.classList.toggle('is-empty', data.cartCount === 0);
            });
            form.querySelector('[name="cantidad"]').value = '1';
            const color = form.querySelector('[name="color"]');
            if (color) color.value = '';
            button.textContent = 'Agregado ✓';
            showFeedback(data.message);
            window.setTimeout(() => { button.textContent = originalText; }, 1400);
        } catch (error) {
            button.textContent = originalText;
            showFeedback(error.message || 'No se pudo agregar el producto.', true);
        } finally {
            button.disabled = false;
        }
    });
})();

(() => {
    const results = document.querySelector('[data-catalog-dynamic]');
    const filters = document.querySelector('[data-catalog-filters]');
    const body = results?.querySelector('[data-catalog-body]');
    const pagination = results?.querySelector('[data-catalog-pagination]');
    const count = document.querySelector('[data-catalog-count]');
    if (!results || !filters || !body || !pagination || !count || !window.fetch) return;

    const endpoint = results.dataset.endpoint;
    const expectedVersion = results.dataset.version || '';
    const admin = results.dataset.admin === 'true';
    const pageSize = Number.parseInt(results.dataset.pageSize || '30', 10) || 30;
    const initialPage = Number.parseInt(results.dataset.page || '1', 10) || 1;
    const csrfInput = results.querySelector('[data-catalog-token] input[name="csrf"], [data-catalog-token] input[name="__RequestVerificationToken"]');
    const csrf = csrfInput?.value || '';
    const csrfName = csrfInput?.name || 'csrf';
    const feedback = document.getElementById('catalog-feedback');
    const advancedSearch = document.querySelector('[data-advanced-search]');
    const advancedToggle = advancedSearch?.querySelector('[data-advanced-toggle]');
    const advancedPanel = advancedSearch?.querySelector('[data-advanced-panel]');
    const advancedRows = advancedSearch?.querySelector('[data-advanced-rules]');
    const advancedAdd = advancedSearch?.querySelector('[data-advanced-add]');
    const advancedClear = advancedSearch?.querySelector('[data-advanced-clear]');
    const advancedCount = advancedSearch?.querySelector('[data-advanced-count]');
    const advancedLimit = advancedSearch?.querySelector('[data-advanced-limit]');
    const view = filters.elements.namedItem('vista')?.value === 'tarjetas' ? 'tarjetas' : 'lista';
    const storageKey = `catalogo:${admin ? 'admin' : 'asesor'}:${expectedVersion}`;
    const rulesStorageKey = `catalogo-reglas:${admin ? 'admin' : 'asesor'}`;
    let products = [];
    let page = initialPage;
    let debounceTimer;

    const text = value => value == null ? '' : String(value);
    const normalize = value => text(value).normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('es-AR').trim();
    const make = (tag, className, content) => {
        const element = document.createElement(tag);
        if (className) element.className = className;
        if (content !== undefined) element.textContent = content;
        return element;
    };
    const appendSmall = (parent, className, content) => parent.append(make('small', className, content));
    const advancedFields = {
        producto: { label: 'Producto', kind: 'text' },
        codigo: { label: 'Código', kind: 'text' },
        marca: { label: 'Marca', kind: 'text' },
        descripcion: { label: 'Descripción', kind: 'text' },
        tipo: { label: 'Tipo', kind: 'text' },
        estado: { label: 'Estado', kind: 'state' },
        imagen: { label: 'Imagen', kind: 'image' }
    };
    const textOperators = [
        ['contains', 'contiene'], ['not_contains', 'no contiene'], ['equals', 'es'], ['not_equals', 'no es'],
        ['starts', 'empieza con'], ['ends', 'termina con'], ['empty', 'está vacío'], ['not_empty', 'no está vacío']
    ];
    const enumOperators = [['equals', 'es'], ['not_equals', 'no es']];
    const joins = [['and', 'Y'], ['or', 'O'], ['and_not', 'Y NO'], ['or_not', 'O NO']];
    const needsValue = operator => !['empty', 'not_empty'].includes(operator);
    const fieldValue = (product, field) => {
        switch (field) {
            case 'producto': return product.nombre;
            case 'codigo': return `${text(product.codigoCatalogo)} ${text(product.codigoLocal)}`.trim();
            case 'marca': return product.marca;
            case 'descripcion': return product.descripcion;
            case 'tipo': return product.tipo;
            case 'estado': return product.activo ? 'activo' : 'inactivo';
            case 'imagen': return product.imagen ? 'con' : 'sin';
            default: return '';
        }
    };
    const readAdvancedRules = () => advancedRows ? Array.from(advancedRows.querySelectorAll('[data-advanced-rule]')).map(row => ({
        join: row.querySelector('[data-rule-join]')?.value || 'and',
        field: row.querySelector('[data-rule-field]')?.value || 'producto',
        operator: row.querySelector('[data-rule-operator]')?.value || 'contains',
        value: text(row.querySelector('[data-rule-value]')?.value).trim()
    })) : [];
    const validAdvancedRules = () => readAdvancedRules().filter(rule => advancedFields[rule.field]
        && (needsValue(rule.operator) ? rule.value !== '' : true));
    const evaluateAdvancedRule = (product, rule) => {
        const actual = normalize(fieldValue(product, rule.field));
        const expected = normalize(rule.value);
        switch (rule.operator) {
            case 'contains': return actual.includes(expected);
            case 'not_contains': return !actual.includes(expected);
            case 'equals': return actual === expected;
            case 'not_equals': return actual !== expected;
            case 'starts': return actual.startsWith(expected);
            case 'ends': return actual.endsWith(expected);
            case 'empty': return actual === '';
            case 'not_empty': return actual !== '';
            default: return true;
        }
    };
    const matchesAdvancedRules = (product, rules) => {
        if (!rules.length) return true;
        let matches = evaluateAdvancedRule(product, rules[0]);
        for (let index = 1; index < rules.length; index += 1) {
            const current = evaluateAdvancedRule(product, rules[index]);
            if (rules[index].join === 'or') matches = matches || current;
            else if (rules[index].join === 'and_not') matches = matches && !current;
            else if (rules[index].join === 'or_not') matches = matches || !current;
            else matches = matches && current;
        }
        return matches;
    };
    const currentFilters = () => ({
        q: text(filters.elements.namedItem('q')?.value).trim(),
        marca: text(filters.elements.namedItem('marca')?.value).trim(),
        tipo: text(filters.elements.namedItem('tipo')?.value).trim(),
        inactivos: admin && filters.elements.namedItem('inactivos')?.value === 'true'
    });
    const filteredProducts = () => {
        const values = currentFilters();
        const query = normalize(values.q);
        const brand = normalize(values.marca);
        const type = normalize(values.tipo);
        const rules = validAdvancedRules();
        return products.filter(product => {
            if (!values.inactivos && !product.activo) return false;
            if (brand && normalize(product.marca) !== brand) return false;
            if (type && normalize(product.tipo) !== type) return false;
            const matchesSimpleSearch = !query || normalize([
                product.nombre, product.descripcion, product.marca, product.categoria,
                product.codigoCatalogo, product.codigoLocal, product.tipo
            ].join(' ')).includes(query);
            return matchesSimpleSearch && matchesAdvancedRules(product, rules);
        });
    };
    const updateUrl = () => {
        const values = currentFilters();
        const params = new URLSearchParams();
        if (values.q) params.set('q', values.q);
        if (values.marca) params.set('marca', values.marca);
        if (values.tipo) params.set('tipo', values.tipo);
        if (values.inactivos) params.set('inactivos', 'true');
        if (view !== 'lista') params.set('vista', view);
        if (page > 1) params.set('pagina', String(page));
        const query = params.toString();
        history.replaceState(null, '', `/Catalogo${query ? `?${query}` : ''}`);
        document.querySelectorAll('.view-switch a').forEach(link => {
            const linkUrl = new URL(link.href, location.origin);
            const targetView = linkUrl.searchParams.get('vista') || 'lista';
            const target = new URLSearchParams(params);
            target.set('vista', targetView);
            target.delete('pagina');
            link.href = `/Catalogo?${target}`;
        });
    };
    const fillSelect = (select, options, selected) => {
        select.replaceChildren();
        options.forEach(([value, label]) => {
            const option = document.createElement('option');
            option.value = value; option.textContent = label; option.selected = value === selected;
            select.append(option);
        });
    };
    const refreshRuleEditor = (row, selectedOperator, selectedValue) => {
        const field = row.querySelector('[data-rule-field]').value;
        const fieldKind = advancedFields[field]?.kind || 'text';
        const operator = row.querySelector('[data-rule-operator]');
        const previousOperator = selectedOperator || operator.value;
        const operators = fieldKind === 'text' ? textOperators : enumOperators;
        fillSelect(operator, operators, operators.some(item => item[0] === previousOperator) ? previousOperator : operators[0][0]);

        const valueField = row.querySelector('[data-rule-value-field]');
        const previousValue = selectedValue ?? row.querySelector('[data-rule-value]')?.value ?? '';
        valueField.replaceChildren(make('span', '', 'Valor'));
        valueField.hidden = !needsValue(operator.value);
        if (valueField.hidden) return;
        if (fieldKind === 'state' || fieldKind === 'image') {
            const select = document.createElement('select');
            select.dataset.ruleValue = '';
            select.setAttribute('aria-label', `Valor para ${advancedFields[field].label}`);
            const options = fieldKind === 'state'
                ? [['activo', 'Activo'], ['inactivo', 'Inactivo']]
                : [['con', 'Con imagen'], ['sin', 'Sin imagen']];
            fillSelect(select, options, options.some(item => item[0] === previousValue) ? previousValue : options[0][0]);
            valueField.append(select);
        } else {
            const input = document.createElement('input');
            input.type = 'search'; input.maxLength = 250; input.value = previousValue;
            input.placeholder = 'Escribí un valor'; input.dataset.ruleValue = '';
            input.setAttribute('aria-label', `Valor para ${advancedFields[field].label}`);
            valueField.append(input);
        }
    };
    const reindexAdvancedRules = () => {
        if (!advancedRows) return;
        const rows = Array.from(advancedRows.querySelectorAll('[data-advanced-rule]'));
        rows.forEach((row, index) => {
            row.dataset.ruleIndex = String(index);
            const joinField = row.querySelector('[data-rule-join-field]');
            const join = row.querySelector('[data-rule-join]');
            joinField.classList.toggle('is-first', index === 0);
            joinField.setAttribute('aria-hidden', String(index === 0));
            join.disabled = index === 0;
            if (index === 0) join.value = 'and';
            row.querySelector('[data-rule-remove]').setAttribute('aria-label', `Eliminar criterio ${index + 1}`);
        });
        if (advancedAdd) advancedAdd.disabled = rows.length >= 10;
        if (advancedLimit) advancedLimit.textContent = rows.length >= 10
            ? 'Alcanzaste el máximo de 10 criterios.'
            : `Podés agregar ${10 - rows.length} criterio${10 - rows.length === 1 ? '' : 's'} más.`;
    };
    const addAdvancedRule = (seed = {}) => {
        if (!advancedRows || advancedRows.children.length >= 10) return null;
        const row = make('div', 'advanced-rule');
        row.dataset.advancedRule = '';
        const joinField = make('label', 'advanced-rule-field advanced-rule-join');
        joinField.dataset.ruleJoinField = '';
        joinField.append(make('span', '', 'Unión'));
        const join = document.createElement('select'); join.dataset.ruleJoin = '';
        fillSelect(join, joins, seed.join || 'and'); joinField.append(join);

        const fieldLabel = make('label', 'advanced-rule-field');
        fieldLabel.append(make('span', '', 'Campo'));
        const field = document.createElement('select'); field.dataset.ruleField = '';
        fillSelect(field, Object.entries(advancedFields).map(([value, definition]) => [value, definition.label]), seed.field || 'producto');
        fieldLabel.append(field);

        const operatorLabel = make('label', 'advanced-rule-field');
        operatorLabel.append(make('span', '', 'Condición'));
        const operator = document.createElement('select'); operator.dataset.ruleOperator = '';
        operatorLabel.append(operator);

        const valueLabel = make('label', 'advanced-rule-field advanced-rule-value');
        valueLabel.dataset.ruleValueField = '';
        const remove = make('button', 'advanced-rule-remove', '×');
        remove.type = 'button'; remove.dataset.ruleRemove = ''; remove.title = 'Eliminar criterio';
        row.append(joinField, fieldLabel, operatorLabel, valueLabel, remove);
        advancedRows.append(row);
        refreshRuleEditor(row, seed.operator, seed.value);
        reindexAdvancedRules();
        return row;
    };
    const updateAdvancedCount = () => {
        if (!advancedCount) return;
        const total = validAdvancedRules().length;
        advancedCount.textContent = `${total}`;
        advancedCount.hidden = total === 0;
        advancedToggle?.classList.toggle('has-rules', total > 0);
    };
    const saveAdvancedRules = () => {
        try {
            const rules = readAdvancedRules();
            if (rules.length) sessionStorage.setItem(rulesStorageKey, JSON.stringify(rules));
            else sessionStorage.removeItem(rulesStorageKey);
        } catch { /* Las reglas siguen funcionando aunque el navegador bloquee el almacenamiento. */ }
    };
    const applyAdvancedChange = () => {
        reindexAdvancedRules(); updateAdvancedCount(); saveAdvancedRules(); page = 1;
        if (products.length) render();
    };
    const setAdvancedOpen = open => {
        if (!advancedPanel || !advancedToggle) return;
        advancedPanel.hidden = !open;
        advancedToggle.setAttribute('aria-expanded', String(open));
        if (open && advancedRows && !advancedRows.children.length) {
            const row = addAdvancedRule();
            updateAdvancedCount();
            row?.querySelector('[data-rule-field]')?.focus();
        }
    };
    const restoreAdvancedRules = () => {
        if (!advancedRows) return;
        try {
            const stored = JSON.parse(sessionStorage.getItem(rulesStorageKey) || '[]');
            if (Array.isArray(stored)) stored.slice(0, 10).forEach(rule => addAdvancedRule(rule));
        } catch { sessionStorage.removeItem(rulesStorageKey); }
        reindexAdvancedRules(); updateAdvancedCount();
        if (advancedRows.children.length) setAdvancedOpen(true);
    };
    const addHidden = (form, name, value) => {
        const input = document.createElement('input');
        input.type = 'hidden'; input.name = name; input.value = value;
        form.append(input);
    };
    const makeImageCell = product => {
        const cell = make('td', 'product-image-cell');
        cell.dataset.label = 'Imagen';
        const placeholder = () => {
            cell.replaceChildren(make('span', 'product-image-placeholder', 'Sin imagen'));
            cell.firstElementChild.setAttribute('aria-label', 'Producto sin imagen');
        };
        if (!product.imagen) { placeholder(); return cell; }
        const image = make('img', 'product-image');
        image.src = `/uploads/productos/${encodeURIComponent(product.imagen)}`;
        image.alt = `Imagen de ${text(product.nombre)}`;
        image.loading = 'lazy';
        image.addEventListener('error', placeholder, { once: true });
        cell.append(image);
        return cell;
    };
    const makeActions = product => {
        const cell = make('td', 'catalog-actions');
        if (product.activo) {
            const form = make('form', 'catalog-add');
            form.method = 'post'; form.action = '/Carrito/Agregar';
            addHidden(form, csrfName, csrf);
            addHidden(form, 'producto', text(product.id));
            addHidden(form, 'volver', location.pathname + location.search);
            if (Array.isArray(product.colores) && product.colores.length) {
                const label = make('label', 'cable-color-select');
                label.append(make('span', '', 'Color'));
                const select = document.createElement('select');
                select.name = 'color'; select.required = true;
                select.setAttribute('aria-label', `Color de ${text(product.nombre)}`);
                const prompt = document.createElement('option');
                prompt.value = ''; prompt.textContent = 'Elegí un color';
                select.append(prompt);
                product.colores.forEach(color => {
                    const option = document.createElement('option');
                    option.value = text(color.id); option.textContent = text(color.nombre);
                    select.append(option);
                });
                label.append(select); form.append(label);
            }
            const quantity = document.createElement('input');
            quantity.type = 'number'; quantity.name = 'cantidad'; quantity.value = '1';
            quantity.min = '1'; quantity.max = '1000000'; quantity.step = '1';
            quantity.inputMode = 'numeric'; quantity.required = true;
            quantity.setAttribute('aria-label', `Cantidad de ${text(product.nombre)}`);
            const button = make('button', 'primary', '+ Agregar');
            button.name = 'accion'; button.value = 'agregar';
            form.append(quantity, button); cell.append(form);
        }
        if (admin) {
            const edit = make('a', 'button edit-product', 'Editar');
            edit.href = `/EditarProducto?id=${encodeURIComponent(product.id)}`;
            cell.append(edit);
        }
        return cell;
    };
    const makeRow = product => {
        const row = document.createElement('tr');
        const productCell = make('td', 'product-name');
        productCell.append(make('strong', '', text(product.nombre)));
        if (!product.activo) productCell.append(make('span', 'tag', 'Inactivo'));
        if (Array.isArray(product.colores) && product.colores.length) productCell.append(make('span', 'tag color-required', 'Elegir color'));

        const codeCell = make('td', 'codes', text(product.codigoCatalogo) || 'Sin código');
        codeCell.dataset.label = 'Código';
        appendSmall(codeCell, 'catalog-code-extra', `Local: ${text(product.codigoLocal) || 'A consultar'}`);
        const brandCell = make('td', '', text(product.marca));
        brandCell.dataset.label = 'Marca'; appendSmall(brandCell, 'catalog-brand-extra', text(product.tipo));
        const descriptionCell = make('td', 'product-description', text(product.descripcion));
        descriptionCell.dataset.label = 'Descripción'; appendSmall(descriptionCell, 'catalog-description-extra', `Unidad: ${text(product.unidad)}`);
        const priceCell = make('td', 'right catalog-price', Number.isFinite(Number(product.precio))
            ? new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' }).format(Number(product.precio))
            : 'A consultar');
        priceCell.dataset.label = 'Precio';
        row.append(productCell, codeCell, brandCell, descriptionCell, makeImageCell(product), priceCell, makeActions(product));
        return row;
    };
    const renderPagination = pages => {
        pagination.replaceChildren();
        const status = make('span', '', `Página ${page} de ${pages}`);
        pagination.append(status);
        const previous = make('button', '', 'Anterior');
        previous.type = 'button'; previous.disabled = page <= 1;
        previous.addEventListener('click', () => { page -= 1; render(true); });
        const next = make('button', '', 'Siguiente');
        next.type = 'button'; next.disabled = page >= pages;
        next.addEventListener('click', () => { page += 1; render(true); });
        pagination.append(previous, next);
    };
    const render = (scroll = false) => {
        const filtered = filteredProducts();
        const pages = Math.max(1, Math.ceil(filtered.length / pageSize));
        page = Math.min(Math.max(page, 1), pages);
        const visible = filtered.slice((page - 1) * pageSize, page * pageSize);
        body.replaceChildren();
        if (visible.length) visible.forEach(product => body.append(makeRow(product)));
        else {
            const row = document.createElement('tr');
            const cell = make('td', '', 'No hay productos con estos filtros.');
            cell.colSpan = 7; row.append(cell); body.append(row);
        }
        count.textContent = `${filtered.length} materiales`;
        renderPagination(pages);
        updateUrl();
        if (feedback) feedback.hidden = true;
        if (scroll) results.scrollIntoView({ behavior: 'smooth', block: 'start' });
    };
    const readStored = () => {
        try {
            const value = JSON.parse(sessionStorage.getItem(storageKey));
            return value?.version === expectedVersion && Array.isArray(value.productos) ? value.productos : null;
        } catch { return null; }
    };
    const store = data => {
        try {
            const targetKey = `catalogo:${admin ? 'admin' : 'asesor'}:${data.version || expectedVersion}`;
            Object.keys(sessionStorage).filter(key => key.startsWith(`catalogo:${admin ? 'admin' : 'asesor'}:`) && key !== targetKey)
                .forEach(key => sessionStorage.removeItem(key));
            sessionStorage.setItem(targetKey, JSON.stringify(data));
        } catch { /* El catálogo sigue disponible en memoria si el navegador bloquea el almacenamiento. */ }
    };
    const load = async () => {
        const stored = readStored();
        if (stored) { products = stored; render(); return; }
        if (feedback) { feedback.textContent = 'Cargando catálogo…'; feedback.classList.remove('error'); feedback.hidden = false; }
        try {
            const response = await fetch(endpoint, { headers: { 'Accept': 'application/json' } });
            if (!response.ok) throw new Error('No se pudo cargar el catálogo.');
            const data = await response.json();
            if (!Array.isArray(data.productos)) throw new Error('El catálogo recibido no es válido.');
            products = data.productos;
            store({ version: data.version, productos: products });
            render();
        } catch (error) {
            if (feedback) { feedback.textContent = `${error.message} Se mantienen los resultados cargados.`; feedback.classList.add('error'); feedback.hidden = false; }
        }
    };

    filters.addEventListener('submit', event => { event.preventDefault(); page = 1; render(); });
    filters.addEventListener('input', event => {
        if (event.target.name !== 'q' || !products.length) return;
        window.clearTimeout(debounceTimer);
        debounceTimer = window.setTimeout(() => { page = 1; render(); }, 280);
    });
    filters.addEventListener('change', event => {
        if (!['marca', 'tipo', 'inactivos'].includes(event.target.name) || !products.length) return;
        page = 1; render();
    });
    filters.querySelector('[data-catalog-clear]')?.addEventListener('click', event => {
        event.preventDefault();
        ['q', 'marca', 'tipo'].forEach(name => { const field = filters.elements.namedItem(name); if (field) field.value = ''; });
        const inactive = filters.elements.namedItem('inactivos'); if (inactive) inactive.value = 'false';
        advancedRows?.replaceChildren();
        reindexAdvancedRules(); updateAdvancedCount(); saveAdvancedRules();
        page = 1; render();
    });
    advancedToggle?.addEventListener('click', () => setAdvancedOpen(advancedToggle.getAttribute('aria-expanded') !== 'true'));
    advancedAdd?.addEventListener('click', () => {
        const row = addAdvancedRule();
        applyAdvancedChange();
        row?.querySelector('[data-rule-field]')?.focus();
    });
    advancedClear?.addEventListener('click', () => {
        advancedRows?.replaceChildren();
        applyAdvancedChange();
    });
    advancedRows?.addEventListener('click', event => {
        const remove = event.target.closest('[data-rule-remove]');
        if (!remove) return;
        remove.closest('[data-advanced-rule]')?.remove();
        applyAdvancedChange();
    });
    advancedRows?.addEventListener('change', event => {
        const row = event.target.closest('[data-advanced-rule]');
        if (!row) return;
        if (event.target.matches('[data-rule-field]')) refreshRuleEditor(row);
        else if (event.target.matches('[data-rule-operator]')) refreshRuleEditor(row, event.target.value);
        applyAdvancedChange();
    });
    advancedRows?.addEventListener('input', event => {
        if (!event.target.matches('input[data-rule-value]')) return;
        window.clearTimeout(debounceTimer);
        debounceTimer = window.setTimeout(applyAdvancedChange, 280);
    });
    restoreAdvancedRules();
    load();
})();
