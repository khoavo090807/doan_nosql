function logout() {
  localStorage.removeItem('token');
  localStorage.removeItem('username');
  localStorage.removeItem('role');
  localStorage.removeItem('refId');
  localStorage.removeItem('hoTen');
  window.location.href = '/login.html';
}

function renderLayout(activePage) {
  if (document.body.dataset.layoutRendered === 'true') return;
  document.body.dataset.layoutRendered = 'true';

  const token = localStorage.getItem('token');
  const role = localStorage.getItem('role') || 'guest';
  const hoTen = localStorage.getItem('hoTen') || localStorage.getItem('username') || 'Người dùng';
  const refId = localStorage.getItem('refId') || '';

  if (!token && window.location.pathname !== '/login.html') {
    window.location.href = '/login.html';
    return;
  }

  if (role === 'sinhvien') {
    const studentAllowedPages = ['dashboard', 'dangky', 'diem'];
    if (!studentAllowedPages.includes(activePage)) {
      window.location.href = '/';
      return;
    }
  }
  if (role === 'giaovien') {
    const teacherDeniedPages = ['taikhoan', 'importexport', 'dangky', 'sinhvien', 'lophocphan', 'monhoc'];
    if (teacherDeniedPages.includes(activePage)) {
      window.location.href = '/';
      return;
    }
  }

  let navItems = [];
  if (role === 'admin') {
    navItems = [
      { id: 'dashboard',    href: '/',                icon: 'bi-speedometer2',       label: 'Tổng quan' },
      { id: 'taikhoan',     href: '/taikhoan.html',   icon: 'bi-person-badge-fill',  label: 'Quản lý Tài khoản' },
      { id: 'sinhvien',     href: '/sinhvien.html',   icon: 'bi-people',             label: 'Quản lý Sinh viên' },
      { id: 'monhoc',       href: '/monhoc.html',     icon: 'bi-book',               label: 'Quản lý Môn học' },
      { id: 'lophocphan',   href: '/lophocphan.html', icon: 'bi-journal-bookmark',   label: 'Quản lý Lớp học phần' },
      { id: 'diem',         href: '/diem.html',       icon: 'bi-pencil-square',      label: 'Quản lý Điểm' },
      { id: 'baocao',       href: '/baocao.html',     icon: 'bi-graph-up',           label: 'Báo cáo học lực' },
      { id: 'importexport', href: '/importexport.html',icon: 'bi-arrow-left-right',  label: 'Import / Export' },
    ];
  } else if (role === 'giaovien') {
    navItems = [
      { id: 'dashboard',    href: '/',                icon: 'bi-speedometer2',       label: 'Bàn làm việc GV' },
      { id: 'diem',         href: '/diem.html',       icon: 'bi-pencil-square',      label: 'Chấm điểm học phần' },
      { id: 'baocao',       href: '/baocao.html',     icon: 'bi-graph-up',           label: 'Báo cáo học lực' },
    ];
  } else {
    navItems = [
      { id: 'dashboard',    href: '/',                icon: 'bi-person-circle',      label: 'Trang cá nhân' },
      { id: 'dangky',       href: '/dangky.html',     icon: 'bi-card-checklist',     label: 'Đăng ký học phần' },
      { id: 'diem',         href: '/diem.html',       icon: 'bi-award-fill',         label: 'Bảng điểm & In PDF' },
    ];
  }

  const roleBadge = role === 'admin'
    ? '<span class="badge bg-danger">Admin</span>'
    : role === 'giaovien'
    ? '<span class="badge bg-warning text-dark">Giáo viên</span>'
    : '<span class="badge bg-info text-dark">Sinh viên</span>';

  const navbarHTML = `
    <nav class="navbar navbar-dark navbar-lms sticky-top">
      <div class="container-fluid">
        <div class="d-flex align-items-center gap-2">
          <button class="btn btn-outline-light btn-sm d-md-none me-1" id="mobileMenuBtn" title="Mở menu">
            <i class="bi bi-list"></i>
          </button>
          <a class="navbar-brand fw-bold" href="/">
            <i class="bi bi-mortarboard-fill me-2"></i>LMS HUIT
            <span class="fw-normal d-none d-md-inline ms-1 opacity-75 fs-6">– Quản lý &amp; chấm điểm</span>
          </a>
        </div>
        <div class="d-flex align-items-center gap-2">
          <div class="text-white small d-none d-sm-block text-end me-2">
            <div class="fw-semibold">${hoTen}</div>
            <div>${roleBadge} ${refId ? `<small class="opacity-75">(${refId})</small>` : ''}</div>
          </div>
          <button class="btn btn-outline-light btn-sm" data-bs-toggle="modal" data-bs-target="#modalDoiMatKhau" title="Đổi mật khẩu">
            <i class="bi bi-key me-1"></i>Đổi mật khẩu
          </button>
          <button class="btn btn-outline-light btn-sm" onclick="logout()" title="Đăng xuất">
            <i class="bi bi-box-arrow-right me-1"></i>Đăng xuất
          </button>
        </div>
      </div>
    </nav>`;

  const sidebarLinks = navItems.map(n => `
    <a class="nav-link ${n.id === activePage ? 'active' : ''}" href="${n.href}">
      <i class="bi ${n.icon}"></i><span>${n.label}</span>
    </a>`).join('');

  const sidebarHTML = `
    <aside id="lms-sidebar" class="sidebar flex-column py-2">
      <div class="sidebar-toggle">
        <button type="button" id="sidebarToggle" title="Thu nhỏ / Mở rộng">
          <i class="bi bi-layout-split"></i><span class="toggle-label">Thu nhỏ</span>
        </button>
      </div>
      <div class="section-title">Menu</div>
      <nav class="nav flex-column">${sidebarLinks}</nav>
      <div style="flex:1"></div>
      <div class="sidebar-divider"></div>
      <div class="sidebar-user px-3 pb-3">
        <div class="small text-muted" style="font-size:.75rem;">
          <div class="fw-semibold text-truncate" style="color:#334155;max-width:200px">${hoTen}</div>
          <div>${roleBadge}</div>
        </div>
      </div>
    </aside>
    <div id="sidebar-backdrop" class="sidebar-backdrop" aria-hidden="true"></div>`;

  document.body.insertAdjacentHTML('afterbegin', navbarHTML);

  if (!document.getElementById('modalDoiMatKhau')) {
    const modalHTML = `
      <div class="modal fade" id="modalDoiMatKhau" tabindex="-1">
        <div class="modal-dialog">
          <form class="modal-content" onsubmit="doiMatKhau(event)">
            <div class="modal-header bg-primary text-white">
              <h5 class="modal-title"><i class="bi bi-key me-2"></i>Đổi mật khẩu</h5>
              <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">
              <div id="modalDoiMatKhau-error" class="alert alert-danger py-2 mb-3" style="display:none;"></div>
              <div class="mb-3">
                <label class="form-label fw-semibold">Mật khẩu hiện tại <span class="text-danger">*</span></label>
                <input type="password" id="dmkMatKhauCu" class="form-control" required placeholder="Nhập mật khẩu hiện tại" />
              </div>
              <div class="mb-3">
                <label class="form-label fw-semibold">Mật khẩu mới <span class="text-danger">*</span></label>
                <input type="password" id="dmkMatKhauMoi" class="form-control" required minlength="6" placeholder="Tối thiểu 6 ký tự" />
              </div>
              <div class="mb-3">
                <label class="form-label fw-semibold">Xác nhận mật khẩu mới <span class="text-danger">*</span></label>
                <input type="password" id="dmkXacNhanMatKhau" class="form-control" required placeholder="Nhập lại mật khẩu mới" />
              </div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Hủy</button>
              <button type="submit" class="btn btn-primary"><i class="bi bi-check2-circle me-1"></i> Đổi mật khẩu</button>
            </div>
          </form>
        </div>
      </div>`;
    document.body.insertAdjacentHTML('beforeend', modalHTML);
  }

  const mainEl = document.querySelector('#page-main-content');
  if (mainEl) {
    const wrapper = document.createElement('div');
    wrapper.className = 'lms-layout d-flex';
    wrapper.innerHTML = sidebarHTML;
    mainEl.parentNode.insertBefore(wrapper, mainEl);
    wrapper.appendChild(mainEl);
  }

  // Sidebar kiểu YouTube: rail 64px luôn trong flow, mở rộng chỉ overlay tạm thời
  const layout = document.querySelector('.lms-layout');
  if (layout) layout.classList.add('collapsed');

  const toggleBtn = document.getElementById('sidebarToggle');
  const updateIcon = () => {
    if (!toggleBtn) return;
    const collapsed = layout?.classList.contains('collapsed');
    if (collapsed) {
      toggleBtn.innerHTML = '<i class="bi bi-layout-split"></i><span class="toggle-label">Mở rộng</span>';
    } else {
      toggleBtn.innerHTML = '<i class="bi bi-layout-split"></i><span class="toggle-label">Thu nhỏ</span>';
    }
    toggleBtn.title = collapsed ? 'Mở rộng sidebar' : 'Thu nhỏ sidebar';
  };
  const collapseSidebar = () => {
    if (!layout) return;
    layout.classList.add('collapsed');
    layout.classList.remove('sidebar-open');
    updateIcon();
  };

  updateIcon();
  if (toggleBtn) {
    toggleBtn.addEventListener('click', () => {
      if (!layout) return;
      layout.classList.toggle('collapsed');
      updateIcon();
    });
  }

  const backdrop = document.getElementById('sidebar-backdrop');
  if (backdrop) {
    backdrop.addEventListener('click', collapseSidebar);
  }
  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape' || !layout) return;
    if (!layout.classList.contains('collapsed') || layout.classList.contains('sidebar-open')) {
      collapseSidebar();
    }
  });

  // Menu di động: overlay đè nội dung, không push layout
  const mobileBtn = document.getElementById('mobileMenuBtn');
  if (mobileBtn) {
    const closeMobile = () => { if (layout) layout.classList.remove('sidebar-open'); };
    mobileBtn.addEventListener('click', () => {
      if (layout) layout.classList.toggle('sidebar-open');
    });
    document.querySelectorAll('#lms-sidebar .nav-link').forEach(a => {
      a.addEventListener('click', closeMobile);
    });
  }
}

async function doiMatKhau(e) {
  e.preventDefault();
  if (typeof clearModalError === 'function') clearModalError("modalDoiMatKhau");

  const matKhauCu = document.getElementById("dmkMatKhauCu")?.value || "";
  const matKhauMoi = document.getElementById("dmkMatKhauMoi")?.value || "";
  const xacNhan = document.getElementById("dmkXacNhanMatKhau")?.value || "";

  if (!matKhauCu) {
    if (typeof showModalError === 'function') showModalError("modalDoiMatKhau", "Vui lòng nhập mật khẩu hiện tại.");
    return;
  }
  if (!matKhauMoi || matKhauMoi.length < 6) {
    if (typeof showModalError === 'function') showModalError("modalDoiMatKhau", "Mật khẩu mới phải có tối thiểu 6 ký tự.");
    return;
  }
  if (matKhauMoi !== xacNhan) {
    if (typeof showModalError === 'function') showModalError("modalDoiMatKhau", "Mật khẩu mới và xác nhận mật khẩu không khớp.");
    return;
  }

  try {
    await api("/api/Auth/doi-mat-khau", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ MatKhauCu: matKhauCu, MatKhauMoi: matKhauMoi })
    });

    const modalEl = document.getElementById("modalDoiMatKhau");
    const modalObj = bootstrap.Modal.getInstance(modalEl);
    if (modalObj) modalObj.hide();

    document.getElementById("dmkMatKhauCu").value = "";
    document.getElementById("dmkMatKhauMoi").value = "";
    document.getElementById("dmkXacNhanMatKhau").value = "";

    if (typeof toast === 'function') toast("Đổi mật khẩu thành công!");
  } catch (err) {
    if (typeof showModalError === 'function') showModalError("modalDoiMatKhau", err.message || "Đổi mật khẩu thất bại.");
  }
}

