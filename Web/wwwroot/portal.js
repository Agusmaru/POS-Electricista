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
