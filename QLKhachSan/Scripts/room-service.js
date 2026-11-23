(function () {
    document.querySelectorAll('.rs-form').forEach(function(form) {
        form.addEventListener('submit', function(event) {
            if (!window.fetch) return;
            event.preventDefault();
            var button = form.querySelector('button'), message = form.querySelector('.rs-feedback');
            if (!message) { message = document.createElement('p'); message.className='rs-feedback'; message.setAttribute('role','status'); form.appendChild(message); }
            button.disabled=true; message.textContent='Đang xử lý...';
            fetch(form.action,{method:'POST',body:new FormData(form),credentials:'same-origin'})
                .then(function(response){if(!response.ok)throw new Error('Yêu cầu không được chấp nhận ('+response.status+'). Kiểm tra quyền, điều kiện phòng, ngày giờ, thanh toán và dữ liệu nhập; tải lại trang nếu phiên đã hết hạn.');if(response.redirected){window.location.assign(response.url);return;}window.location.reload();})
                .catch(function(error){message.textContent=error.message;message.classList.add('rs-error');button.disabled=false;});
        });
    });
})();
(function(){var banner=document.querySelector('.rs-live');if(!banner||!window.fetch)return;var previous=null;
function poll(){if(document.hidden)return;fetch(banner.getAttribute('data-url'),{credentials:'same-origin',cache:'no-store'}).then(function(r){if(!r.ok)throw new Error();return r.json();}).then(function(data){if(previous!==null&&previous!==data.count){banner.textContent='Có cập nhật dịch vụ mới. ';var link=document.createElement('a');link.href=window.location.href;link.textContent='Tải lại để xem thông báo';banner.appendChild(link);}previous=data.count;}).catch(function(){});}
poll();window.setInterval(poll,30000);})();
