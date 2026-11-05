(function () {
    'use strict';
    const root = document.getElementById('staffRoomMatrix');
    if (!root) return;
    const el = id => document.getElementById(id);
    const grid = el('staffMatrixGrid'), dialog = el('staffRoomDialog');
    let selected = root.dataset.today, weekStart = addDays(selected, -3), rooms = [], request = 0;
    const colors = {
        'Trống': 'bg-emerald-50 border-emerald-300 text-emerald-900',
        'Chờ xác nhận': 'bg-yellow-50 border-yellow-300 text-yellow-900',
        'Đã đặt': 'bg-amber-50 border-amber-300 text-amber-900',
        'Đang ở': 'bg-rose-50 border-rose-300 text-rose-900',
        'Bảo trì': 'bg-gray-100 border-gray-300 text-gray-600',
        'Đã trả phòng': 'bg-blue-50 border-blue-300 text-blue-900',
        'Ngừng hoạt động': 'bg-gray-200 border-gray-400 text-gray-700'
    };
    function addDays(value, days) {
        const parts = value.split('-').map(Number);
        const date = new Date(parts[0], parts[1] - 1, parts[2], 12);
        date.setDate(date.getDate() + days);
        return date.getFullYear() + '-' + String(date.getMonth() + 1).padStart(2, '0') + '-' + String(date.getDate()).padStart(2, '0');
    }
    function node(tag, text, className) {
        const item = document.createElement(tag);
        if (text != null) item.textContent = text;
        if (className) item.className = className;
        return item;
    }
    function renderWeek() {
        el('staffMatrixDate').value = selected;
        el('staffMatrixWeek').replaceChildren();
        const days = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
        for (let i = 0; i < 7; i++) {
            const value = addDays(weekStart, i);
            const parts = value.split('-').map(Number);
            const dayDate = new Date(parts[0], parts[1] - 1, parts[2], 12);
            const dayName = days[dayDate.getDay()];
            const dateDisplay = value.slice(8) + '/' + value.slice(5, 7);

            const button = node('button', null,
                'px-3.5 py-2 rounded-xl text-xs transition flex flex-col items-center min-w-[65px] ' +
                (value === selected ? 'bg-yellow-400 text-gray-950 font-black ring-2 ring-yellow-300 shadow-md scale-105' : 'bg-gray-800 text-gray-200 hover:bg-gray-700 hover:text-white font-bold'));

            button.type = 'button';
            button.dataset.date = value;
            button.setAttribute('aria-pressed', String(value === selected));

            button.appendChild(node('span', dayName, 'text-[10px] uppercase opacity-80'));
            button.appendChild(node('span', dateDisplay, 'text-sm font-black mt-0.5'));

            button.addEventListener('click', () => select(value, false));
            el('staffMatrixWeek').appendChild(button);
        }
    }
    function select(value, resetWeek) {
        if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return;
        selected = value;
        if (resetWeek) weekStart = addDays(value, -3);
        renderWeek(); load();
    }
    function renderRooms() {
        grid.replaceChildren();
        rooms.forEach(room => {
            const button = node('button', null, 'border rounded-xl p-3 text-center transition shadow-sm ' + (colors[room.status] || colors['Ngừng hoạt động']));
            button.type = 'button'; button.dataset.roomId = room.physicalRoomId;
            button.appendChild(node('div', room.roomNumber, 'font-extrabold text-base'));
            button.appendChild(node('div', room.roomTypeName, 'text-xs opacity-75 truncate'));
            button.appendChild(node('div', room.status, 'text-xs font-bold mt-1'));
            button.addEventListener('click', () => showRoom(room.physicalRoomId));
            grid.appendChild(button);
        });
        if (!rooms.length) grid.appendChild(node('p', 'Chưa có dữ liệu phòng vật lý.', 'col-span-full text-gray-500'));
    }
    function bookingLine(booking) {
        return '#' + booking.bookingId + ' · ' + booking.checkInDate + ' → ' + booking.checkOutDate + ' · ' + booking.status;
    }
    function bookingLink(parent) {
        if (root.dataset.bookingsUrl) {
            const link = node('a', 'Mở danh sách đơn cần xử lý', 'block text-blue-700 underline mt-2');
            link.href = root.dataset.bookingsUrl;
            parent.appendChild(link);
        }
    }
    function showRoom(id) {
        const room = rooms.find(item => item.physicalRoomId === id);
        if (!room) { dialog.close(); return; }
        dialog.dataset.roomId = id;
        el('staffRoomTitle').textContent = 'Phòng ' + room.roomNumber;
        const body = el('staffRoomBody'); body.replaceChildren();
        body.appendChild(node('p', room.roomTypeName + ' · ' + selected + ' · ' + room.status));
        if (room.currentBooking) {
            const b = room.currentBooking;
            body.appendChild(node('p', bookingLine(b)));
            body.appendChild(node('p', b.customerName + ' · ' + b.customerPhone));
            body.appendChild(node('p', 'Thanh toán: ' + (b.isPaid ? 'Đã thanh toán đủ' : 'Chưa thanh toán đủ')));
            bookingLink(body);
        }
        if (room.currentMaintenance) {
            const m = room.currentMaintenance;
            body.appendChild(node('p', 'Bảo trì: ' + m.startDate + ' đến trước ' + m.endDate));
            body.appendChild(node('p', m.reason || 'Bảo trì theo lịch'));
        } else if (room.status === 'Bảo trì') body.appendChild(node('p', 'Bảo trì thủ công. Liên hệ quản trị viên để cập nhật.'));
        if (room.upcomingBookings && room.upcomingBookings.length) {
            body.appendChild(node('h5', 'Các đơn sắp tới', 'font-bold pt-2'));
            room.upcomingBookings.forEach(b => body.appendChild(node('p', bookingLine(b))));
        }
        if (!dialog.open) dialog.showModal();
    }
    function renderUnassigned(items) {
        const box = el('staffUnassigned'); box.replaceChildren(); box.hidden = !items.length;
        if (!items.length) return;
        box.appendChild(node('h4', 'Đơn cần xử lý: chưa gán đủ số phòng (mọi ngày)', 'font-bold text-amber-800'));
        items.forEach(b => box.appendChild(node('p', bookingLine(b) + ' · ' + b.roomTypeName + ' · Thiếu ' + b.missingRoomCount + ' phòng')));
        box.appendChild(node('p', 'Các đơn này giữ số lượng phòng khả dụng; sơ đồ chưa gán chúng vào số phòng cụ thể.'));
        bookingLink(box);
    }
    async function load() {
        const current = ++request;
        const openId = dialog.open ? Number(dialog.dataset.roomId) : null;
        const loading = el('staffMatrixLoading'), error = el('staffMatrixError');
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 15000);
        loading.hidden = false; error.hidden = true;
        grid.setAttribute('aria-busy', 'true');
        // Do not leave a previous day's cards clickable while a new day is loading.
        rooms = []; grid.replaceChildren(); el('staffUnassigned').hidden = true;
        if (dialog.open) dialog.close();
        try {
            const response = await fetch(root.dataset.url + '?date=' + encodeURIComponent(selected), { signal: controller.signal, credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' } });
            if (response.status === 401 || response.status === 403) throw Error('Phiên đăng nhập đã hết hạn hoặc bạn không có quyền xem lịch phòng.');
            if (!response.ok) throw Error('Không thể tải lịch phòng. Vui lòng thử lại.');
            if (!/application\/json/i.test(response.headers.get('content-type') || '')) throw Error('Phản hồi không hợp lệ. Vui lòng đăng nhập lại và tải trang.');
            const data = await response.json();
            if (!data.success || !Array.isArray(data.rooms)) throw Error('Không thể tải dữ liệu lịch phòng. Vui lòng thử lại.');
            if (current !== request) return;
            rooms = data.rooms;
            el('staffMatrixDateTitle').textContent = 'Trạng thái ngày ' + data.displayDate + ' · ' + rooms.length + ' phòng';
            renderRooms(); renderUnassigned(data.unassignedBookings || []);
            if (openId !== null) showRoom(openId);
        } catch (ex) {
            if (current !== request) return;
            el('staffMatrixDateTitle').textContent = '';
            error.textContent = ex.name === 'AbortError' ? 'Tải lịch phòng quá lâu. Vui lòng thử lại.' :
                ex instanceof SyntaxError ? 'Dữ liệu lịch phòng không hợp lệ. Vui lòng tải lại.' : ex.message;
            error.hidden = false;
        } finally {
            clearTimeout(timeout);
            if (current === request) { loading.hidden = true; grid.setAttribute('aria-busy', 'false'); }
        }
    }
    root.querySelectorAll('[data-shift]').forEach(button => button.addEventListener('click', () => {
        const days = Number(button.dataset.shift); weekStart = addDays(weekStart, days); select(addDays(selected, days), false);
    }));
    el('staffMatrixToday').addEventListener('click', () => select(root.dataset.today, true));
    el('staffMatrixDate').addEventListener('change', event => select(event.target.value, true));
    el('staffMatrixRefresh').addEventListener('click', load);
    el('staffRoomClose').addEventListener('click', () => dialog.close());
    dialog.addEventListener('click', event => { if (event.target === dialog) dialog.close(); });
    window.addEventListener('focus', load);
    window.addEventListener('pageshow', event => { if (event.persisted) load(); });
    document.addEventListener('visibilitychange', () => { if (!document.hidden) load(); });
    document.addEventListener('staff:booking-updated', load);
    renderWeek(); load();
}());
