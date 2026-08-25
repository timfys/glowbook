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
    var pickBtn = document.getElementById('avatarPickBtn');
    var errBox = document.getElementById('avatarClientError');
    var form = input && input.closest('form');
    if (!input || !preview) {
        return;
    }

    function showError(msg) {
        if (!errBox) return;
        errBox.textContent = msg || '';
        errBox.classList.toggle('d-none', !msg);
    }

    function setPreview(url) {
        preview.src = url;
        preview.classList.remove('d-none');
        if (fallback) {
            fallback.classList.add('d-none');
        }
    }

    function resizeImage(file) {
        return new Promise(function (resolve, reject) {
            if (!file || !file.type || file.type.indexOf('image/') !== 0) {
                // iOS sometimes sends empty type for HEIC — still try to decode
                if (!file) {
                    reject(new Error('Файл не выбран'));
                    return;
                }
            }

            var url = URL.createObjectURL(file);
            var img = new Image();
            img.onload = function () {
                URL.revokeObjectURL(url);
                var maxSide = 1024;
                var w = img.naturalWidth || img.width;
                var h = img.naturalHeight || img.height;
                var scale = Math.min(1, maxSide / Math.max(w, h));
                var cw = Math.max(1, Math.round(w * scale));
                var ch = Math.max(1, Math.round(h * scale));
                var canvas = document.createElement('canvas');
                canvas.width = cw;
                canvas.height = ch;
                var ctx = canvas.getContext('2d');
                if (!ctx) {
                    reject(new Error('Не удалось обработать фото'));
                    return;
                }
                ctx.drawImage(img, 0, 0, cw, ch);
                canvas.toBlob(function (blob) {
                    if (!blob) {
                        reject(new Error('Не удалось сжать фото. Попробуйте JPEG/PNG.'));
                        return;
                    }
                    resolve(new File([blob], 'avatar.jpg', { type: 'image/jpeg', lastModified: Date.now() }));
                }, 'image/jpeg', 0.82);
            };
            img.onerror = function () {
                URL.revokeObjectURL(url);
                reject(new Error('Этот формат телефон не отдал. Выберите JPEG или PNG.'));
            };
            img.src = url;
        });
    }

    if (pickBtn) {
        pickBtn.addEventListener('click', function () {
            input.click();
        });
    }

    input.addEventListener('change', function () {
        var file = input.files && input.files[0];
        showError('');
        if (!file) {
            return;
        }

        resizeImage(file).then(function (ready) {
            var dt = new DataTransfer();
            dt.items.add(ready);
            input.files = dt.files;
            setPreview(URL.createObjectURL(ready));
        }).catch(function (err) {
            showError(err.message || 'Не удалось подготовить фото');
            input.value = '';
        });
    });

    if (form) {
        form.addEventListener('submit', function (e) {
            // If user picked a file but resize still running — rare; files already replaced on change.
            showError('');
        });
    }
})();

(function () {
    var triggers = document.querySelectorAll('[data-avatar-zoom]');
    if (!triggers.length) {
        return;
    }

    var overlay = document.createElement('div');
    overlay.className = 'avatar-lightbox';
    overlay.setAttribute('role', 'dialog');
    overlay.setAttribute('aria-modal', 'true');
    overlay.innerHTML =
        '<button type="button" class="avatar-lightbox-close" aria-label="Закрыть">&times;</button>' +
        '<img alt="" />';
    document.body.appendChild(overlay);

    var image = overlay.querySelector('img');
    var closeBtn = overlay.querySelector('.avatar-lightbox-close');

    function openLightbox(src, alt) {
        image.src = src;
        image.alt = alt || '';
        overlay.classList.add('is-open');
        document.body.style.overflow = 'hidden';
    }

    function closeLightbox() {
        overlay.classList.remove('is-open');
        image.removeAttribute('src');
        document.body.style.overflow = '';
    }

    triggers.forEach(function (el) {
        el.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            openLightbox(el.getAttribute('data-avatar-zoom'), (el.querySelector('img') || {}).alt || '');
        });
    });

    closeBtn.addEventListener('click', closeLightbox);
    overlay.addEventListener('click', function (e) {
        if (e.target === overlay) {
            closeLightbox();
        }
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && overlay.classList.contains('is-open')) {
            closeLightbox();
        }
    });
})();
