(function () {
    var root = document.getElementById('gbAppointmentReminders');
    if (!root) return;

    var apiUrl = root.getAttribute('data-api-url');
    if (!apiUrl) return;

    var POLL_MS = 60000;
    var GRACE_MS = 90000;

    var OFFSETS = [
        { key: '24h', minutesBefore: 24 * 60, heading: 'Запись завтра' },
        { key: '1h', minutesBefore: 60, heading: 'Запись через час' },
        { key: '15m', minutesBefore: 15, heading: 'Скоро запись' }
    ];

    var STORAGE_KEY = 'gb-appt-reminders-sent';

    function readSent() {
        try {
            var raw = localStorage.getItem(STORAGE_KEY);
            if (!raw) return {};
            var parsed = JSON.parse(raw);
            return parsed && typeof parsed === 'object' ? parsed : {};
        } catch (_) {
            return {};
        }
    }

    function writeSent(map) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(map));
        } catch (_) { /* ignore */ }
    }

    function reminderKey(id, offsetKey) {
        return String(id) + ':' + offsetKey;
    }

    function pruneSent(map, activeIds) {
        var changed = false;
        Object.keys(map).forEach(function (key) {
            var id = key.split(':')[0];
            if (activeIds.indexOf(id) === -1) {
                delete map[key];
                changed = true;
            }
        });
        return changed;
    }

    function formatTime(iso) {
        try {
            return new Date(iso).toLocaleString('ru-RU', {
                weekday: 'short',
                day: 'numeric',
                month: 'short',
                hour: '2-digit',
                minute: '2-digit'
            });
        } catch (_) {
            return '';
        }
    }

    function pickField(obj, camel, pascal) {
        if (!obj) return '';
        if (obj[camel] != null && obj[camel] !== '') return obj[camel];
        if (obj[pascal] != null && obj[pascal] !== '') return obj[pascal];
        return '';
    }

    function shouldNotify(startsAt, minutesBefore) {
        var startMs = new Date(startsAt).getTime();
        if (isNaN(startMs)) return false;
        var targetMs = startMs - minutesBefore * 60000;
        var now = Date.now();
        return now >= targetMs && now < targetMs + GRACE_MS;
    }

    function requestPermission() {
        if (!('Notification' in window)) return;
        if (Notification.permission === 'default') {
            Notification.requestPermission().catch(function () { /* ignore */ });
        }
    }

    function showNotification(heading, appt) {
        var title = 'GlowBox · ' + heading;
        var subtitle = pickField(appt, 'subtitle', 'Subtitle');
        var when = formatTime(pickField(appt, 'startsAt', 'StartsAt'));
        var apptTitle = pickField(appt, 'title', 'Title') || 'Запись';
        var body = [apptTitle, subtitle, when].filter(Boolean).join(' · ');
        var url = pickField(appt, 'editUrl', 'EditUrl') || '/';

        if (window.GlowBookAndroid && typeof window.GlowBookAndroid.showNotification === 'function') {
            try {
                window.GlowBookAndroid.showNotification(title, body, url);
                return;
            } catch (_) { /* fall through */ }
        }

        if (!('Notification' in window) || Notification.permission !== 'granted') return;

        var id = pickField(appt, 'id', 'Id');
        try {
            var notification = new Notification(title, {
                body: body,
                tag: 'gb-appt-' + id,
                requireInteraction: false
            });

            if (url) {
                notification.onclick = function () {
                    window.focus();
                    window.location.href = url;
                    notification.close();
                };
            }
        } catch (_) { /* ignore */ }
    }

    function processAppointments(items) {
        if (!items || !items.length) return;

        var sent = readSent();
        var activeIds = items.map(function (a) { return String(pickField(a, 'id', 'Id')); });
        if (pruneSent(sent, activeIds)) writeSent(sent);

        var changed = false;

        items.forEach(function (appt) {
            var id = pickField(appt, 'id', 'Id');
            var startsAt = pickField(appt, 'startsAt', 'StartsAt');
            if (!id || !startsAt) return;

            OFFSETS.forEach(function (offset) {
                var key = reminderKey(id, offset.key);
                if (sent[key]) return;
                if (!shouldNotify(startsAt, offset.minutesBefore)) return;

                showNotification(offset.heading, appt);
                sent[key] = Date.now();
                changed = true;
            });
        });

        if (changed) writeSent(sent);
    }

    function fetchAndNotify() {
        fetch(apiUrl, { credentials: 'same-origin' })
            .then(function (r) {
                if (!r.ok) throw new Error('fetch failed');
                return r.json();
            })
            .then(processAppointments)
            .catch(function () { /* ignore */ });
    }

    requestPermission();
    fetchAndNotify();
    setInterval(fetchAndNotify, POLL_MS);

    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) fetchAndNotify();
    });
})();
