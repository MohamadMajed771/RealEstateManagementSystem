// Live in-app notifications: bumps the bell counter and shows a toast.
(function () {
    if (typeof signalR === 'undefined' || !document.body.dataset.signedIn) return;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/notifications')
        .withAutomaticReconnect()
        .build();

    function bumpBell() {
        const bell = document.getElementById('notificationBell');
        if (!bell) return;
        let badge = bell.querySelector('.badge');
        if (!badge) {
            badge = document.createElement('span');
            badge.className = 'badge rounded-pill bg-danger ms-1';
            badge.textContent = '0';
            bell.append(badge);
        }
        badge.textContent = String((parseInt(badge.textContent, 10) || 0) + 1);
    }

    function toast(n) {
        let area = document.getElementById('toastArea');
        if (!area) {
            area = document.createElement('div');
            area.id = 'toastArea';
            area.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            area.style.zIndex = 1080;
            document.body.append(area);
        }
        const el = document.createElement('div');
        el.className = 'toast';
        el.setAttribute('role', 'status');

        const header = document.createElement('div');
        header.className = 'toast-header';
        const strong = document.createElement('strong');
        strong.className = 'me-auto';
        strong.textContent = n.title;
        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'btn-close';
        close.setAttribute('data-bs-dismiss', 'toast');
        header.append(strong, close);

        const body = document.createElement('div');
        body.className = 'toast-body';
        body.textContent = n.message;
        if (n.url && n.url.startsWith('/')) {
            body.style.cursor = 'pointer';
            body.addEventListener('click', () => { window.location.href = n.url; });
        }

        el.append(header, body);
        area.append(el);
        if (window.bootstrap) new bootstrap.Toast(el, { delay: 8000 }).show();
        el.addEventListener('hidden.bs.toast', () => el.remove());
    }

    connection.on('notification', n => { bumpBell(); toast(n); });
    connection.start().catch(() => { /* live updates are optional */ });
})();
