(function () {
    var chat = document.getElementById('gbClientChat');
    var box = document.getElementById('gbChatMessages');
    var form = document.getElementById('gbChatForm');
    var input = document.getElementById('gbChatInput');
    var attachBtn = document.getElementById('gbChatAttachBtn');
    var preview = document.getElementById('gbChatAttachPreview');
    var strip = document.getElementById('gbChatAttachStrip');
    var clearBtn = document.getElementById('gbChatAttachClear');
    if (!chat || !box || !form || !input) return;

    document.body.classList.add('gb-chat-active');

    var clientRecordId = parseInt(chat.getAttribute('data-client-record-id') || '0', 10);
    var sendUrl = chat.getAttribute('data-send-url') || '';
    var hubUrl = chat.getAttribute('data-hub-url') || '/hubs/chat';
    var streamUrl = chat.getAttribute('data-stream-url') || '';
    var attachmentPattern = chat.getAttribute('data-attachment-url-pattern') || '/chat/api/attachment/{0}';
    var currentUserId = chat.getAttribute('data-current-user-id') || '';
    var pendingFiles = [];
    var pendingUrls = [];

    var lastId = 0;
    box.querySelectorAll('[data-id]').forEach(function (el) {
        var id = parseInt(el.getAttribute('data-id'), 10);
        if (id > lastId) lastId = id;
    });
    scrollToBottom();

    function revokePendingUrls() {
        pendingUrls.forEach(function (url) {
            try { URL.revokeObjectURL(url); } catch (_) {}
        });
        pendingUrls = [];
    }

    function clearAttachPreview() {
        revokePendingUrls();
        pendingFiles = [];
        if (strip) strip.innerHTML = '';
        if (preview) preview.hidden = true;
    }

    function showAttachPreview(files) {
        clearAttachPreview();
        pendingFiles = Array.prototype.slice.call(files || []).filter(Boolean);
        if (!pendingFiles.length || !strip || !preview) return;

        pendingFiles.forEach(function (file, index) {
            var tile = document.createElement('div');
            tile.className = 'gb-chat-attach-item';
            var isImage = file.type && file.type.indexOf('image/') === 0;
            var isVideo = file.type && file.type.indexOf('video/') === 0;
            if (isImage || isVideo) {
                var url = URL.createObjectURL(file);
                pendingUrls.push(url);
                if (isVideo) {
                    tile.innerHTML = '<video src="' + url + '" muted playsinline preload="metadata"></video><span class="gb-chat-attach-item-badge">VIDEO</span>';
                } else {
                    tile.style.backgroundImage = 'url("' + url + '")';
                }
            } else {
                tile.classList.add('is-file');
                tile.innerHTML = '<span class="gb-chat-attach-item-file">PDF</span><span class="gb-chat-attach-item-name"></span>';
                tile.querySelector('.gb-chat-attach-item-name').textContent = file.name || 'Файл';
            }
            var remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'gb-chat-attach-item-remove';
            remove.setAttribute('aria-label', 'Убрать');
            remove.innerHTML = '&times;';
            remove.addEventListener('click', function () {
                pendingFiles.splice(index, 1);
                showAttachPreview(pendingFiles.slice());
            });
            tile.appendChild(remove);
            strip.appendChild(tile);
        });
        preview.hidden = false;
    }

    if (attachBtn) {
        attachBtn.addEventListener('click', function () {
            if (window.GbMedia && typeof window.GbMedia.openAttachPicker === 'function') {
                window.GbMedia.openAttachPicker({
                    maxItems: 12,
                    onConfirm: function (files) {
                        showAttachPreview(files);
                    }
                });
                return;
            }

            var fallback = document.createElement('input');
            fallback.type = 'file';
            fallback.accept = 'image/*,video/*,.pdf,application/pdf';
            fallback.multiple = true;
            fallback.hidden = true;
            document.body.appendChild(fallback);
            fallback.addEventListener('change', function () {
                showAttachPreview(fallback.files);
                fallback.remove();
            });
            fallback.click();
        });
    }

    if (clearBtn) {
        clearBtn.addEventListener('click', clearAttachPreview);
    }

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        sendMessage();
    });

    function formatTime(iso) {
        try {
            var d = new Date(iso);
            var pad = function (n) { return n < 10 ? '0' + n : n; };
            return pad(d.getDate()) + '.' + pad(d.getMonth() + 1) + ' ' + pad(d.getHours()) + ':' + pad(d.getMinutes());
        } catch (_) { return ''; }
    }

    function attachmentUrl(id) {
        return attachmentPattern.replace('{0}', String(id));
    }

    function resolveIsMine(msg) {
        var senderId = msg.senderUserId || msg.SenderUserId || '';
        if (senderId && currentUserId) return senderId === currentUserId;
        if (typeof msg.isMine === 'boolean') return msg.isMine;
        if (typeof msg.IsMine === 'boolean') return msg.IsMine;
        return false;
    }

    function buildAttachmentHtml(msg) {
        var hasAttachment = msg.hasAttachment || msg.HasAttachment;
        if (!hasAttachment) return '';

        var id = msg.id || msg.Id;
        var url = msg.attachmentUrl || msg.AttachmentUrl || attachmentUrl(id);
        var fileName = msg.attachmentFileName || msg.AttachmentFileName || 'Файл';
        var isImage = msg.isImageAttachment || msg.IsImageAttachment;
        var isVideo = msg.isVideoAttachment || msg.IsVideoAttachment;

        if (isImage) {
            return '<div class="gb-chat-attachment">' +
                '<button type="button" class="gb-chat-attachment-zoom" data-gb-zoom="' + escapeAttr(url) + '" aria-label="Открыть фото">' +
                '<img class="gb-chat-attachment-img" src="' + escapeAttr(url) + '" alt="' + escapeAttr(fileName) + '" loading="lazy" /></button></div>';
        }
        if (isVideo) {
            return '<div class="gb-chat-attachment">' +
                '<video class="gb-chat-attachment-video" src="' + escapeAttr(url) + '" controls playsinline preload="metadata"></video></div>';
        }
        return '<div class="gb-chat-attachment">' +
            '<a class="gb-chat-file-chip" href="' + escapeAttr(url) + '" download="' + escapeAttr(fileName) + '">' +
            '<span class="gb-chat-file-chip-icon" aria-hidden="true">PDF</span>' +
            '<span class="gb-chat-file-chip-name">' + escapeHtml(fileName) + '</span></a></div>';
    }

    function escapeHtml(text) {
        var div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }

    function escapeAttr(text) {
        return String(text || '').replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function appendMessage(msg) {
        var id = msg.id || msg.Id;
        if (!id || box.querySelector('[data-id="' + id + '"]')) return;

        var isMine = resolveIsMine(msg);
        var body = msg.body != null ? msg.body : (msg.Body || '');

        var div = document.createElement('div');
        div.className = 'gb-chat-bubble ' + (isMine ? 'is-mine' : 'is-theirs');
        div.setAttribute('data-id', id);

        var html = '';
        if (body) {
            html += '<div class="gb-chat-bubble-body">' + escapeHtml(body) + '</div>';
        }
        html += buildAttachmentHtml(msg);
        html += '<div class="gb-chat-bubble-meta">' + formatTime(msg.createdAt || msg.CreatedAt) + '</div>';
        div.innerHTML = html;

        box.appendChild(div);
        if (id > lastId) lastId = id;
        scrollToBottom();
    }

    function scrollToBottom() {
        box.scrollTop = box.scrollHeight;
    }

    function notifyIncoming(msg) {
        if (!('Notification' in window) || Notification.permission !== 'granted') return;
        if (resolveIsMine(msg)) return;
        if (!document.hidden) return;

        var body = msg.body || msg.Body || '';
        var hasAttachment = msg.hasAttachment || msg.HasAttachment;
        var senderName = msg.senderName || msg.SenderName || 'Новое сообщение';
        var text = body || (hasAttachment ? 'Вложение' : 'Новое сообщение');

        try {
            new Notification('GlowBox · ' + senderName, { body: text, tag: 'gb-chat-' + clientRecordId });
        } catch (_) { /* ignore */ }
    }

    function requestNotificationPermission() {
        if (!('Notification' in window)) return;
        if (Notification.permission === 'default') {
            Notification.requestPermission().catch(function () { /* ignore */ });
        }
    }

    function postOne(text, file) {
        var formData = new FormData();
        if (text) formData.append('message', text);
        if (file) formData.append('file', file);
        return fetch(sendUrl, {
            method: 'POST',
            body: formData,
            credentials: 'same-origin'
        }).then(function (r) {
            if (!r.ok) throw new Error('send failed');
            return r.json();
        });
    }

    function sendMessage() {
        var text = (input.value || '').trim();
        var files = pendingFiles.slice();
        if (!text && !files.length) return;

        var sendBtn = document.getElementById('gbChatSend');
        if (sendBtn) sendBtn.disabled = true;

        var chain = Promise.resolve();
        if (files.length) {
            files.forEach(function (file, index) {
                chain = chain.then(function () {
                    return postOne(index === 0 ? text : '', file).then(appendMessage);
                });
            });
        } else {
            chain = postOne(text, null).then(appendMessage);
        }

        chain
            .then(function () {
                input.value = '';
                clearAttachPreview();
            })
            .catch(function () {
                alert('Не удалось отправить. Фото/PDF до 5 МБ, видео до 25 МБ.');
            })
            .finally(function () {
                if (sendBtn) sendBtn.disabled = false;
            });
    }

    function handleIncoming(msg) {
        appendMessage(msg);
        notifyIncoming(msg);
    }

    function startStream() {
        if (!streamUrl || typeof EventSource === 'undefined') return null;

        if (window._gbChatEventSource) {
            window._gbChatEventSource.close();
            window._gbChatEventSource = null;
        }

        var url = streamUrl + (streamUrl.indexOf('?') >= 0 ? '&' : '?') + 'after=' + lastId;
        var source = new EventSource(url);
        window._gbChatEventSource = source;

        source.addEventListener('message', function (e) {
            try {
                handleIncoming(JSON.parse(e.data));
            } catch (_) { /* ignore malformed */ }
        });

        source.onerror = function () {
            source.close();
            if (window._gbChatEventSource === source) {
                window._gbChatEventSource = null;
            }
            setTimeout(startStream, 3000);
        };

        return source;
    }

    function startHub() {
        if (!window.signalR || !hubUrl) return null;

        var connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, { withCredentials: true })
            .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
            .build();

        connection.on('ReceiveMessage', handleIncoming);

        connection.onreconnected(function () {
            connection.invoke('JoinThread', clientRecordId).catch(function () { /* retry on next reconnect */ });
        });

        connection.start()
            .then(function () { return connection.invoke('JoinThread', clientRecordId); })
            .catch(function () { /* SSE keeps working */ });

        return connection;
    }

    var chatInputFocused = false;
    var viewportBaseline = window.visualViewport
        ? window.visualViewport.height
        : window.innerHeight;

    function syncKeyboardInset() {
        var vv = window.visualViewport;
        var inset = 0;
        if (vv) {
            // Covered area under the visual viewport (software keyboard).
            inset = Math.max(0, Math.round(window.innerHeight - vv.height - vv.offsetTop));
            // Fallback when innerHeight already shrank (adjustResize) or WebView quirks.
            if (inset < 80 && chatInputFocused) {
                var shrink = Math.round(viewportBaseline - vv.height);
                if (shrink > 80) inset = shrink;
            }
        }

        document.documentElement.style.setProperty('--gb-keyboard-inset', inset + 'px');
        // Focus is the source of truth: with adjustResize inset can stay 0 while IME is open.
        document.body.classList.toggle('gb-chat-keyboard-open', chatInputFocused || inset > 80);

        if (chatInputFocused) {
            try {
                form.scrollIntoView({ block: 'end', behavior: 'auto' });
            } catch (_) {
                form.scrollIntoView(false);
            }
            scrollToBottom();
        }
    }

    function bindKeyboardAvoidance() {
        var vv = window.visualViewport;
        if (vv) {
            vv.addEventListener('resize', syncKeyboardInset);
            vv.addEventListener('scroll', syncKeyboardInset);
        }
        window.addEventListener('resize', syncKeyboardInset);
        input.addEventListener('focus', function () {
            chatInputFocused = true;
            document.body.classList.add('gb-chat-keyboard-open');
            window.setTimeout(syncKeyboardInset, 50);
            window.setTimeout(syncKeyboardInset, 250);
            window.setTimeout(syncKeyboardInset, 450);
        });
        input.addEventListener('blur', function () {
            chatInputFocused = false;
            window.setTimeout(function () {
                if (!chatInputFocused) {
                    document.body.classList.remove('gb-chat-keyboard-open');
                    syncKeyboardInset();
                }
            }, 80);
        });
        // Refresh baseline when returning to the page without keyboard.
        window.addEventListener('pageshow', function () {
            viewportBaseline = window.visualViewport
                ? window.visualViewport.height
                : window.innerHeight;
        });
        syncKeyboardInset();
    }

    requestNotificationPermission();
    bindKeyboardAvoidance();
    startStream();
    startHub();
})();
