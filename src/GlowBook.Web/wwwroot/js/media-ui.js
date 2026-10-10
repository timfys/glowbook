(function () {
    'use strict';

    function el(tag, className, html) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (html != null) node.innerHTML = html;
        return node;
    }

    function ensureSheet() {
        var sheet = document.getElementById('gbMediaSheet');
        if (sheet) return sheet;

        sheet = el('div', 'gb-media-sheet');
        sheet.id = 'gbMediaSheet';
        sheet.setAttribute('hidden', '');
        sheet.innerHTML =
            '<div class="gb-media-sheet-backdrop" data-gb-sheet-close></div>' +
            '<div class="gb-media-sheet-panel" role="dialog" aria-modal="true" aria-label="Действия">' +
            '  <div class="gb-media-sheet-handle" aria-hidden="true"></div>' +
            '  <div class="gb-media-sheet-title" id="gbMediaSheetTitle"></div>' +
            '  <div class="gb-media-sheet-actions" id="gbMediaSheetActions"></div>' +
            '  <button type="button" class="gb-media-sheet-cancel" data-gb-sheet-close>Отмена</button>' +
            '</div>';
        document.body.appendChild(sheet);

        sheet.addEventListener('click', function (e) {
            if (e.target.closest('[data-gb-sheet-close]')) closeSheet();
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && !sheet.hasAttribute('hidden')) closeSheet();
        });
        return sheet;
    }

    function openSheet(title, actions) {
        var sheet = ensureSheet();
        var titleEl = sheet.querySelector('#gbMediaSheetTitle');
        var list = sheet.querySelector('#gbMediaSheetActions');
        titleEl.textContent = title || '';
        titleEl.hidden = !title;
        list.innerHTML = '';

        (actions || []).forEach(function (action) {
            if (!action) return;
            var btn = el('button', 'gb-media-sheet-action' + (action.danger ? ' is-danger' : ''));
            btn.type = 'button';
            btn.innerHTML =
                (action.icon ? '<span class="gb-media-sheet-icon" aria-hidden="true">' + action.icon + '</span>' : '') +
                '<span>' + action.label + '</span>';
            btn.addEventListener('click', function () {
                closeSheet();
                if (typeof action.onClick === 'function') action.onClick();
            });
            list.appendChild(btn);
        });

        sheet.removeAttribute('hidden');
        requestAnimationFrame(function () { sheet.classList.add('is-open'); });
        document.body.style.overflow = 'hidden';
    }

    function closeSheet() {
        var sheet = document.getElementById('gbMediaSheet');
        if (!sheet) return;
        sheet.classList.remove('is-open');
        setTimeout(function () { sheet.setAttribute('hidden', ''); }, 180);
        document.body.style.overflow = '';
    }

    function ensureLightbox() {
        var overlay = document.getElementById('gbMediaLightbox');
        if (overlay) return overlay;

        overlay = el('div', 'avatar-lightbox gb-media-lightbox');
        overlay.id = 'gbMediaLightbox';
        overlay.setAttribute('role', 'dialog');
        overlay.setAttribute('aria-modal', 'true');
        overlay.innerHTML =
            '<button type="button" class="avatar-lightbox-close" aria-label="Закрыть">&times;</button>' +
            '<img alt="" />';
        document.body.appendChild(overlay);

        var closeBtn = overlay.querySelector('.avatar-lightbox-close');
        function closeLightbox() {
            overlay.classList.remove('is-open', 'is-round');
            overlay.querySelector('img').removeAttribute('src');
            document.body.style.overflow = '';
        }

        closeBtn.addEventListener('click', closeLightbox);
        overlay.addEventListener('click', function (e) {
            if (e.target === overlay) closeLightbox();
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && overlay.classList.contains('is-open')) closeLightbox();
        });
        return overlay;
    }

    function openLightbox(src, alt, round) {
        if (!src) return;
        var overlay = ensureLightbox();
        var image = overlay.querySelector('img');
        image.src = src;
        image.alt = alt || '';
        overlay.classList.toggle('is-round', !!round);
        overlay.classList.add('is-open');
        document.body.style.overflow = 'hidden';
    }

    function ensureEditor() {
        var editor = document.getElementById('gbAvatarEditor');
        if (editor) return editor;

        editor = el('div', 'gb-avatar-editor');
        editor.id = 'gbAvatarEditor';
        editor.setAttribute('hidden', '');
        editor.innerHTML =
            '<div class="gb-avatar-editor-bar">' +
            '  <button type="button" class="gb-avatar-editor-btn" data-gb-editor-cancel>Отмена</button>' +
            '  <div class="gb-avatar-editor-title">Фото профиля</div>' +
            '  <button type="button" class="gb-avatar-editor-btn is-primary" data-gb-editor-done>Готово</button>' +
            '</div>' +
            '<div class="gb-avatar-editor-stage">' +
            '  <div class="gb-avatar-editor-canvas-wrap">' +
            '    <img class="gb-avatar-editor-img" alt="" draggable="false" />' +
            '    <div class="gb-avatar-editor-mask" aria-hidden="true"></div>' +
            '  </div>' +
            '  <p class="gb-avatar-editor-hint">Перетащите и масштабируйте жестом / колёсиком</p>' +
            '</div>';
        document.body.appendChild(editor);
        return editor;
    }

    function openAvatarEditor(file, onDone) {
        var editor = ensureEditor();
        var img = editor.querySelector('.gb-avatar-editor-img');
        var wrap = editor.querySelector('.gb-avatar-editor-canvas-wrap');
        var state = { scale: 1, x: 0, y: 0, minScale: 1, dragging: false, startX: 0, startY: 0, originX: 0, originY: 0 };
        var objectUrl = URL.createObjectURL(file);

        function applyTransform() {
            img.style.transform = 'translate(-50%, -50%) translate(' + state.x + 'px,' + state.y + 'px) scale(' + state.scale + ')';
        }

        function fit() {
            var box = wrap.getBoundingClientRect();
            var nw = img.naturalWidth || 1;
            var nh = img.naturalHeight || 1;
            var side = Math.min(box.width, box.height);
            state.minScale = Math.max(side / nw, side / nh);
            state.scale = state.minScale;
            state.x = 0;
            state.y = 0;
            applyTransform();
        }

        function closeEditor() {
            editor.classList.remove('is-open');
            setTimeout(function () { editor.setAttribute('hidden', ''); }, 160);
            try { URL.revokeObjectURL(objectUrl); } catch (_) {}
            document.body.style.overflow = '';
        }

        function exportCrop() {
            var box = wrap.getBoundingClientRect();
            var side = Math.min(box.width, box.height);
            var out = 512;
            var canvas = document.createElement('canvas');
            canvas.width = out;
            canvas.height = out;
            var ctx = canvas.getContext('2d');
            if (!ctx) {
                onDone(file);
                closeEditor();
                return;
            }

            var nw = img.naturalWidth || 1;
            var nh = img.naturalHeight || 1;
            var drawnW = nw * state.scale;
            var drawnH = nh * state.scale;
            var left = (box.width / 2) + state.x - drawnW / 2;
            var top = (box.height / 2) + state.y - drawnH / 2;
            var maskLeft = (box.width - side) / 2;
            var maskTop = (box.height - side) / 2;

            var sx = (maskLeft - left) / state.scale;
            var sy = (maskTop - top) / state.scale;
            var sw = side / state.scale;
            var sh = side / state.scale;

            ctx.fillStyle = '#fff';
            ctx.fillRect(0, 0, out, out);
            try {
                ctx.drawImage(img, sx, sy, sw, sh, 0, 0, out, out);
            } catch (_) {
                onDone(file);
                closeEditor();
                return;
            }

            canvas.toBlob(function (blob) {
                if (!blob) {
                    onDone(file);
                    closeEditor();
                    return;
                }
                try {
                    onDone(new File([blob], 'avatar.jpg', { type: 'image/jpeg', lastModified: Date.now() }));
                } catch (_) {
                    onDone(file);
                }
                closeEditor();
            }, 'image/jpeg', 0.9);
        }

        img.onload = function () {
            fit();
            editor.removeAttribute('hidden');
            requestAnimationFrame(function () { editor.classList.add('is-open'); });
            document.body.style.overflow = 'hidden';
        };
        img.src = objectUrl;

        editor.querySelector('[data-gb-editor-cancel]').onclick = closeEditor;
        editor.querySelector('[data-gb-editor-done]').onclick = exportCrop;

        wrap.onpointerdown = function (e) {
            state.dragging = true;
            state.startX = e.clientX;
            state.startY = e.clientY;
            state.originX = state.x;
            state.originY = state.y;
            wrap.setPointerCapture(e.pointerId);
        };
        wrap.onpointermove = function (e) {
            if (!state.dragging) return;
            state.x = state.originX + (e.clientX - state.startX);
            state.y = state.originY + (e.clientY - state.startY);
            applyTransform();
        };
        wrap.onpointerup = wrap.onpointercancel = function () { state.dragging = false; };
        wrap.onwheel = function (e) {
            e.preventDefault();
            var next = state.scale * (e.deltaY > 0 ? 0.92 : 1.08);
            state.scale = Math.max(state.minScale, Math.min(state.minScale * 4, next));
            applyTransform();
        };
    }

    function setInputFile(input, file) {
        if (!input || !file) return false;
        try {
            var dt = new DataTransfer();
            dt.items.add(file);
            input.files = dt.files;
            return true;
        } catch (_) {
            return false;
        }
    }

    function bindAvatarPicker(form) {
        if (!form || form.__gbAvatarBound) return;
        form.__gbAvatarBound = true;

        var input = form.querySelector('input[type="file"][name="avatar"], #avatarInput');
        var preview = form.querySelector('#avatarPreview') || form.querySelector('img.tg-card-avatar, img.gb-account-card-avatar');
        var fallback = form.querySelector('#avatarFallback');
        var errBox = document.getElementById('avatarClientError');
        var cameraBtn = form.querySelector('[data-avatar-change], .profile-avatar-camera');
        var viewBtn = form.querySelector('.profile-avatar-view');

        function showError(msg) {
            if (!errBox) return;
            errBox.textContent = msg || '';
            errBox.classList.toggle('d-none', !msg);
        }

        function hasAvatar() {
            return form.getAttribute('data-has-avatar') === '1' || form.getAttribute('data-has-avatar') === 'true';
        }

        function avatarUrl() {
            return form.getAttribute('data-avatar-url') || (preview && preview.getAttribute('src')) || '';
        }

        function openGallery(capture) {
            if (!input) return;
            if (capture) input.setAttribute('capture', 'environment');
            else input.removeAttribute('capture');
            input.setAttribute('accept', 'image/*');
            input.click();
        }

        function removeAvatar() {
            var removeUrl = form.getAttribute('data-remove-url');
            if (!removeUrl) return;
            var token = form.querySelector('input[name="__RequestVerificationToken"]');
            var del = document.createElement('form');
            del.method = 'post';
            del.action = removeUrl;
            if (token) {
                var t = document.createElement('input');
                t.type = 'hidden';
                t.name = '__RequestVerificationToken';
                t.value = token.value;
                del.appendChild(t);
            }
            document.body.appendChild(del);
            del.submit();
        }

        function openMenu() {
            var actions = [];
            if (hasAvatar() && avatarUrl()) {
                actions.push({
                    label: 'Открыть фото',
                    onClick: function () { openLightbox(avatarUrl(), '', true); }
                });
            }
            actions.push({
                label: 'Выбрать фото',
                onClick: function () { openGallery(false); }
            });
            actions.push({
                label: 'Сделать снимок',
                onClick: function () { openGallery(true); }
            });
            if (hasAvatar() && form.getAttribute('data-remove-url')) {
                actions.push({
                    label: 'Удалить фото',
                    danger: true,
                    onClick: removeAvatar
                });
            } else if (hasAvatar()) {
                var removeCheck = form.querySelector('input[name="RemoveAvatar"]');
                if (removeCheck) {
                    actions.push({
                        label: 'Удалить фото',
                        danger: true,
                        onClick: function () {
                            removeCheck.checked = true;
                            if (preview) {
                                preview.classList.add('d-none');
                                preview.removeAttribute('src');
                            }
                            if (fallback) fallback.classList.remove('d-none');
                            form.setAttribute('data-has-avatar', '0');
                        }
                    });
                }
            }
            openSheet('Фото профиля', actions);
        }

        if (cameraBtn) {
            cameraBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                openMenu();
            });
        }

        if (viewBtn) {
            viewBtn.removeAttribute('data-avatar-zoom');
            viewBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                openMenu();
            });
        } else if (preview && !hasAvatar()) {
            preview.addEventListener('click', function (e) {
                e.preventDefault();
                openMenu();
            });
        }

        if (fallback) {
            fallback.style.cursor = 'pointer';
            fallback.addEventListener('click', function (e) {
                e.preventDefault();
                openMenu();
            });
        }

        if (!input) return;

        input.addEventListener('change', function () {
            var file = input.files && input.files[0];
            showError('');
            if (!file) return;

            openAvatarEditor(file, function (ready) {
                if (!setInputFile(input, ready)) {
                    // keep original selection
                }
                if (preview) {
                    try {
                        preview.src = URL.createObjectURL(ready);
                        preview.classList.remove('d-none');
                        if (fallback) fallback.classList.add('d-none');
                    } catch (_) { /* ignore */ }
                }
                if (form.getAttribute('data-avatar-autosubmit') === '1') {
                    form.submit();
                }
            });
        });
    }

    document.querySelectorAll('form.profile-avatar-picker, form[data-gb-avatar]').forEach(bindAvatarPicker);

    document.addEventListener('click', function (e) {
        var zoom = e.target.closest('[data-avatar-zoom], [data-gb-zoom]');
        if (!zoom) return;
        // Avatar pickers handle their own menu
        if (zoom.closest('form.profile-avatar-picker, form[data-gb-avatar]')) return;
        e.preventDefault();
        var src = zoom.getAttribute('data-avatar-zoom') || zoom.getAttribute('data-gb-zoom') || (zoom.querySelector('img') || zoom).getAttribute('src');
        openLightbox(src, (zoom.querySelector('img') || zoom).alt || '', zoom.hasAttribute('data-avatar-zoom-round'));
    });

    function ensureAttachPicker() {
        var root = document.getElementById('gbAttachPicker');
        if (root) return root;

        root = el('div', 'gb-attach-picker');
        root.id = 'gbAttachPicker';
        root.setAttribute('hidden', '');
        root.innerHTML =
            '<div class="gb-attach-picker-backdrop" data-gb-attach-close></div>' +
            '<div class="gb-attach-picker-panel" role="dialog" aria-modal="true" aria-label="Галерея">' +
            '  <div class="gb-attach-picker-handle" aria-hidden="true"></div>' +
            '  <div class="gb-attach-picker-tabs">' +
            '    <button type="button" class="gb-attach-tab is-active" data-gb-attach-tab="gallery">Галерея</button>' +
            '    <button type="button" class="gb-attach-tab" data-gb-attach-tab="file">Файл</button>' +
            '  </div>' +
            '  <div class="gb-attach-pane is-active" data-gb-attach-pane="gallery">' +
            '    <div class="gb-attach-grid" id="gbAttachGrid"></div>' +
            '  </div>' +
            '  <div class="gb-attach-pane" data-gb-attach-pane="file">' +
            '    <button type="button" class="gb-attach-file-btn" id="gbAttachPickDoc">' +
            '      <span class="gb-attach-file-btn-title">Документ</span>' +
            '      <span class="gb-attach-file-btn-sub">PDF до 5 МБ</span>' +
            '    </button>' +
            '    <div class="gb-attach-file-list" id="gbAttachFileList"></div>' +
            '  </div>' +
            '  <div class="gb-attach-picker-foot">' +
            '    <div class="gb-attach-picker-count" id="gbAttachCount">Ничего не выбрано</div>' +
            '    <button type="button" class="btn btn-gb-primary gb-attach-confirm" id="gbAttachConfirm" disabled>Прикрепить</button>' +
            '  </div>' +
            '  <input type="file" id="gbAttachMediaInput" accept="image/*,video/*" multiple hidden />' +
            '  <input type="file" id="gbAttachCameraInput" accept="image/*,video/*" capture="environment" hidden />' +
            '  <input type="file" id="gbAttachDocInput" accept=".pdf,application/pdf" hidden />' +
            '</div>';
        document.body.appendChild(root);
        return root;
    }

    function openAttachPicker(options) {
        options = options || {};
        var onConfirm = typeof options.onConfirm === 'function' ? options.onConfirm : function () {};
        var maxItems = options.maxItems || 12;
        var root = ensureAttachPicker();
        var grid = root.querySelector('#gbAttachGrid');
        var fileList = root.querySelector('#gbAttachFileList');
        var countEl = root.querySelector('#gbAttachCount');
        var confirmBtn = root.querySelector('#gbAttachConfirm');
        var mediaInput = root.querySelector('#gbAttachMediaInput');
        var cameraInput = root.querySelector('#gbAttachCameraInput');
        var docInput = root.querySelector('#gbAttachDocInput');
        var items = []; // { id, file, url, selected, kind: image|video|file }

        function revokeAll() {
            items.forEach(function (it) {
                if (it.url) {
                    try { URL.revokeObjectURL(it.url); } catch (_) {}
                }
            });
        }

        function selectedFiles() {
            return items.filter(function (it) { return it.selected; }).map(function (it) { return it.file; });
        }

        function syncCount() {
            var n = selectedFiles().length;
            countEl.textContent = n === 0 ? 'Ничего не выбрано' : ('Выбрано: ' + n);
            confirmBtn.disabled = n === 0;
            confirmBtn.textContent = n === 0 ? 'Прикрепить' : ('Прикрепить ' + n);
        }

        function setTab(name) {
            root.querySelectorAll('[data-gb-attach-tab]').forEach(function (tab) {
                tab.classList.toggle('is-active', tab.getAttribute('data-gb-attach-tab') === name);
            });
            root.querySelectorAll('[data-gb-attach-pane]').forEach(function (pane) {
                pane.classList.toggle('is-active', pane.getAttribute('data-gb-attach-pane') === name);
            });
        }

        function addFiles(fileListLike, forceSelect) {
            var list = Array.prototype.slice.call(fileListLike || []);
            list.forEach(function (file) {
                if (!file || items.length >= maxItems) return;
                var exists = items.some(function (it) {
                    return it.file.name === file.name && it.file.size === file.size && it.file.lastModified === file.lastModified;
                });
                if (exists) return;
                var kind = file.type && file.type.indexOf('video/') === 0
                    ? 'video'
                    : (file.type && file.type.indexOf('image/') === 0 ? 'image' : 'file');
                var url = (kind === 'image' || kind === 'video') ? URL.createObjectURL(file) : null;
                items.push({
                    id: String(Date.now()) + '-' + Math.random().toString(36).slice(2, 7),
                    file: file,
                    url: url,
                    selected: forceSelect !== false,
                    kind: kind
                });
            });
            render();
        }

        function renderGallery() {
            grid.innerHTML = '';

            var cameraTile = el('button', 'gb-attach-tile gb-attach-tile-action');
            cameraTile.type = 'button';
            cameraTile.innerHTML = '<span class="gb-attach-tile-icon" aria-hidden="true">' +
                '<svg width="26" height="26" viewBox="0 0 24 24" fill="none"><path d="M4 8h3l1.5-2h7L17 8h3a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-8a2 2 0 0 1 2-2z" stroke="currentColor" stroke-width="1.7"/><circle cx="12" cy="14" r="3.1" stroke="currentColor" stroke-width="1.7"/></svg>' +
                '</span><span>Камера</span>';
            cameraTile.addEventListener('click', function () { cameraInput.click(); });
            grid.appendChild(cameraTile);

            var galleryTile = el('button', 'gb-attach-tile gb-attach-tile-action');
            galleryTile.type = 'button';
            galleryTile.innerHTML = '<span class="gb-attach-tile-icon" aria-hidden="true">' +
                '<svg width="26" height="26" viewBox="0 0 24 24" fill="none"><rect x="3.5" y="5" width="17" height="14" rx="2.5" stroke="currentColor" stroke-width="1.7"/><circle cx="9" cy="10.5" r="1.6" fill="currentColor"/><path d="M4.5 17l4.2-4.2a1.2 1.2 0 0 1 1.6 0L14 16.5l1.7-1.7a1.2 1.2 0 0 1 1.6 0L19.5 17" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>' +
                '</span><span>Галерея</span>';
            galleryTile.addEventListener('click', function () { mediaInput.click(); });
            grid.appendChild(galleryTile);

            items.filter(function (it) { return it.kind !== 'file'; }).forEach(function (it) {
                var tile = el('button', 'gb-attach-tile' + (it.selected ? ' is-selected' : ''));
                tile.type = 'button';
                tile.setAttribute('aria-pressed', it.selected ? 'true' : 'false');
                if (it.kind === 'video') {
                    tile.innerHTML =
                        '<video class="gb-attach-tile-media" src="' + it.url + '" muted playsinline preload="metadata"></video>' +
                        '<span class="gb-attach-tile-badge">VIDEO</span>' +
                        (it.selected ? '<span class="gb-attach-tile-check" aria-hidden="true"></span>' : '');
                } else {
                    tile.innerHTML =
                        '<span class="gb-attach-tile-media" style="background-image:url(\'' + it.url + '\')"></span>' +
                        (it.selected ? '<span class="gb-attach-tile-check" aria-hidden="true"></span>' : '');
                }
                tile.addEventListener('click', function () {
                    it.selected = !it.selected;
                    render();
                });
                grid.appendChild(tile);
            });
        }

        function renderFiles() {
            fileList.innerHTML = '';
            var docs = items.filter(function (it) { return it.kind === 'file'; });
            if (!docs.length) {
                fileList.innerHTML = '<p class="gb-attach-file-empty">Выберите PDF из файлов устройства</p>';
                return;
            }
            docs.forEach(function (it) {
                var row = el('button', 'gb-attach-file-row' + (it.selected ? ' is-selected' : ''));
                row.type = 'button';
                row.innerHTML =
                    '<span class="gb-attach-file-row-icon">PDF</span>' +
                    '<span class="gb-attach-file-row-meta">' +
                    '  <span class="gb-attach-file-row-name"></span>' +
                    '  <span class="gb-attach-file-row-size"></span>' +
                    '</span>' +
                    (it.selected ? '<span class="gb-attach-tile-check" aria-hidden="true"></span>' : '');
                row.querySelector('.gb-attach-file-row-name').textContent = it.file.name || 'document.pdf';
                row.querySelector('.gb-attach-file-row-size').textContent = Math.max(1, Math.round(it.file.size / 1024)) + ' КБ';
                row.addEventListener('click', function () {
                    it.selected = !it.selected;
                    render();
                });
                fileList.appendChild(row);
            });
        }

        function render() {
            renderGallery();
            renderFiles();
            syncCount();
        }

        function closePicker() {
            root.classList.remove('is-open');
            setTimeout(function () {
                root.setAttribute('hidden', '');
                revokeAll();
                items = [];
                mediaInput.value = '';
                cameraInput.value = '';
                docInput.value = '';
                render();
            }, 180);
            document.body.style.overflow = '';
        }

        root.querySelectorAll('[data-gb-attach-tab]').forEach(function (tab) {
            tab.onclick = function () { setTab(tab.getAttribute('data-gb-attach-tab')); };
        });
        root.querySelectorAll('[data-gb-attach-close]').forEach(function (btn) {
            btn.onclick = closePicker;
        });
        root.querySelector('#gbAttachPickDoc').onclick = function () { docInput.click(); };
        mediaInput.onchange = function () {
            addFiles(mediaInput.files, true);
            mediaInput.value = '';
            setTab('gallery');
        };
        cameraInput.onchange = function () {
            addFiles(cameraInput.files, true);
            cameraInput.value = '';
            setTab('gallery');
        };
        docInput.onchange = function () {
            addFiles(docInput.files, true);
            docInput.value = '';
            setTab('file');
        };
        confirmBtn.onclick = function () {
            var files = selectedFiles();
            if (!files.length) return;
            // Keep object URLs only if caller needs them — pass raw Files
            var copy = files.slice();
            closePicker();
            onConfirm(copy);
        };

        revokeAll();
        items = [];
        setTab('gallery');
        render();
        root.removeAttribute('hidden');
        requestAnimationFrame(function () { root.classList.add('is-open'); });
        document.body.style.overflow = 'hidden';

        // Immediately open gallery on mobile for TG-like speed — still show our UI behind/after
        // Don't auto-open native picker: user sees our grid first with Camera + Gallery tiles.
    }

    window.GbMedia = {
        openSheet: openSheet,
        closeSheet: closeSheet,
        openLightbox: openLightbox,
        openAvatarEditor: openAvatarEditor,
        openAttachPicker: openAttachPicker
    };
})();
