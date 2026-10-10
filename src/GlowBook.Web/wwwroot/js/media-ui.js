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

    function hasNativeGalleryBridge() {
        return !!(window.GlowBookAndroid
            && typeof window.GlowBookAndroid.hasNativeGallery === 'function'
            && window.GlowBookAndroid.hasNativeGallery());
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
        var native = hasNativeGalleryBridge();
        // { id, file?, url?, thumbUrl?, itemUrl?, name?, mime?, size?, selected, kind, native? }
        var items = [];
        var galleryStatus = ''; // '', loading, need-permission, empty, error
        var docsStatus = '';
        var confirming = false;

        function revokeAll() {
            items.forEach(function (it) {
                if (it.url && it.url.indexOf('blob:') === 0) {
                    try { URL.revokeObjectURL(it.url); } catch (_) {}
                }
            });
        }

        function selectedItems() {
            return items.filter(function (it) { return it.selected; });
        }

        function syncCount() {
            var n = selectedItems().length;
            countEl.textContent = n === 0 ? 'Ничего не выбрано' : ('Выбрано: ' + n);
            confirmBtn.disabled = n === 0 || confirming;
            confirmBtn.textContent = confirming
                ? 'Загрузка…'
                : (n === 0 ? 'Прикрепить' : ('Прикрепить ' + n));
        }

        function setTab(name) {
            root.querySelectorAll('[data-gb-attach-tab]').forEach(function (tab) {
                tab.classList.toggle('is-active', tab.getAttribute('data-gb-attach-tab') === name);
            });
            root.querySelectorAll('[data-gb-attach-pane]').forEach(function (pane) {
                pane.classList.toggle('is-active', pane.getAttribute('data-gb-attach-pane') === name);
            });
            if (name === 'file' && native && !items.some(function (it) { return it.kind === 'file' && it.native; })) {
                loadNativeDocuments();
            }
        }

        function upsertNative(raw, forceSelect) {
            if (!raw || !raw.id) return;
            var existing = items.find(function (it) { return it.id === raw.id; });
            if (existing) {
                if (forceSelect) existing.selected = true;
                return;
            }
            if (items.filter(function (it) { return it.selected; }).length >= maxItems && !forceSelect) {
                // still show in grid, just not auto-selected
            }
            items.push({
                id: raw.id,
                native: true,
                file: null,
                url: raw.thumbUrl || null,
                thumbUrl: raw.thumbUrl || null,
                itemUrl: raw.itemUrl || null,
                name: raw.name || 'file',
                mime: raw.mime || '',
                size: raw.size || 0,
                selected: !!forceSelect,
                kind: raw.kind || 'image'
            });
        }

        function addFiles(fileListLike, forceSelect) {
            var list = Array.prototype.slice.call(fileListLike || []);
            list.forEach(function (file) {
                if (!file) return;
                var exists = items.some(function (it) {
                    return it.file
                        && it.file.name === file.name
                        && it.file.size === file.size
                        && it.file.lastModified === file.lastModified;
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
                    kind: kind,
                    name: file.name,
                    mime: file.type,
                    size: file.size
                });
            });
            render();
        }

        function parseBridgeJson(raw) {
            try { return JSON.parse(raw || '{}'); } catch (_) { return { items: [], error: 'parse' }; }
        }

        function loadNativeGallery() {
            if (!native) return;
            galleryStatus = 'loading';
            renderGallery();
            if (!window.GlowBookAndroid.hasGalleryPermission()) {
                galleryStatus = 'need-permission';
                renderGallery();
                window.GlowBook = window.GlowBook || {};
                window.GlowBook.onGalleryPermission = function (ok) {
                    if (ok) loadNativeGallery();
                    else {
                        galleryStatus = 'need-permission';
                        renderGallery();
                    }
                };
                window.GlowBookAndroid.requestGalleryPermission();
                return;
            }
            var data = parseBridgeJson(window.GlowBookAndroid.listGallery(0, 100));
            if (data.error === 'permission') {
                galleryStatus = 'need-permission';
                renderGallery();
                return;
            }
            (data.items || []).forEach(function (raw) { upsertNative(raw, false); });
            galleryStatus = (data.items || []).length ? '' : 'empty';
            render();
        }

        function loadNativeDocuments() {
            if (!native) return;
            docsStatus = 'loading';
            renderFiles();
            if (!window.GlowBookAndroid.hasGalleryPermission()) {
                docsStatus = 'need-permission';
                renderFiles();
                window.GlowBook = window.GlowBook || {};
                window.GlowBook.onGalleryPermission = function (ok) {
                    if (ok) loadNativeDocuments();
                    else {
                        docsStatus = 'need-permission';
                        renderFiles();
                    }
                };
                window.GlowBookAndroid.requestGalleryPermission();
                return;
            }
            var data = parseBridgeJson(window.GlowBookAndroid.listDocuments(0, 80));
            if (data.error === 'permission') {
                docsStatus = 'need-permission';
                renderFiles();
                return;
            }
            (data.items || []).forEach(function (raw) { upsertNative(raw, false); });
            docsStatus = '';
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

            if (!native) {
                var galleryTile = el('button', 'gb-attach-tile gb-attach-tile-action');
                galleryTile.type = 'button';
                galleryTile.innerHTML = '<span class="gb-attach-tile-icon" aria-hidden="true">' +
                    '<svg width="26" height="26" viewBox="0 0 24 24" fill="none"><rect x="3.5" y="5" width="17" height="14" rx="2.5" stroke="currentColor" stroke-width="1.7"/><circle cx="9" cy="10.5" r="1.6" fill="currentColor"/><path d="M4.5 17l4.2-4.2a1.2 1.2 0 0 1 1.6 0L14 16.5l1.7-1.7a1.2 1.2 0 0 1 1.6 0L19.5 17" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>' +
                    '</span><span>Галерея</span>';
                galleryTile.addEventListener('click', function () { mediaInput.click(); });
                grid.appendChild(galleryTile);
            }

            if (galleryStatus === 'loading') {
                var loading = el('div', 'gb-attach-file-empty');
                loading.textContent = 'Загрузка галереи…';
                loading.style.gridColumn = '1 / -1';
                grid.appendChild(loading);
            } else if (galleryStatus === 'need-permission') {
                var perm = el('button', 'gb-attach-tile gb-attach-tile-action');
                perm.type = 'button';
                perm.style.gridColumn = 'span 2';
                perm.innerHTML = '<span>Разрешить доступ к фото</span>';
                perm.addEventListener('click', function () {
                    window.GlowBook = window.GlowBook || {};
                    window.GlowBook.onGalleryPermission = function (ok) {
                        if (ok) loadNativeGallery();
                    };
                    window.GlowBookAndroid.requestGalleryPermission();
                });
                grid.appendChild(perm);
            } else if (galleryStatus === 'empty') {
                var empty = el('div', 'gb-attach-file-empty');
                empty.textContent = 'В галерее пока пусто';
                empty.style.gridColumn = '1 / -1';
                grid.appendChild(empty);
            }

            items.filter(function (it) { return it.kind !== 'file'; }).forEach(function (it) {
                var tile = el('button', 'gb-attach-tile' + (it.selected ? ' is-selected' : ''));
                tile.type = 'button';
                tile.setAttribute('aria-pressed', it.selected ? 'true' : 'false');
                var mediaSrc = it.thumbUrl || it.url || '';
                if (it.kind === 'video') {
                    tile.innerHTML =
                        '<span class="gb-attach-tile-media" style="background-image:url(\'' + mediaSrc.replace(/'/g, '%27') + '\')"></span>' +
                        '<span class="gb-attach-tile-badge">VIDEO</span>' +
                        (it.selected ? '<span class="gb-attach-tile-check" aria-hidden="true"></span>' : '');
                } else {
                    tile.innerHTML =
                        '<span class="gb-attach-tile-media" style="background-image:url(\'' + mediaSrc.replace(/'/g, '%27') + '\')"></span>' +
                        (it.selected ? '<span class="gb-attach-tile-check" aria-hidden="true"></span>' : '');
                }
                tile.addEventListener('click', function () {
                    if (!it.selected && selectedItems().length >= maxItems) return;
                    it.selected = !it.selected;
                    render();
                });
                grid.appendChild(tile);
            });
        }

        function renderFiles() {
            fileList.innerHTML = '';
            if (docsStatus === 'loading') {
                fileList.innerHTML = '<p class="gb-attach-file-empty">Загрузка файлов…</p>';
                return;
            }
            if (docsStatus === 'need-permission') {
                var permBtn = el('button', 'gb-attach-file-btn');
                permBtn.type = 'button';
                permBtn.innerHTML = '<span class="gb-attach-file-btn-title">Разрешить доступ</span><span class="gb-attach-file-btn-sub">Чтобы показать PDF с устройства</span>';
                permBtn.addEventListener('click', function () {
                    window.GlowBook = window.GlowBook || {};
                    window.GlowBook.onGalleryPermission = function (ok) {
                        if (ok) loadNativeDocuments();
                    };
                    window.GlowBookAndroid.requestGalleryPermission();
                });
                fileList.appendChild(permBtn);
                return;
            }

            var docs = items.filter(function (it) { return it.kind === 'file'; });
            if (!docs.length) {
                fileList.innerHTML = native
                    ? '<p class="gb-attach-file-empty">PDF на устройстве не найдены — можно выбрать вручную выше</p>'
                    : '<p class="gb-attach-file-empty">Выберите PDF из файлов устройства</p>';
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
                row.querySelector('.gb-attach-file-row-name').textContent = it.name || (it.file && it.file.name) || 'document.pdf';
                var sz = it.size || (it.file && it.file.size) || 0;
                row.querySelector('.gb-attach-file-row-size').textContent = Math.max(1, Math.round(sz / 1024)) + ' КБ';
                row.addEventListener('click', function () {
                    if (!it.selected && selectedItems().length >= maxItems) return;
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
                galleryStatus = '';
                docsStatus = '';
                confirming = false;
                render();
            }, 180);
            document.body.style.overflow = '';
        }

        function materializeSelected(done) {
            var selected = selectedItems();
            if (!selected.length) {
                done([]);
                return;
            }
            var out = [];
            var i = 0;

            function next() {
                if (i >= selected.length) {
                    done(out);
                    return;
                }
                var it = selected[i++];
                if (it.file) {
                    out.push(it.file);
                    next();
                    return;
                }
                if (!it.itemUrl) {
                    next();
                    return;
                }
                fetch(it.itemUrl)
                    .then(function (res) {
                        if (!res.ok) throw new Error('fetch');
                        return res.blob();
                    })
                    .then(function (blob) {
                        out.push(new File([blob], it.name || 'file', { type: it.mime || blob.type || 'application/octet-stream' }));
                        next();
                    })
                    .catch(function () { next(); });
            }
            next();
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
            if (!selectedItems().length || confirming) return;
            confirming = true;
            syncCount();
            materializeSelected(function (files) {
                confirming = false;
                if (!files.length) {
                    syncCount();
                    return;
                }
                closePicker();
                onConfirm(files);
            });
        };

        revokeAll();
        items = [];
        galleryStatus = '';
        docsStatus = '';
        confirming = false;
        setTab('gallery');
        render();
        root.removeAttribute('hidden');
        requestAnimationFrame(function () { root.classList.add('is-open'); });
        document.body.style.overflow = 'hidden';

        if (native) {
            loadNativeGallery();
        }
    }

    window.GbMedia = {
        openSheet: openSheet,
        closeSheet: closeSheet,
        openLightbox: openLightbox,
        openAvatarEditor: openAvatarEditor,
        openAttachPicker: openAttachPicker
    };
})();
