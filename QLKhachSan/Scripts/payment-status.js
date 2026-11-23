(function () {
    'use strict';
    const panel = document.getElementById('automaticPayment');
    if (!panel) return;
    const message = document.getElementById('automaticPaymentMessage');
    let stopped = false, busy = false, timer, failures = 0;
    function stop(text, success) {
        stopped = true;
        clearTimeout(timer);
        message.textContent = text;
        message.style.color = success ? '#15803d' : '#92400e';
        document.querySelectorAll('[data-transfer-instructions]').forEach(el => el.hidden = true);
        const status = document.getElementById('paymentStatus');
        if (success && status) status.textContent = 'Đã thanh toán';
    }
    async function poll() {
        if (stopped || busy) return;
        busy = true;
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 12000);
        try {
            const response = await fetch(panel.dataset.statusUrl, { cache: 'no-store', credentials: 'same-origin', signal: controller.signal });
            if (response.status === 401 || response.status === 403) {
                stop('Phiên truy cập đã hết hạn. Vui lòng đăng nhập và mở lại đơn.', false); return;
            }
            if (!response.ok) throw new Error('Temporary status error');
            const result = await response.json();
            if (!result.success) throw new Error('Status unavailable');
            failures = 0;
            if (result.isPaid) {
                stop('Thanh toán thành công! Hệ thống đã ghi nhận tiền. Đơn vẫn được xử lý theo quy trình xác nhận của nhân viên.', true);
                if (panel.dataset.successUrl) window.location.replace(panel.dataset.successUrl);
                return;
            }
            if (!result.canPay) { stop('Đơn không còn nhận chuyển khoản riêng. Vui lòng kiểm tra lịch sử đơn hoặc liên hệ nhân viên.', false); return; }
            if (Number(result.outstandingAmount) !== Number(panel.dataset.amount)) {
                stop('Số tiền của đơn đã thay đổi. Vui lòng tải lại trang để lấy mã QR mới; không chuyển theo mã QR cũ.', false); return;
            }
            if (result.needsReview) {
                document.querySelectorAll('[data-transfer-instructions]').forEach(el => el.hidden = true);
                message.textContent = 'Có giao dịch cần đối soát cho đơn này. Vui lòng liên hệ nhân viên, không chuyển thêm tiền. Trang sẽ tiếp tục kiểm tra kết quả.';
                return;
            }
            message.textContent = panel.dataset.enabled === 'true'
                ? 'Đang chờ giao dịch ngân hàng. Chuyển đúng số tiền và giữ nguyên nội dung; hệ thống sẽ tự cập nhật, không cần bấm xác nhận.'
                : 'Thanh toán tự động chưa được kích hoạt. Liên hệ nhân viên để được hỗ trợ xác minh.';
        } catch (error) {
            failures++;
            message.textContent = 'Chưa kết nối được để kiểm tra thanh toán. Hệ thống sẽ thử lại; không chuyển tiền lần nữa nếu đã chuyển.';
        } finally {
            clearTimeout(timeout);
            busy = false;
            if (!stopped) timer = setTimeout(poll, Math.min(30000, 4000 * Math.pow(2, Math.min(failures, 3))));
        }
    }
    window.addEventListener('pageshow', function (event) { if (event.persisted) { stopped = false; clearTimeout(timer); poll(); } });
    document.addEventListener('visibilitychange', function () { if (!document.hidden && !stopped) { clearTimeout(timer); poll(); } });
    poll();
}());
