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

    function setDesktopCollapsed(collapsed) {
        document.body.classList.toggle('gb-sidebar-collapsed', collapsed);
        toggle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
    }

    function isSidebarVisible() {
        return !document.body.classList.contains('gb-sidebar-collapsed');
    }

    toggle.addEventListener('click', function () {
        if (!isDesktop()) {
            return;
        }

        setDesktopCollapsed(isSidebarVisible());
    });

    backdrop.addEventListener('click', function () {
        if (isDesktop()) {
            setDesktopCollapsed(true);
        }
    });

    desktopQuery.addEventListener('change', function () {
        setDesktopCollapsed(false);
    });

    setDesktopCollapsed(false);
})();

(function () {
    var input = document.getElementById('avatarInput');
    var preview = document.getElementById('avatarPreview');
    var fallback = document.getElementById('avatarFallback');
    var errBox = document.getElementById('avatarClientError');
    if (!input) {
        return;
    }

    function showError(msg) {
        if (!errBox) return;
        errBox.textContent = msg || '';
        errBox.classList.toggle('d-none', !msg);
    }

    function setPreviewFromFile(file) {
        if (!preview || !file) return;
        try {
            preview.src = URL.createObjectURL(file);
            preview.classList.remove('d-none');
            if (fallback) fallback.classList.add('d-none');
        } catch (_) { /* WebView may lack createObjectURL */ }
    }

    function canReplaceInputFiles() {
        try {
            return typeof DataTransfer !== 'undefined' && typeof File !== 'undefined';
        } catch (_) {
            return false;
        }
    }

    function resizeImage(file) {
        return new Promise(function (resolve, reject) {
            if (!file) {
                reject(new Error('Файл не выбран'));
                return;
            }
            if (typeof URL === 'undefined' || !URL.createObjectURL) {
                resolve(file);
                return;
            }

            var url = URL.createObjectURL(file);
            var img = new Image();
            img.onload = function () {
                try { URL.revokeObjectURL(url); } catch (_) {}
                var maxSide = 1024;
                var w = img.naturalWidth || img.width;
                var h = img.naturalHeight || img.height;
                var scale = Math.min(1, maxSide / Math.max(w, h || 1));
                var cw = Math.max(1, Math.round(w * scale));
                var ch = Math.max(1, Math.round(h * scale));
                var canvas = document.createElement('canvas');
                canvas.width = cw;
                canvas.height = ch;
                var ctx = canvas.getContext && canvas.getContext('2d');
                if (!ctx || !canvas.toBlob) {
                    resolve(file);
                    return;
                }
                ctx.drawImage(img, 0, 0, cw, ch);
                canvas.toBlob(function (blob) {
                    if (!blob || !canReplaceInputFiles()) {
                        resolve(file);
                        return;
                    }
                    try {
                        resolve(new File([blob], 'avatar.jpg', { type: 'image/jpeg', lastModified: Date.now() }));
                    } catch (_) {
                        resolve(file);
                    }
                }, 'image/jpeg', 0.82);
            };
            img.onerror = function () {
                try { URL.revokeObjectURL(url); } catch (_) {}
                // Keep original — WebView/HEIC often fails decode but upload may still work
                resolve(file);
            };
            img.src = url;
        });
    }

    input.addEventListener('change', function () {
        var file = input.files && input.files[0];
        var form = input.closest('form');
        var autoSubmit = form && form.getAttribute('data-avatar-autosubmit') === '1';
        showError('');
        if (!file) return;

        setPreviewFromFile(file);

        function maybeSubmit() {
            if (autoSubmit && form) {
                form.submit();
            }
        }

        // Optional compress — never wipe the chosen file on failure (critical for Android WebView)
        if (!canReplaceInputFiles()) {
            maybeSubmit();
            return;
        }

        resizeImage(file).then(function (ready) {
            if (ready && ready !== file) {
                try {
                    var dt = new DataTransfer();
                    dt.items.add(ready);
                    input.files = dt.files;
                    setPreviewFromFile(ready);
                } catch (_) {
                    // Keep original file in the input
                }
            }
            maybeSubmit();
        }).catch(function () {
            maybeSubmit();
        });
    });
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

(function () {
    var form = document.querySelector('[data-client-form="1"]');
    if (!form) {
        return;
    }

    var nameInput = document.getElementById('clientName');
    var phoneInput = document.getElementById('clientPhone');
    var pickBtn = document.getElementById('pickContactBtn');
    if (!nameInput || !phoneInput || !pickBtn) {
        return;
    }

    window.GlowBook = window.GlowBook || {};

    function normalizePhone(value) {
        if (!value) {
            return '';
        }
        var digits = String(value).replace(/\D/g, '');
        if (digits.length === 11 && digits.charAt(0) === '8') {
            return '+7' + digits.slice(1);
        }
        if (digits.length === 10) {
            return '+7' + digits;
        }
        if (digits.length > 0 && String(value).trim().charAt(0) === '+') {
            return '+' + digits;
        }
        return String(value).trim();
    }

    function applyContact(data) {
        if (!data) {
            return;
        }
        if (data.name) {
            nameInput.value = data.name;
        }
        if (data.phone) {
            phoneInput.value = normalizePhone(data.phone);
        }
        nameInput.dispatchEvent(new Event('input', { bubbles: true }));
        phoneInput.dispatchEvent(new Event('input', { bubbles: true }));
    }

    window.GlowBook.onContactSelected = applyContact;
    window.GlowBook.onContactPickFailed = function () {
        /* user cancelled or denied permission */
    };

    function pickViaBrowserApi() {
        if (!navigator.contacts || !navigator.contacts.select) {
            return false;
        }

        navigator.contacts.select(['name', 'tel'], { multiple: false })
            .then(function (contacts) {
                if (!contacts || !contacts.length) {
                    return;
                }
                var contact = contacts[0];
                var name = '';
                var phone = '';
                if (contact.name && contact.name.length) {
                    name = contact.name[0];
                }
                if (contact.tel && contact.tel.length) {
                    phone = contact.tel[0];
                }
                applyContact({ name: name, phone: phone });
            })
            .catch(function () { /* cancelled */ });

        return true;
    }

    pickBtn.addEventListener('click', function () {
        if (window.GlowBookAndroid && typeof window.GlowBookAndroid.pickContact === 'function') {
            window.GlowBookAndroid.pickContact();
            return;
        }

        if (pickViaBrowserApi()) {
            return;
        }

        alert('Выбор из контактов доступен в приложении GlowBook или в Chrome на Android.');
    });
})();

(function () {
    var toggle = document.getElementById('gbThemeToggle');
    if (!toggle) return;

    function currentTheme() {
        return document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
    }

    function applyTheme(theme) {
        if (theme === 'dark')
            document.documentElement.setAttribute('data-theme', 'dark');
        else
            document.documentElement.removeAttribute('data-theme');
        try { localStorage.setItem('gb-theme', theme); } catch (_) {}
    }

    toggle.addEventListener('click', function () {
        applyTheme(currentTheme() === 'dark' ? 'light' : 'dark');
    });
})();

(function () {
    var form = document.getElementById('registerForm');
    if (!form) return;

    var options = form.querySelectorAll('.account-type-option');

    function sync() {
        options.forEach(function (el) {
            var input = el.querySelector('input[type="radio"]');
            el.classList.toggle('is-selected', input && input.checked);
        });
    }

    form.querySelectorAll('input[name="AccountType"]').forEach(function (r) {
        r.addEventListener('change', sync);
    });
    sync();
})();
