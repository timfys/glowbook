(function () {
    var toggle = document.getElementById('gbSidebarToggle');
    var backdrop = document.getElementById('gbSidebarBackdrop');
    var sidebar = document.getElementById('gbSidebar');

    if (!toggle || !backdrop || !sidebar) {
        return;
    }

    var desktopQuery = window.matchMedia('(min-width: 992px)');

    function isDesktop() {
        return desktopQuery.matches;
    }

    function setMobileOpen(open) {
        document.body.classList.toggle('gb-sidebar-open', open);
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    }

    function setDesktopCollapsed(collapsed) {
        document.body.classList.toggle('gb-sidebar-collapsed', collapsed);
        toggle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
    }

    function isSidebarVisible() {
        if (isDesktop()) {
            return !document.body.classList.contains('gb-sidebar-collapsed');
        }

        return document.body.classList.contains('gb-sidebar-open');
    }

    toggle.addEventListener('click', function () {
        if (isDesktop()) {
            setDesktopCollapsed(isSidebarVisible());
            return;
        }

        setMobileOpen(!document.body.classList.contains('gb-sidebar-open'));
    });

    backdrop.addEventListener('click', function () {
        setMobileOpen(false);
    });

    sidebar.querySelectorAll('.nav-link').forEach(function (link) {
        link.addEventListener('click', function () {
            if (!isDesktop()) {
                setMobileOpen(false);
            }
        });
    });

    desktopQuery.addEventListener('change', function () {
        setMobileOpen(false);
        setDesktopCollapsed(false);
    });

    setDesktopCollapsed(false);
})();

(function () {
    var input = document.getElementById('avatarInput');
    var preview = document.getElementById('avatarPreview');
    var fallback = document.getElementById('avatarFallback');
    if (!input || !preview) {
        return;
    }

    input.addEventListener('change', function () {
        var file = input.files && input.files[0];
        if (!file) {
            return;
        }

        preview.src = URL.createObjectURL(file);
        preview.classList.remove('d-none');
        if (fallback) {
            fallback.classList.add('d-none');
        }
    });
})();
