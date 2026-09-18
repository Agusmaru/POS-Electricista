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
