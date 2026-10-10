(function () {
    if (!window.signalR) return;
    if (window.__gbPushNotifyBound) return;
    window.__gbPushNotifyBound = true;

    function requestPerms() {
        if (window.GlowBookAndroid && typeof window.GlowBookAndroid.requestNotificationPermission === 'function') {
            try { window.GlowBookAndroid.requestNotificationPermission(); } catch (_) { /* ignore */ }
        }
        if ('Notification' in window && Notification.permission === 'default') {
            Notification.requestPermission().catch(function () { /* ignore */ });
        }
    }

    function currentChatThreadId() {
        var el = document.querySelector('[data-client-record-id]');
        if (!el) return null;
        var n = parseInt(el.getAttribute('data-client-record-id'), 10);
        return isNaN(n) ? null : n;
    }

    function show(payload) {
        if (!payload) return;
        var title = payload.title || payload.Title || 'GlowBox';
        var body = payload.body || payload.Body || '';
        var url = payload.url || payload.Url || '/';
        var threadId = payload.threadId != null ? payload.threadId : payload.ThreadId;
        var openThread = currentChatThreadId();

        // Already looking at this chat — skip toast noise.
        if (threadId != null && openThread === threadId && !document.hidden) return;

        if (window.GlowBookAndroid && typeof window.GlowBookAndroid.showNotification === 'function') {
            try {
                window.GlowBookAndroid.showNotification(title, body, url);
                return;
            } catch (_) { /* fall through */ }
        }

        if (!('Notification' in window) || Notification.permission !== 'granted') return;
        if (!document.hidden && threadId != null && openThread === threadId) return;

        try {
            var n = new Notification(title, { body: body, tag: 'gb-' + (payload.kind || 'notify') + '-' + (threadId || url) });
            n.onclick = function () {
                try { window.focus(); } catch (_) {}
                if (url) window.location.href = url;
                n.close();
            };
        } catch (_) { /* ignore */ }
    }

    requestPerms();

    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/notify', { withCredentials: true })
        .withAutomaticReconnect([0, 1000, 3000, 5000, 10000, 30000])
        .build();

    connection.on('UserNotify', show);

    function start() {
        connection.start().catch(function () {
            window.setTimeout(start, 4000);
        });
    }

    start();
})();
