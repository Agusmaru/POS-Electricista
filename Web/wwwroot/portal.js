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
