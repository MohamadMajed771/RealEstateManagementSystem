// Case chat over SignalR. Messages are inserted with textContent
// (never innerHTML) so user text cannot inject markup.
(function () {
    const root = document.getElementById('caseChat');
    if (!root || typeof signalR === 'undefined') return;

    const me = root.dataset.me;
    const tabs = Array.from(root.querySelectorAll('[data-conversation]'));
    const box = document.getElementById('chatMessages');
    const form = document.getElementById('chatForm');
    const input = document.getElementById('chatInput');
    const send = document.getElementById('chatSend');
    const state = document.getElementById('chatState');
    if (tabs.length === 0) { state.textContent = ''; return; }

    let current = null;
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/case-chat')
        .withAutomaticReconnect()
        .build();

    function fmt(iso) {
        const d = new Date(iso.endsWith('Z') ? iso : iso + 'Z');
        return d.toLocaleString([], { dateStyle: 'short', timeStyle: 'short' });
    }

    function render(m) {
        const mine = m.senderId === me;
        const row = document.createElement('div');
        row.className = 'd-flex mb-2 ' + (mine ? 'justify-content-end' : 'justify-content-start');

        const bubble = document.createElement('div');
        bubble.className = 'p-2 rounded-3 shadow-sm ' + (mine ? 'bg-primary text-white' : 'bg-white border');
        bubble.style.maxWidth = '75%';

        const meta = document.createElement('div');
        meta.className = 'small ' + (mine ? 'text-white-50' : 'text-muted');
        meta.textContent = (mine ? 'You' : m.senderName + ' · ' + m.senderRole) + ' · ' + fmt(m.sentAt);

        const text = document.createElement('div');
        text.style.whiteSpace = 'pre-wrap';
        text.style.wordBreak = 'break-word';
        text.textContent = m.content;

        bubble.append(meta, text);
        row.append(bubble);
        box.append(row);
    }

    function scrollDown() { box.scrollTop = box.scrollHeight; }

    async function open(conversationId) {
        if (current) await connection.invoke('Leave', current).catch(() => { });
        current = conversationId;
        tabs.forEach(t => t.classList.toggle('active', Number(t.dataset.conversation) === conversationId));
        box.textContent = '';
        try {
            const history = await connection.invoke('Join', conversationId);
            if (history.length === 0) {
                const empty = document.createElement('div');
                empty.className = 'text-muted small text-center mt-5';
                empty.textContent = 'No messages yet. Say hello!';
                box.append(empty);
            }
            history.forEach(render);
            scrollDown();
            const badge = tabs.find(t => Number(t.dataset.conversation) === conversationId)?.querySelector('.unread');
            if (badge) badge.remove();
        } catch (e) {
            state.textContent = 'Could not open this conversation.';
        }
    }

    connection.on('message', m => {
        if (m.conversationId !== current) return;
        if (box.querySelector('.text-center.mt-5')) box.textContent = '';
        render(m);
        scrollDown();
        if (m.senderId !== me) connection.invoke('MarkRead', current).catch(() => { });
    });

    connection.onreconnecting(() => { state.textContent = 'reconnecting…'; send.disabled = true; });
    connection.onreconnected(() => { state.textContent = 'live'; send.disabled = false; if (current) open(current); });
    connection.onclose(() => { state.textContent = 'offline — reload the page'; send.disabled = true; });

    tabs.forEach(t => t.addEventListener('click', () => open(Number(t.dataset.conversation))));

    form.addEventListener('submit', async e => {
        e.preventDefault();
        const text = input.value.trim();
        if (!text || !current) return;
        send.disabled = true;
        try {
            await connection.invoke('Send', current, text);
            input.value = '';
        } catch (err) {
            alert(err && err.message ? err.message.replace(/^.*HubException: /, '') : 'Message not sent.');
        } finally {
            send.disabled = false;
            input.focus();
        }
    });

    input.addEventListener('keydown', e => {
        if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); form.requestSubmit(); }
    });

    connection.start()
        .then(() => { state.textContent = 'live'; send.disabled = false; open(Number(tabs[0].dataset.conversation)); })
        .catch(() => { state.textContent = 'offline — reload the page'; });
})();
