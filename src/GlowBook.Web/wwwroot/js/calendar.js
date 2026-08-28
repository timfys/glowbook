(function () {
    var wrap = document.querySelector('.week-calendar-wrap');
    if (!wrap) return;

    var savedWindowY = 0;
    var savedWrapX = 0;

    function saveScroll() {
        savedWindowY = window.scrollY || window.pageYOffset || 0;
        savedWrapX = wrap.scrollLeft;
    }

    function restoreScroll() {
        requestAnimationFrame(function () {
            window.scrollTo(0, savedWindowY);
            wrap.scrollLeft = savedWrapX;
        });
    }

    wrap.querySelectorAll('.week-event-time-input').forEach(function (input) {
        input.addEventListener('mousedown', saveScroll);
        input.addEventListener('touchstart', saveScroll, { passive: true });
        input.addEventListener('focus', function () {
            saveScroll();
            if (typeof input.focus === 'function') {
                try {
                    input.focus({ preventScroll: true });
                } catch (_) {
                    restoreScroll();
                }
            }
        });
        input.addEventListener('blur', restoreScroll);
    });

    wrap.querySelectorAll('.week-event-time-form').forEach(function (form) {
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            saveScroll();

            var saveBtn = form.querySelector('.week-event-time-save');
            if (saveBtn) saveBtn.disabled = true;

            var token = form.querySelector('input[name="__RequestVerificationToken"]');
            var body = new FormData(form);

            fetch(form.action, {
                method: 'POST',
                body: body,
                credentials: 'same-origin',
                headers: {
                    'X-Requested-With': 'fetch',
                    'Accept': 'application/json'
                }
            })
                .then(function (r) {
                    return r.json().then(function (data) {
                        if (!r.ok) throw new Error((data && data.error) || 'save failed');
                        return data;
                    });
                })
                .then(function () {
                    form.classList.add('week-event-time-saved');
                    setTimeout(function () {
                        form.classList.remove('week-event-time-saved');
                    }, 1200);
                    restoreScroll();
                })
                .catch(function (err) {
                    alert(err.message || 'Не удалось сохранить время');
                    restoreScroll();
                })
                .finally(function () {
                    if (saveBtn) saveBtn.disabled = false;
                });
        });
    });

    wrap.querySelectorAll('.week-event-actions a[href]').forEach(function (link) {
        link.addEventListener('click', function () {
            try {
                sessionStorage.setItem('gbCalendarScrollY', String(window.scrollY || 0));
                sessionStorage.setItem('gbCalendarScrollX', String(wrap.scrollLeft));
            } catch (_) { /* ignore */ }
        });
    });

    try {
        var y = sessionStorage.getItem('gbCalendarScrollY');
        var x = sessionStorage.getItem('gbCalendarScrollX');
        if (y != null) {
            requestAnimationFrame(function () {
                window.scrollTo(0, parseFloat(y) || 0);
                wrap.scrollLeft = parseFloat(x || '0') || 0;
            });
            sessionStorage.removeItem('gbCalendarScrollY');
            sessionStorage.removeItem('gbCalendarScrollX');
        }
    } catch (_) { /* ignore */ }
})();
