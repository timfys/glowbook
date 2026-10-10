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

        alert('Выбор из контактов доступен в приложении GlowBox или в Chrome на Android.');
    });
})();

(function () {
    var toggles = document.querySelectorAll('.gb-theme-toggle');
    if (!toggles.length) return;

    function currentTheme() {
        return document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
    }

    function applyTheme(theme) {
        if (theme === 'dark')
            document.documentElement.setAttribute('data-theme', 'dark');
        else
            document.documentElement.removeAttribute('data-theme');
        try { localStorage.setItem('gb-theme', theme); } catch (_) {}
        syncIcons();
    }

    function syncIcons() {
        var dark = currentTheme() === 'dark';
        document.querySelectorAll('.gb-theme-icon-moon').forEach(function (el) {
            el.hidden = dark;
        });
        document.querySelectorAll('.gb-theme-icon-sun').forEach(function (el) {
            el.hidden = !dark;
        });
    }

    toggles.forEach(function (toggle) {
        toggle.addEventListener('click', function () {
            applyTheme(currentTheme() === 'dark' ? 'light' : 'dark');
        });
    });

    syncIcons();
})();

(function () {
    var form = document.getElementById('registerForm');
    if (!form) return;

    var options = form.querySelectorAll('.account-type-option');
    var usernameWrap = document.getElementById('registerUsernameWrap');

    function isMasterSelected() {
        var checked = form.querySelector('input[name="AccountType"]:checked');
        var val = checked ? checked.value : null;
        if (val == null) {
            var hidden = form.querySelector('input[name="AccountType"][type="hidden"]');
            val = hidden ? hidden.value : 'Master';
        }
        return val === 'Master' || val === '0';
    }

    function sync() {
        options.forEach(function (el) {
            var input = el.querySelector('input[type="radio"]');
            el.classList.toggle('is-selected', input && input.checked);
        });
        if (usernameWrap) {
            usernameWrap.style.display = isMasterSelected() ? '' : 'none';
        }
    }

    form.querySelectorAll('input[name="AccountType"]').forEach(function (r) {
        r.addEventListener('change', sync);
    });
    sync();
})();

(function () {
    function copyViaAndroid(text) {
        try {
            if (window.GlowBookAndroid && typeof window.GlowBookAndroid.copyText === 'function') {
                return !!window.GlowBookAndroid.copyText(text);
            }
        } catch (_) { /* ignore */ }
        return false;
    }

    function copyText(text) {
        if (copyViaAndroid(text)) {
            return Promise.resolve();
        }

        if (navigator.clipboard && navigator.clipboard.writeText) {
            return navigator.clipboard.writeText(text).catch(function () {
                return copyTextFallback(text);
            });
        }

        return copyTextFallback(text);
    }

    function copyTextFallback(text) {
        return new Promise(function (resolve, reject) {
            var area = document.createElement('textarea');
            area.value = text;
            area.setAttribute('readonly', '');
            area.style.position = 'fixed';
            area.style.opacity = '0';
            area.style.left = '-9999px';
            document.body.appendChild(area);
            area.focus();
            area.select();
            try {
                var ok = document.execCommand('copy');
                document.body.removeChild(area);
                if (ok) resolve();
                else reject(new Error('copy failed'));
            } catch (err) {
                document.body.removeChild(area);
                reject(err);
            }
        });
    }

    function showCopied(btn) {
        var box = btn.closest('.booking-link-box');
        var toast = box && box.querySelector('.booking-link-toast');
        var labelEl = btn.querySelector('[data-copy-label-text]');
        var label = btn.getAttribute('data-copy-label') || 'Скопировать ссылку';
        var copied = btn.getAttribute('data-copied-label') || 'Скопировано';
        if (labelEl) labelEl.textContent = copied;
        else btn.textContent = copied;
        btn.disabled = true;
        if (toast) toast.hidden = false;
        window.setTimeout(function () {
            if (labelEl) labelEl.textContent = label;
            else btn.textContent = label;
            btn.disabled = false;
            if (toast) toast.hidden = true;
        }, 1800);
    }

    document.querySelectorAll('[data-copy-url]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var url = btn.getAttribute('data-copy-url') || '';
            if (!url) return;
            copyText(url).then(function () {
                showCopied(btn);
            }).catch(function () {
                window.prompt('Скопируйте ссылку:', url);
            });
        });
    });

    document.querySelectorAll('[data-share-url]').forEach(function (btn) {
        if (!navigator.share) return;
        btn.classList.remove('d-none');
        btn.addEventListener('click', function () {
            var url = btn.getAttribute('data-share-url') || '';
            if (!url) return;
            navigator.share({
                title: btn.getAttribute('data-share-title') || 'GlowBox',
                text: btn.getAttribute('data-share-text') || '',
                url: url
            }).catch(function () { /* cancelled */ });
        });
    });
})();

(function () {
    // Open /book/... in the Android app when installed (Chrome intent URL fallback).
    if (!/^\/book(\/|$)/i.test(location.pathname)) return;
    if (window.GlowBookAndroid) return;

    var ua = navigator.userAgent || '';
    if (!/Android/i.test(ua)) return;
    if (/; wv\)/i.test(ua)) return;

    var host = location.hostname || '';
    if (host !== 'glowbook-production-5e1a.up.railway.app') return;

    var path = location.pathname + location.search + location.hash;
    var intentUrl = 'intent://' + host + path +
        '#Intent;scheme=https;package=com.glowbook.app;S.browser_fallback_url=' +
        encodeURIComponent(location.href) + ';end';

    function tryOpenApp() {
        location.href = intentUrl;
    }

    try {
        if (!sessionStorage.getItem('gb-app-open-tried')) {
            sessionStorage.setItem('gb-app-open-tried', '1');
            window.setTimeout(tryOpenApp, 250);
        }
    } catch (_) { /* private mode */ }

    function mountOpenAppBar() {
        if (document.querySelector('.gb-open-app-bar')) return;
        var bar = document.createElement('div');
        bar.className = 'gb-open-app-bar';
        bar.innerHTML =
            '<span>Есть приложение GlowBox?</span>' +
            '<button type="button" class="btn btn-gb-primary btn-sm">Открыть в приложении</button>';
        var btn = bar.querySelector('button');
        if (btn) btn.addEventListener('click', tryOpenApp);
        var page = document.querySelector('.book-page');
        if (page) page.insertBefore(bar, page.firstChild);
        else document.body.insertBefore(bar, document.body.firstChild);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', mountOpenAppBar);
    } else {
        mountOpenAppBar();
    }
})();
