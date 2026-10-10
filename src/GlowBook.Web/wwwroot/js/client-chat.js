(function () {
    var chat = document.getElementById('gbClientChat');
    var box = document.getElementById('gbChatMessages');
    var form = document.getElementById('gbChatForm');
    var input = document.getElementById('gbChatInput');
    var fileInput = document.getElementById('gbChatFile');
    var attachBtn = document.getElementById('gbChatAttachBtn');
    var preview = document.getElementById('gbChatAttachPreview');
    var thumb = document.getElementById('gbChatAttachThumb');
    var nameEl = document.getElementById('gbChatAttachName');
    var sizeEl = document.getElementById('gbChatAttachSize');
    var clearBtn = document.getElementById('gbChatAttachClear');
    if (!chat || !box || !form || !input) return;

    var clientRecordId = parseInt(chat.getAttribute('data-client-record-id') || '0', 10);
    var sendUrl = chat.getAttribute('data-send-url') || '';
    var hubUrl = chat.getAttribute('data-hub-url') || '/hubs/chat';
    var streamUrl = chat.getAttribute('data-stream-url') || '';
    var attachmentPattern = chat.getAttribute('data-attachment-url-pattern') || '/chat/api/attachment/{0}';
    var currentUserId = chat.getAttribute('data-current-user-id') || '';
    var previewObjectUrl = null;

    var lastId = 0;
    box.querySelectorAll('[data-id]').forEach(function (el) {
        var id = parseInt(el.getAttribute('data-id'), 10);
        if (id > lastId) lastId = id;
    });
    scrollToBottom();

    function formatBytes(n) {
        if (!n && n !== 0) return '';
        if (n < 1024) return n + ' Б';
        if (n < 1024 * 1024) return (n / 1024).toFixed(1) + ' КБ';
        return (n / (1024 * 1024)).toFixed(1) + ' МБ';
    }

    function clearAttachPreview() {
        if (previewObjectUrl) {
            try { URL.revokeObjectURL(previewObjectUrl); } catch (_) {}
            previewObjectUrl = null;
        }
        if (fileInput) fileInput.value = '';
        if (thumb) {
            thumb.style.backgroundImage = '';
            thumb.textContent = '';
            thumb.classList.remove('is-file');
        }
        if (nameEl) nameEl.textContent = '';
        if (sizeEl) sizeEl.textContent = '';
        if (preview) preview.hidden = true;
    }

    function showAttachPreview(file) {
        if (!file || !preview) return;
        if (previewObjectUrl) {
            try { URL.revokeObjectURL(previewObjectUrl); } catch (_) {}
            previewObjectUrl = null;
        }
        if (nameEl) nameEl.textContent = file.name || 'Файл';
        if (sizeEl) sizeEl.textContent = formatBytes(file.size);
        if (thumb) {
            if (file.type && file.type.indexOf('image/') === 0) {
                previewObjectUrl = URL.createObjectURL(file);
                thumb.style.backgroundImage = 'url("' + previewObjectUrl + '")';
                thumb.textContent = '';
                thumb.classList.remove('is-file');
            } else {
                thumb.style.backgroundImage = '';
                thumb.textContent = 'PDF';
                thumb.classList.add('is-file');
            }
        }
        preview.hidden = false;
    }

    function pickFile(accept, capture) {
        if (!fileInput) return;
        fileInput.setAttribute('accept', accept || '*/*');
        if (capture) fileInput.setAttribute('capture', 'environment');
        else fileInput.removeAttribute('capture');
        fileInput.click();
    }

    if (attachBtn) {
        attachBtn.addEventListener('click', function () {
            if (window.GbMedia && typeof window.GbMedia.openSheet === 'function') {
                window.GbMedia.openSheet('Вложение', [
                    {
                        label: 'Фото',
                        onClick: function () { pickFile('image/*', false); }
                    },
                    {
                        label: 'Снимок',
                        onClick: function () { pickFile('image/*', true); }
                    },
                    {
                        label: 'Файл',
                        onClick: function () { pickFile('image/*,.pdf,application/pdf', false); }
                    }
                ]);
            } else {
                pickFile('image/*,.pdf', false);
            }
        });
    }

    if (fileInput) {
        fileInput.addEventListener('change', function () {
            var file = fileInput.files && fileInput.files[0];
            if (file) showAttachPreview(file);
            else clearAttachPreview();
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

        if (isImage) {
            return '<div class="gb-chat-attachment">' +
                '<button type="button" class="gb-chat-attachment-zoom" data-gb-zoom="' + escapeAttr(url) + '" aria-label="Открыть фото">' +
                '<img class="gb-chat-attachment-img" src="' + escapeAttr(url) + '" alt="' + escapeAttr(fileName) + '" loading="lazy" /></button></div>';
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
        var text = body || (hasAttachment ? 'Отправлен файл' : 'Новое сообщение');

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

    function clearForm() {
        input.value = '';
        clearAttachPreview();
    }

    function sendMessage() {
        var text = (input.value || '').trim();
        var file = fileInput && fileInput.files && fileInput.files.length > 0 ? fileInput.files[0] : null;
        if (!text && !file) return;

        var sendBtn = document.getElementById('gbChatSend');
        if (sendBtn) sendBtn.disabled = true;

        var formData = new FormData();
        if (text) formData.append('message', text);
        if (file) formData.append('file', file);

        fetch(sendUrl, {
            method: 'POST',
            body: formData,
            credentials: 'same-origin'
        })
            .then(function (r) {
                if (!r.ok) throw new Error('send failed');
                return r.json();
            })
            .then(function (msg) {
                appendMessage(msg);
                clearForm();
            })
            .catch(function () {
                alert('Не удалось отправить сообщение');
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

    requestNotificationPermission();
    startStream();
    startHub();
})();
