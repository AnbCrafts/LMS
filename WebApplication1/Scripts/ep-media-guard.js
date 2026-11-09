/**
 * ep-media-guard.js
 * Client-Side DRM & Media Protection Suite for EduPulse LMS
 * Protects course PDFs, videos, and images against local downloading, saving, ripping, and printing.
 */

(function () {
    'use strict';

    var toastTimeout = null;

    function showProtectionToast(message) {
        var existingToast = document.getElementById('epDrmToast');
        if (!existingToast) {
            existingToast = document.createElement('div');
            existingToast.id = 'epDrmToast';
            existingToast.className = 'ep-drm-toast';
            existingToast.innerHTML = '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#f59e0b" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect><path d="M7 11V7a5 5 0 0 1 10 0v4"></path></svg><span id="epDrmToastMsg"></span>';
            document.body.appendChild(existingToast);
        }

        var msgEl = document.getElementById('epDrmToastMsg');
        if (msgEl) {
            msgEl.textContent = message || 'Protected Course Material: Downloading or saving to local storage is disabled.';
        }

        existingToast.classList.add('show');

        if (toastTimeout) {
            clearTimeout(toastTimeout);
        }
        toastTimeout = setTimeout(function () {
            existingToast.classList.remove('show');
        }, 2600);
    }

    // Expose toast function globally
    window.epShowProtectionToast = showProtectionToast;

    // 1. Intercept Right-Click (Context Menu) on Media Elements
    document.addEventListener('contextmenu', function (e) {
        var target = e.target;
        if (!target) return;

        var isProtectedMedia = target.matches('img, video, canvas, source') ||
            target.closest('video, canvas, .ep-pdf-viewer-card, .secure-media-container, #pdfViewportContainer, [data-protected-media="true"]');

        if (isProtectedMedia) {
            e.preventDefault();
            e.stopPropagation();
            showProtectionToast('Protected Course Media: Right-click saving is disabled to protect intellectual property.');
            return false;
        }
    }, true);

    // 2. Prevent Drag & Drop of Images, Videos, and Canvas
    document.addEventListener('dragstart', function (e) {
        var target = e.target;
        if (!target) return;

        var isProtectedMedia = target.matches('img, video, canvas, a[href$=".pdf"]') ||
            target.closest('video, canvas, .ep-pdf-viewer-card, .secure-media-container');

        if (isProtectedMedia) {
            e.preventDefault();
            e.stopPropagation();
            showProtectionToast('Protected Asset: Drag-and-drop export is disabled.');
            return false;
        }
    }, true);

    // 3. Block Save and Print Keyboard Shortcuts
    document.addEventListener('keydown', function (e) {
        // Ctrl+S or Cmd+S (Save As)
        if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S')) {
            e.preventDefault();
            e.stopPropagation();
            showProtectionToast('Save Disabled: Offline local saving of this course page is prohibited.');
            return false;
        }

        // Ctrl+P or Cmd+P (Print to PDF)
        if ((e.ctrlKey || e.metaKey) && (e.key === 'p' || e.key === 'P')) {
            e.preventDefault();
            e.stopPropagation();
            showProtectionToast('Print Prohibited: Exporting course materials to PDF is disabled.');
            return false;
        }
    }, true);

    // 4. Sanitize HTML5 Video Elements
    function sanitizeVideoPlayers() {
        var videos = document.querySelectorAll('video');
        videos.forEach(function (v) {
            v.setAttribute('controlsList', 'nodownload nofullscreen noremoteplayback');
            v.setAttribute('disablePictureInPicture', 'true');
            v.setAttribute('oncontextmenu', 'return false;');
            v.classList.add('secure-media-element');
        });
    }

    // 5. Override Canvas Data Export for DRM Protection
    try {
        var originalToDataURL = HTMLCanvasElement.prototype.toDataURL;
        var originalToBlob = HTMLCanvasElement.prototype.toBlob;

        HTMLCanvasElement.prototype.toDataURL = function () {
            if (this.id === 'pdfViewerCanvas' || this.classList.contains('secure-canvas')) {
                console.warn('[EduPulse DRM] Direct canvas data export blocked.');
                return 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=';
            }
            return originalToDataURL.apply(this, arguments);
        };

        HTMLCanvasElement.prototype.toBlob = function () {
            if (this.id === 'pdfViewerCanvas' || this.classList.contains('secure-canvas')) {
                console.warn('[EduPulse DRM] Direct canvas blob export blocked.');
                var callback = arguments[0];
                if (typeof callback === 'function') {
                    callback(new Blob([], { type: 'image/png' }));
                }
                return;
            }
            return originalToBlob.apply(this, arguments);
        };
    } catch (err) {
        // Ignore in environments where canvas prototype is sealed
    }

    // 6. Enforce on DOM Ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', sanitizeVideoPlayers);
    } else {
        sanitizeVideoPlayers();
    }

    // Run again periodically to sanitize dynamically loaded lesson modules
    setInterval(sanitizeVideoPlayers, 1500);

})();
