// Smart Hotel — client-side helpers (no framework required)
(function () {
    'use strict';

    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) =>
        ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const money = (v) => '$' + Number(v || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    window.smartHotel = {
        esc,
        money,
        csrf: () => document.querySelector('meta[name="csrf-token"]')?.content ?? ''
    };

    // ---- Sidebar (mobile) ----
    const sidebar = document.querySelector('.sidebar');
    const backdrop = document.querySelector('.sidebar-backdrop');
    const toggleSidebar = (open) => {
        sidebar?.classList.toggle('show', open);
        backdrop?.classList.toggle('show', open);
    };
    document.querySelectorAll('[data-sidebar-toggle]').forEach((b) =>
        b.addEventListener('click', () => toggleSidebar(!sidebar?.classList.contains('show'))));
    document.querySelectorAll('[data-sidebar-close]').forEach((b) => b.addEventListener('click', () => toggleSidebar(false)));

    // ---- Auto-hide success alerts ----
    document.querySelectorAll('.alert[data-autohide]').forEach((a) =>
        setTimeout(() => bootstrap.Alert.getOrCreateInstance(a).close(), 6000));

    // ---- Confirmation dialog for destructive forms: <form data-confirm="..."> ----
    const modalEl = document.getElementById('confirmModal');
    if (modalEl) {
        const modal = new bootstrap.Modal(modalEl);
        let pending = null;
        document.addEventListener('submit', (e) => {
            const form = e.target;
            if (!(form instanceof HTMLFormElement) || !form.dataset.confirm || form.dataset.confirmed === '1') return;
            e.preventDefault();
            pending = form;
            modalEl.querySelector('[data-confirm-title]').textContent = form.dataset.confirmTitle || 'Please confirm';
            modalEl.querySelector('[data-confirm-message]').textContent = form.dataset.confirm;
            const ok = modalEl.querySelector('[data-confirm-ok]');
            ok.textContent = form.dataset.confirmButton || 'Yes, continue';
            ok.className = 'btn px-4 ' + (form.dataset.confirmClass || 'btn-danger');
            modal.show();
        });
        modalEl.querySelector('[data-confirm-ok]').addEventListener('click', () => {
            if (!pending) return;
            pending.dataset.confirmed = '1';
            modal.hide();
            pending.submit();
        });
    }

    // ---- Booking forms: live availability + price summary ----
    document.querySelectorAll('select[data-room-select]').forEach((select) => {
        const form = select.closest('form');
        const checkIn = form.querySelector('[data-checkin]');
        const checkOut = form.querySelector('[data-checkout]');
        const exclude = select.dataset.excludeBooking || '';
        const countLabel = form.querySelector('[data-room-count]');
        const summary = document.querySelector('[data-booking-summary]');

        const nights = () => {
            if (!checkIn?.value || !checkOut?.value) return 0;
            const diff = (new Date(checkOut.value) - new Date(checkIn.value)) / 86400000;
            return diff > 0 ? Math.round(diff) : 0;
        };

        const updateSummary = () => {
            if (!summary) return;
            const option = select.selectedOptions[0];
            const rate = Number(option?.dataset.price || 0);
            const n = nights();
            const tax = Number(summary.dataset.taxRate || 0);
            summary.querySelector('[data-summary-room]').textContent = option?.value ? option.textContent.split('·')[0].trim() : '—';
            summary.querySelector('[data-summary-nights]').textContent = n;
            summary.querySelector('[data-summary-rate]').textContent = money(rate);
            summary.querySelector('[data-summary-subtotal]').textContent = money(rate * n);
            summary.querySelector('[data-summary-tax]').textContent = money(rate * n * tax);
            summary.querySelector('[data-summary-total]').textContent = money(rate * n * (1 + tax));
        };

        const refresh = async () => {
            // A locked (disabled) room select belongs to an in-house stay: keep the room, only recalculate.
            if (select.disabled || !checkIn?.value || !checkOut?.value || checkOut.value <= checkIn.value) { updateSummary(); return; }
            try {
                const url = `/api/rooms/available?checkIn=${encodeURIComponent(checkIn.value)}&checkOut=${encodeURIComponent(checkOut.value)}&excludeBookingId=${encodeURIComponent(exclude)}`;
                const res = await fetch(url, { headers: { Accept: 'application/json' }, credentials: 'same-origin' });
                if (!res.ok) return;
                const rooms = await res.json();
                const current = select.value;
                select.innerHTML = '<option value="">Select a room…</option>' + rooms.map((r) =>
                    `<option value="${r.id}" data-price="${r.pricePerNight}" data-capacity="${r.capacity}">Room ${esc(r.roomNumber)} · ${esc(r.roomType)} · ${money(r.pricePerNight)}/night</option>`).join('');
                if ([...select.options].some((o) => o.value === current)) select.value = current;
                if (countLabel) countLabel.textContent = `${rooms.length} room(s) available for these dates`;
            } catch { /* keep server-rendered list */ }
            updateSummary();
        };

        checkIn?.addEventListener('change', refresh);
        checkOut?.addEventListener('change', refresh);
        select.addEventListener('change', updateSummary);
        updateSummary();
    });

    // ---- Room form: fill price from selected room type ----
    document.querySelectorAll('select[data-type-select]').forEach((select) => {
        const price = document.querySelector(select.dataset.typeSelect);
        select.addEventListener('change', () => {
            const p = select.selectedOptions[0]?.dataset.price;
            if (price && p) price.value = p;
        });
    });

    // ---- Housekeeping status modal ----
    const statusModal = document.getElementById('statusModal');
    statusModal?.addEventListener('show.bs.modal', (e) => {
        const t = e.relatedTarget;
        if (!t) return;
        statusModal.querySelector('[name="RoomId"]').value = t.dataset.roomId;
        statusModal.querySelector('[data-room-label]').textContent = t.dataset.roomNumber;
        statusModal.querySelector('[data-room-current]').textContent = t.dataset.statusLabel;
        const select = statusModal.querySelector('[name="Status"]');
        select.value = t.dataset.next || t.dataset.status;
        statusModal.querySelector('[data-occupied-warning]').hidden = t.dataset.status !== 'Occupied';
        select.disabled = t.dataset.status === 'Occupied';
        statusModal.querySelector('[type="submit"]').disabled = t.dataset.status === 'Occupied';
        statusModal.querySelector('[name="Note"]').value = '';
    });
})();
