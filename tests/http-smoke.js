// Read-only pages and deliberately rejected writes; never creates business records.
const assert = require('assert');
const base = process.env.HOTEL_TEST_URL || 'http://localhost:51180';
if (!/^http:\/\/localhost:\d+$/.test(base)) throw Error('Localhost only');
(async () => {
    let count = 0;
    function check(name, value) { assert(value, name); count++; console.log('PASS: ' + name); }
    const home = await fetch(base + '/');
    check('Home HTTP 200', home.status === 200);
    const cookies = home.headers.getSetCookie().map(c => c.split(';')[0]).join('; ');
    const html = await home.text();
    const match = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
    check('CSRF token rendered', !!match);
    const token = match[1];
    for (const action of ['Login', 'Register']) {
        for (const mode of ['form', 'json']) {
            const response = await fetch(base + '/Account/' + action, { method: 'POST', redirect: 'manual',
                headers: mode === 'form' ? { Cookie: cookies, 'Content-Type': 'application/x-www-form-urlencoded' } :
                    { Cookie: cookies, 'Content-Type': 'application/json', 'X-CSRF-Token': token },
                body: mode === 'form' ? new URLSearchParams({ __RequestVerificationToken: token }).toString() : '{}' });
            check(action + ' ' + mode + ' valid CSRF reaches input validation', response.status === 302);
        }
        const denied = await fetch(base + '/Account/' + action, { method: 'POST', headers: { Cookie: cookies }, redirect: 'manual' });
        check(action + ' missing CSRF rejected', denied.status === 400);
    }
    const food = await fetch(base + '/Restaurant/PlaceOrder', { method: 'POST', headers: { Cookie: cookies,
        'X-CSRF-Token': token, 'Content-Type': 'application/json' }, body: '{}' });
    check('Food POST valid CSRF reaches input validation without creating order', food.status === 200 && !(await food.json()).success);
    for (const url of ['/Admin/SaveRoomMaintenance','/Staff/CreateBooking']) {
        const denied = await fetch(base + url, { method: 'POST', headers: { Cookie: cookies, 'X-CSRF-Token': token }, redirect: 'manual' });
        check(url + ' still requires authenticated role', [401,403].includes(denied.status));
    }
    for (const path of ['/Room', '/Room?isLuxury=true', '/Room/Details/1', '/Restaurant', '/Account/Login', '/Account/Logout', '/Account/Register', '/Home/Register', '/Home/About', '/Home/Contact', '/AIChat']) {
        const response = await fetch(base + path, { headers: { Cookie: cookies } });
        check(path + ' renders', response.status === 200 && !(await response.text()).includes('Server Error in'));
    }
    const start = new Date(); start.setDate(start.getDate() + 90);
    const end = new Date(start); end.setDate(end.getDate() + 2);
    const date = value => value.toISOString().slice(0, 10);
    for (const action of ['GetAvailableRooms', 'GetAvailablePhysicalRooms']) {
        const response = await fetch(base + '/Booking/' + action + '?roomId=1&checkIn=' + date(start) + '&checkOut=' + date(end));
        const json = await response.json();
        check(action + ' HTTP JSON success', response.status === 200 && json.success);
        if (json.physicalRooms) check('Five physical rooms returned for one type', json.physicalRooms.length === 5);
    }
    for (const path of ['/Admin/Rooms', '/Staff/Index', '/Staff/GetPhysicalRoomsMatrixByDate?date=2026-11-07', '/Admin/GetPhysicalRoomsMatrixByDate?date=2026-11-07', '/Restaurant/AdminOrders', '/Booking/Payment/44']) {
        const response = await fetch(base + path, { headers: { Cookie: cookies }, redirect: 'manual' });
        check(path + ' denied anonymously', [401,403,404].includes(response.status));
    }
    const missing = await fetch(base + '/Booking/ConfirmCashPayment/44', { method: 'POST', headers: { Cookie: cookies } });
    check('Missing CSRF token rejected over HTTP', missing.status === 400);
    for (const path of ['/Booking/ConfirmCashPayment/44', '/Booking/SimulateBankTransferPaid/44', '/Booking/RequestBankTransferReview/44']) {
        const response = await fetch(base + path, { method: 'POST', headers: { Cookie: cookies, 'X-CSRF-Token': token } });
        check(path + ' cannot change another booking', response.status === 403);
    }
    const invalid = await fetch(base + '/Booking/Create', { method: 'POST', headers: { Cookie: cookies, 'X-CSRF-Token': token, 'Content-Type': 'application/json' }, body: '{}' });
    check('Invalid booking body rejected without creating data', invalid.status === 200 && !(await invalid.json()).success);
    const csrfBad = await fetch(base + '/Booking/Create', { method: 'POST', headers: { Cookie: cookies, 'X-CSRF-Token': 'tampered', 'Content-Type': 'application/json' }, body: '{}' });
    check('Tampered CSRF token rejected', csrfBad.status === 400);
    console.log('HTTP TOTAL PASSED: ' + count);
})().catch(error => { console.error(error); process.exitCode = 1; });
