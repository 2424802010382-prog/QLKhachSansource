// Native Chrome DevTools Protocol, no npm install and no writes to business data.
const fs = require('fs'), path = require('path'), cp = require('child_process'), assert = require('assert');
const chrome = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
// Keep Chrome's locked profile files outside MSBuild's intermediate directories.
const profile = path.join(require('os').tmpdir(), 'LuxuryHotel-BrowserRegression-' + process.pid);
fs.mkdirSync(profile, { recursive: true });
const browser = cp.spawn(chrome, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    '--remote-debugging-port=0', '--user-data-dir=' + profile, 'about:blank'], { windowsHide: true, stdio: 'ignore' });
let ws, count = 0;
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
function check(name, value) { assert(value, name); count++; console.log('PASS: ' + name); }
(async () => {
    const activePort = path.join(profile, 'DevToolsActivePort');
    for (let i = 0; i < 100 && !fs.existsSync(activePort); i++) await delay(100);
    const port = fs.readFileSync(activePort, 'utf8').split('\n')[0];
    const targets = await (await fetch('http://localhost:' + port + '/json/list')).json();
    ws = new WebSocket(targets.find(t => t.type === 'page').webSocketDebuggerUrl);
    await new Promise(resolve => ws.addEventListener('open', resolve, { once: true }));
    let id = 0; const pending = new Map(), exceptions = [];
    ws.addEventListener('message', event => {
        const msg = JSON.parse(event.data);
        if (msg.id) { const p = pending.get(msg.id); if (p) { pending.delete(msg.id); msg.error ? p.reject(new Error(JSON.stringify(msg.error))) : p.resolve(msg.result); } }
        if (msg.method === 'Runtime.exceptionThrown') exceptions.push(msg.params.exceptionDetails.exception?.description || msg.params.exceptionDetails.text);
    });
    function send(method, params = {}) {
        return new Promise((resolve, reject) => { const key = ++id; pending.set(key, { resolve, reject }); ws.send(JSON.stringify({ id: key, method, params })); });
    }
    async function evaluate(expression) {
        const result = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
        if (result.exceptionDetails) throw Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
        return result.result.value;
    }
    async function waitFor(expression) {
        for (let i = 0; i < 150; i++) { try { if (await evaluate(expression)) return; } catch (_) {} await delay(100); }
        throw Error('Timed out: ' + expression);
    }
    async function navigate(route) {
        await send('Page.navigate', { url: 'http://localhost:51180' + route });
        await waitFor("document.readyState === 'complete'");
    }
    await send('Page.enable'); await send('Runtime.enable');
    await send('Emulation.setDeviceMetricsOverride', { width: 1366, height: 900, deviceScaleFactor: 1, mobile: false });
    await navigate('/');
    check('Home JavaScript initialized', await evaluate("typeof bookRoom === 'function' && typeof openRoomSelectionModal === 'function' && window.hotelSecurityInstalled"));
    const future = new Date(Date.now() + 90 * 86400000).toISOString().slice(0, 10);
    const departure = new Date(Date.now() + 92 * 86400000).toISOString().slice(0, 10);
    await evaluate(`bookRoom(1, 'Deluxe', 2000000, 2); document.getElementById('checkInDate').value=${JSON.stringify(future)}; document.getElementById('checkOutDate').value=${JSON.stringify(departure)}; document.getElementById('roomCount').value='2'; openRoomSelectionModal();`);
    await waitFor("document.querySelectorAll('#physicalRoomsGrid [data-room-number]').length === 5");
    check('Customer popup loads all five physical rooms from API', true);
    await evaluate("Array.from(document.querySelectorAll('#physicalRoomsGrid [data-booked=false]')).slice(0,2).forEach(el=>el.click()); confirmRoomSelection();");
    check('Multiple selected rooms persist in form', await evaluate("document.getElementById('selectedRoomNumber').value.split(',').filter(Boolean).length === 2"));
    check('Room popup closes after valid selection', await evaluate("document.getElementById('roomSelectionModal').classList.contains('hidden')"));
    await evaluate("document.getElementById('checkInDate').dispatchEvent(new Event('change'));");
    check('Changing dates clears stale room selection', await evaluate("document.getElementById('selectedRoomNumber').value === ''"));
    await send('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true });
    await evaluate('openRoomSelectionModal()');
    await waitFor("document.querySelectorAll('#physicalRoomsGrid [data-room-number]').length === 5");
    check('Room popup renders within mobile viewport', await evaluate("document.getElementById('physicalRoomsGrid').getBoundingClientRect().width <= 390"));
    const screenshot = await send('Page.captureScreenshot', { format: 'png' });
    fs.writeFileSync('QLKhachSan/obj/BrowserRegression-mobile.png', Buffer.from(screenshot.data, 'base64'));
    await evaluate('closeRoomSelectionModal()');
    await navigate('/Restaurant');
    check('Restaurant has no jQuery dependency for room selector', await evaluate("typeof loadMyActiveRooms === 'function' && typeof updateOrderTypeFields === 'function'"));
    await evaluate("const radio=document.querySelector('input[name=orderType][value=\"Giao lên phòng\"]'); radio.checked=true; updateOrderTypeFields();");
    await waitFor("!document.getElementById('orderRoomNumber').textContent.includes('Đang tải')");
    check('Room service selector resolves session error instead of staying loading', await evaluate("document.getElementById('orderRoomNumber').options.length === 1"));

    // Isolated DOM fixture exercises the actual Admin calendar script without granting
    // a real admin session or issuing a successful write to the application database.
    await navigate('/');
    const source = fs.readFileSync('QLKhachSan/Views/Admin/Rooms.cshtml', 'utf8');
    let adminScript = source.slice(source.indexOf('        let currentMatrixDateStr'), source.indexOf('    </script>', source.indexOf('        let currentMatrixDateStr')));
    adminScript = adminScript.replace(/@DateTime\.Today\.AddDays\(-3\)\.ToString\("yyyy-MM-dd"\)/g, future)
        .replace(/@DateTime\.Today\.ToString\("yyyy-MM-dd"\)/g, future)
        .replace(/@Url\.Action\("([^"]+)",\s*"([^"]+)"\)/g, '/$2/$1');
    await evaluate(`document.body.innerHTML = '<div id="sevenDaysBar"></div><input id="adminMatrixDatePicker"><div id="adminMatrixError" class="hidden"></div><div id="adminMatrixLoading" class="hidden"></div><div id="adminMatrixGrid"></div><div id="adminRoomDetailModal" class="modal-hidden"><h2 id="adminModalRoomTitle"></h2><p id="adminModalDateTitle"></p><div id="adminModalBody"></div></div><input name="__RequestVerificationToken" value="isolated-dom-fixture">';
        window.fixtureRooms=Array.from({length:40},(_,i)=>({physicalRoomId:i+1,roomNumber:String((Math.floor(i/5)+1)*100+(i%5)+1),roomTypeName:'Room type',status:'Trống',currentBooking:null,currentMaintenance:null,upcomingBookings:[],maintenanceHistory:[]}));
        window.fixtureRooms[0].roomTypeName='<img src=x onerror="window.hotelXss=1">';
        window.fetch=async function(url,options){ return new Response(JSON.stringify(String(url).includes('SaveRoomMaintenance') ? {success:false,message:'Regression injected failure'} : {success:true,rooms:window.fixtureRooms}),{status:String(url).includes('SaveRoomMaintenance')?500:200,headers:{'Content-Type':'application/json'}}); };
        window.showLuxuryAlert=function(message){window.fixtureNotice=message;};`);
    await evaluate(adminScript);
    await evaluate('renderSevenDaysBar(); loadMatrixDataForDate(currentMatrixDateStr);');
    await waitFor("document.querySelectorAll('#adminMatrixGrid [onclick]').length === 40");
    check('Admin DOM fixture renders 40 room cards and seven dates', await evaluate("document.querySelectorAll('#sevenDaysBar button').length === 7"));
    await evaluate('window.fixtureOriginalDate=currentMatrixDateStr; navigateMatrixWindow(7);');
    check('Seven-days-next changes selected date', await evaluate('currentMatrixDateStr !== window.fixtureOriginalDate'));
    await evaluate('navigateMatrixWindow(-7);');
    check('Seven-days-previous restores window', await evaluate('currentMatrixDateStr === window.fixtureOriginalDate'));
    await evaluate('goToToday();');
    await waitFor('matrixRoomsCache.length === 40');
    check('Today control selects local current date', await evaluate('currentMatrixDateStr === formatDateToYYYYMMDD(new Date())'));
    await evaluate('openAdminRoomDetailModal(0);');
    check('Admin room popup opens in fixture', await evaluate("!document.getElementById('adminRoomDetailModal').classList.contains('modal-hidden')"));
    check('Matrix escapes HTML from room data', await evaluate("!window.hotelXss && document.getElementById('adminMatrixGrid').innerHTML.includes('&lt;img')"));
    await evaluate(`(async function(){ document.getElementById('maintStartDate').value=${JSON.stringify(future)}; document.getElementById('maintEndDate').value=${JSON.stringify(departure)}; document.getElementById('maintReason').value='Regression isolated DOM fixture'; await saveMaintenance(1); }())`);
    check('Maintenance save button exits loading after HTTP failure', await evaluate("!document.querySelector('#adminRoomDetailModal button[onclick^=\"saveMaintenance(\"]').disabled && !document.querySelector('#adminRoomDetailModal button[onclick^=\"saveMaintenance(\"]').textContent.includes('Đang lưu')"));
    await evaluate('closeAdminRoomDetailModal();');
    check('Admin room popup closes in fixture', await evaluate("document.getElementById('adminRoomDetailModal').classList.contains('modal-hidden')"));
    // Exercise the actual Staff markup and shipped JS in an isolated DOM, without
    // creating a production login bypass or writing bookings to the database.
    const staffView = fs.readFileSync('QLKhachSan/Views/Staff/Index.cshtml', 'utf8');
    let staffMarkup = staffView.slice(staffView.indexOf('<section id="staffRoomMatrix"'), staffView.indexOf('</section>', staffView.indexOf('<section id="staffRoomMatrix"')) + 10);
    staffMarkup = staffMarkup.replace(/<section[^>]+>/, '<section id="staffRoomMatrix" data-url="/Staff/GetPhysicalRoomsMatrixByDate" data-today="' + future + '" data-bookings-url="/Staff/Bookings">');
    await evaluate(`document.body.innerHTML=${JSON.stringify(staffMarkup)};
        window.staffResponseMode='ok';window.staffRequestedDates=[];
        window.fixtureRooms[0].status='Chờ xác nhận';
        window.fixtureRooms[0].currentBooking={bookingId:44,customerName:'Fixture guest',customerPhone:'Fixture phone',checkInDate:'07/11/2026',checkOutDate:'09/11/2026',status:'Chờ xác nhận',isPaid:false};
        window.fixtureRooms[1].status='Đang ở';
        window.fixtureRooms[2].status='Bảo trì';window.fixtureRooms[2].currentMaintenance={startDate:'07/11/2026',endDate:'10/11/2026',reason:'Fixture maintenance'};
        window.fetch=async function(url){
            const date=new URL(url,location.origin).searchParams.get('date'); window.staffRequestedDates.push(date);
            if(window.staffResponseMode==='403') return new Response('{}',{status:403,headers:{'Content-Type':'application/json'}});
            if(window.staffResponseMode==='html') return new Response('<html>Login</html>',{headers:{'Content-Type':'text/html'}});
            if(window.staffResponseMode==='json') return new Response('{broken',{headers:{'Content-Type':'application/json'}});
            if(window.staffResponseMode==='500') return new Response('{}',{status:500,headers:{'Content-Type':'application/json'}});
            if(window.staffResponseMode==='abort') throw new DOMException('Fixture timeout','AbortError');
            if(window.staffResponseMode==='network') throw new TypeError('Fixture network failure');
            if(window.staffResponseMode==='queue') return new Promise(resolve=>window.staffPending.push({date,resolve}));
            return new Response(JSON.stringify({success:true,selectedDate:date,displayDate:date,rooms:window.fixtureRooms,unassignedBookings:[{bookingId:99,checkInDate:'07/11/2026',checkOutDate:'09/11/2026',status:'Chờ xác nhận',roomTypeName:'Deluxe',missingRoomCount:1}]}),{headers:{'Content-Type':'application/json'}});
        };`);
    await send('Emulation.setDeviceMetricsOverride', { width: 1366, height: 900, deviceScaleFactor: 1, mobile: false });
    await evaluate(fs.readFileSync('QLKhachSan/Scripts/staff-room-matrix.js', 'utf8'));
    await waitFor("document.querySelectorAll('#staffMatrixGrid button').length===40");
    check('Staff fixture renders 40 cards and seven calendar dates', await evaluate("document.querySelectorAll('#staffMatrixWeek button').length===7"));
    check('Staff displays pending occupied maintenance and empty states', await evaluate("['Chờ xác nhận','Đang ở','Bảo trì','Trống'].every(s=>document.getElementById('staffMatrixGrid').textContent.includes(s))"));
    check('Staff escapes room names and lists unassigned reservations separately', await evaluate("!window.hotelXss && document.getElementById('staffMatrixGrid').textContent.includes('<img') && document.getElementById('staffUnassigned').textContent.includes('#99')"));
    await evaluate("document.querySelector('#staffRoomMatrix [data-shift=\"7\"]').click()");
    await waitFor("document.getElementById('staffMatrixLoading').hidden");
    const staffNextDate = await evaluate("document.getElementById('staffMatrixDate').value");
    check('Staff next seven days requests a new selected date', staffNextDate !== future);
    await evaluate("document.querySelector('#staffRoomMatrix [data-shift=\"-7\"]').click()");
    await waitFor("document.getElementById('staffMatrixLoading').hidden");
    check('Staff previous seven days restores date', await evaluate("document.getElementById('staffMatrixDate').value") === future);
    await evaluate(`document.getElementById('staffMatrixDate').value=${JSON.stringify(departure)};document.getElementById('staffMatrixDate').dispatchEvent(new Event('change'));`);
    await waitFor("document.getElementById('staffMatrixLoading').hidden");
    check('Staff date picker reloads requested date', await evaluate('window.staffRequestedDates.at(-1)') === departure);
    await evaluate("document.getElementById('staffMatrixToday').click()");
    await waitFor("document.getElementById('staffMatrixLoading').hidden");
    check('Staff Today restores server today date', await evaluate("document.getElementById('staffMatrixDate').value") === future);
    await evaluate("document.querySelector('#staffMatrixGrid button').click()");
    check('Staff room popup opens with booking and date', await evaluate("document.getElementById('staffRoomDialog').open && document.getElementById('staffRoomBody').textContent.includes('#44')"));
    await evaluate("window.fixtureRooms[0].status='Đã đặt';document.dispatchEvent(new Event('staff:booking-updated'));");
    await waitFor("document.getElementById('staffMatrixLoading').hidden");
    check('Staff reload updates both room grid and open popup', await evaluate("document.getElementById('staffRoomDialog').open && document.getElementById('staffRoomBody').textContent.includes('Đã đặt') && document.querySelector('#staffMatrixGrid button').textContent.includes('Đã đặt')"));
    check('Staff fixture contains no Admin actions or links', await evaluate("!Array.from(document.querySelectorAll('#staffRoomMatrix a')).some(a=>a.pathname.startsWith('/Admin')) && !document.querySelector('#staffRoomMatrix [onclick*=\"saveMaintenance\"]')"));
    await evaluate("document.getElementById('staffRoomClose').click()");
    check('Staff room popup closes', await evaluate("!document.getElementById('staffRoomDialog').open"));
    for (const mode of ['403','html','json','500','abort','network']) {
        await evaluate(`window.staffResponseMode=${JSON.stringify(mode)};document.getElementById('staffMatrixRefresh').click();`);
        await waitFor("document.getElementById('staffMatrixLoading').hidden");
        check('Staff handles '+mode+' and clears loading and stale cards', await evaluate("!document.getElementById('staffMatrixError').hidden && document.querySelectorAll('#staffMatrixGrid button').length===0"));
    }
    await evaluate("window.staffResponseMode='ok';document.getElementById('staffMatrixRefresh').click()");
    await waitFor("document.querySelectorAll('#staffMatrixGrid button').length===40");
    check('Staff reload recovers after errors', await evaluate("document.getElementById('staffMatrixError').hidden"));
    await evaluate("window.staffResponseMode='queue';window.staffPending=[];document.querySelector('#staffRoomMatrix [data-shift=\"7\"]').click();document.querySelector('#staffRoomMatrix [data-shift=\"-7\"]').click();");
    await evaluate("window.staffPending[1].resolve(new Response(JSON.stringify({success:true,rooms:window.fixtureRooms,displayDate:window.staffPending[1].date,unassignedBookings:[]}),{headers:{'Content-Type':'application/json'}}));");
    await waitFor("document.getElementById('staffMatrixLoading').hidden");
    await evaluate("window.staffPending[0].resolve(new Response(JSON.stringify({success:true,rooms:[],displayDate:'STALE-RESPONSE',unassignedBookings:[]}),{headers:{'Content-Type':'application/json'}}));");
    await delay(100);
    check('Staff ignores out-of-order calendar response', await evaluate("document.querySelectorAll('#staffMatrixGrid button').length===40 && !document.getElementById('staffMatrixDateTitle').textContent.includes('STALE-RESPONSE')"));
    await evaluate("window.staffResponseMode='ok'");
    await send('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true });
    await evaluate("document.querySelector('#staffMatrixGrid button').click()");
    check('Staff popup fits mobile viewport', await evaluate("document.getElementById('staffRoomDialog').getBoundingClientRect().width<=390"));
    const staffScreenshot = await send('Page.captureScreenshot', { format: 'png' });
    fs.writeFileSync('QLKhachSan/obj/StaffMatrix-mobile.png', Buffer.from(staffScreenshot.data, 'base64'));
    check('No browser JavaScript exceptions across tested pages and isolated dashboard fixtures', exceptions.length === 0);
    if (exceptions.length) console.error(exceptions.join('\n'));
    console.log('BROWSER TOTAL PASSED: ' + count);
})().catch(error => { console.error(error); process.exitCode = 1; }).finally(async () => {
    if (ws && ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify({ id: 2147483647, method: 'Browser.close' }));
    await Promise.race([new Promise(resolve => browser.once('exit', resolve)), delay(3000)]);
    if (ws) ws.close();
    if (browser.exitCode === null) browser.kill();
});
