const api = (path, opts = {}) => {
  const token = localStorage.getItem('token');
  const headers = { ...opts.headers };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }
  return fetch(path, { ...opts, headers }).then(async r => {
    if (r.status === 401) {
      localStorage.clear();
      window.location.href = '/login.html';
      throw new Error('Phiên đăng nhập đã hết hạn.');
    }
    if (r.status === 403) {
      if (typeof toast === 'function') {
        toast('Bạn không có quyền thực hiện thao tác này', 'danger');
      }
      const t = await r.text();
      throw new Error(t || 'Bạn không có quyền thực hiện thao tác này');
    }
    if (!r.ok) {
      const t = await r.text();
      throw new Error(t || r.statusText);
    }
    if (r.status === 204) return null;
    const ct = r.headers.get("content-type") || "";
    return ct.includes("json") ? r.json() : r;
  });
};

function toast(msg, type = "success") {
  let region = document.getElementById('lmsToastRegion');
  if (!region) {
    region = document.createElement('div');
    region.id = 'lmsToastRegion';
    region.className = 'toast-container position-fixed top-0 end-0 p-3';
    region.setAttribute('aria-live', 'polite');
    region.setAttribute('aria-atomic', 'true');
    document.body.appendChild(region);
  }
  const tone = type === 'danger' ? 'danger' : type === 'warning' ? 'warning' : type === 'info' ? 'info' : 'success';
  const item = document.createElement('div');
  item.className = `toast text-bg-${tone} border-0`;
  item.setAttribute('role', 'status');
  item.innerHTML = `<div class="d-flex"><div class="toast-body"></div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Đóng"></button></div>`;
  item.querySelector('.toast-body').textContent = String(msg ?? '');
  region.appendChild(item);
  const instance = bootstrap.Toast.getOrCreateInstance(item, { delay: 4500 });
  item.addEventListener('hidden.bs.toast', () => item.remove(), { once: true });
  instance.show();
}

function confirmAction(message, options = {}) {
  const modalEl = document.getElementById('confirmActionModal');
  const messageEl = document.getElementById('confirmActionMessage');
  const titleEl = document.getElementById('confirmActionTitle');
  const acceptBtn = document.getElementById('confirmActionAccept');
  if (!modalEl || !window.bootstrap?.Modal) return Promise.resolve(false);

  messageEl.textContent = message;
  titleEl.textContent = options.title || 'Xác nhận thao tác';
  acceptBtn.textContent = options.confirmText || 'Xác nhận';
  acceptBtn.className = `btn btn-${options.variant || 'primary'}`;
  const modal = bootstrap.Modal.getOrCreateInstance(modalEl);

  return new Promise(resolve => {
    let accepted = false;
    const onAccept = () => { accepted = true; modal.hide(); };
    acceptBtn.addEventListener('click', onAccept, { once: true });
    modalEl.addEventListener('hidden.bs.modal', () => resolve(accepted), { once: true });
    modal.show();
  });
}

function clientFieldError(field) {
  const value = String(field.value ?? '').trim();
  if (field.required && !value) return 'Trường này không được để trống.';
  if (field.type === 'email' && value && field.validity.typeMismatch) return 'Vui lòng nhập địa chỉ email hợp lệ.';
  if (field.minLength > 0 && value && value.length < field.minLength) return `Vui lòng nhập ít nhất ${field.minLength} ký tự.`;
  if (field.type === 'number' && value !== '') {
    const number = Number(value);
    if (field.validity.stepMismatch) return 'Vui lòng nhập giá trị đúng định dạng.';
    if (field.min !== '' && number < Number(field.min)) return `Giá trị phải từ ${field.min} trở lên.`;
    if (field.max !== '' && number > Number(field.max)) return `Giá trị không được vượt quá ${field.max}.`;
  }
  return '';
}

function updateClientFieldValidity(field, show = false) {
  const message = clientFieldError(field);
  field.setCustomValidity(message);
  field.classList.toggle('is-invalid', show && !!message);
  const feedback = field.parentElement?.querySelector('.invalid-feedback');
  if (feedback && message) feedback.textContent = message;
  return !message;
}

function validateClientForm(form) {
  let valid = true;
  form.querySelectorAll('input, select, textarea').forEach(field => {
    if (field.disabled || field.type === 'hidden') return;
    if (!updateClientFieldValidity(field, true)) valid = false;
  });
  return valid;
}

function bindLiveFormValidation(form) {
  if (!form || form.dataset.liveValidation === 'true') return;
  form.dataset.liveValidation = 'true';
  form.closest('.modal')?.addEventListener('show.bs.modal', () => {
    form.querySelectorAll('input, select, textarea').forEach(field => {
      field.setCustomValidity('');
      field.classList.remove('is-invalid');
      delete field.dataset.touched;
    });
  });
  form.addEventListener('blur', event => {
    if (event.target.matches('input, select, textarea')) {
      event.target.dataset.touched = 'true';
      updateClientFieldValidity(event.target, true);
    }
  }, true);
  form.addEventListener('input', event => {
    if (event.target.matches('input, select, textarea') && event.target.dataset.touched === 'true') {
      updateClientFieldValidity(event.target, true);
    }
  });
  form.addEventListener('change', event => {
    if (event.target.matches('input, select, textarea') && event.target.dataset.touched === 'true') {
      updateClientFieldValidity(event.target, true);
    }
  });
  form.addEventListener('submit', event => {
    if (!validateClientForm(form)) {
      event.preventDefault();
      event.stopImmediatePropagation();
    }
  }, true);
}

document.querySelectorAll('.modal form').forEach(bindLiveFormValidation);

function showModalError(modalId, msg) {
  const zone = document.getElementById(modalId + "-error");
  if (!zone) return;
  zone.textContent = msg;
  zone.style.display = "block";
}

function clearModalError(modalId) {
  const zone = document.getElementById(modalId + "-error");
  if (zone) { zone.textContent = ""; zone.style.display = "none"; }
  // xóa cả is-invalid trên các input trong modal
  const modal = document.getElementById(modalId);
  if (modal) modal.querySelectorAll(".is-invalid").forEach(el => el.classList.remove("is-invalid"));
}

function showPage(id) {
  const listEl   = document.getElementById('sinhvien-list');
  const detailEl = document.getElementById('svDetail');
  if (!listEl || !detailEl) return;
  if (id === 'chitiet') {
    listEl.style.display  = 'none';
    detailEl.style.display = 'block';
  } else {
    listEl.style.display  = 'block';
    detailEl.style.display = 'none';
  }
}

function xepLoaiBadge(xl) {
  const map = {
    'Xuất sắc': 'xl-xuat-sac',
    'Giỏi':     'xl-gioi',
    'Khá':      'xl-kha',
    'Trung bình':'xl-tb',
    'Yếu':      'xl-yeu'
  };
  return `<span class="badge ${map[xl] || 'xl-tb'}">${xl || '—'}</span>`;
}

function getTeacherName(giangVien) {
  return giangVien?.HoTen || giangVien?.TenGV || 'N/A';
}

async function loadDashboard() {
  const role = localStorage.getItem('role');
  const refId = localStorage.getItem('refId');

  try {
    if (role === 'sinhvien') {
      await loadStudentDashboard(refId);
      return;
    }
    if (role === 'giaovien') {
      await loadTeacherDashboard(refId);
      return;
    }
    await loadAdminDashboard();
  } catch (e) {
    toast("Không thể tải dữ liệu tổng quan: " + e.message, "danger");
  }
}

function renderStatCards(cards) {
  const el = document.getElementById('statCards');
  if (!el) return;
  const bgMap = { primary:'#dbeafe', success:'#dcfce7', warning:'#fef9c3', info:'#e0f2fe', dark:'#f1f5f9', danger:'#fee2e2' };
  const fgMap = { primary:'#1d4ed8', success:'#15803d', warning:'#a16207', info:'#0369a1', dark:'#334155', danger:'#b91c1c' };
  el.innerHTML = cards.map(([t, n, i, c]) => `
    <div class="col-6 col-md">
      <div class="card stat-card p-3 h-100">
        <div class="d-flex justify-content-between align-items-center">
          <div>
            <div class="text-muted small mb-1">${t}</div>
            <div class="fs-4 fw-bold" style="color:${fgMap[c]||'#1e293b'}">${n}</div>
          </div>
          <div class="stat-icon" style="background:${bgMap[c]||'#f1f5f9'};color:${fgMap[c]||'#64748b'}">
            <i class="bi ${i}"></i>
          </div>
        </div>
      </div>
    </div>`).join('');
}

async function loadAdminDashboard() {
  const [sv, lhp, gv, mh, lops] = await Promise.all([
    api('/api/SinhVien'), api('/api/LopHocPhan'), api('/api/GiangVien'),
    api('/api/MonHoc'), api('/api/Reports/lopsinhhoat')
  ]);
  renderStatCards([
    ['Sinh viên', sv.length, 'bi-people', 'primary'],
    ['Lớp học phần', lhp.length, 'bi-journal-text', 'success'],
    ['Giảng viên', gv.length, 'bi-person-badge', 'warning'],
    ['Môn học', mh.length, 'bi-book', 'info'],
    ['Lớp sinh hoạt', lops.length, 'bi-building', 'dark']
  ]);
  fillLopSelects(lops);
}

async function loadTeacherDashboard(refId) {
  const [lhp, sv] = await Promise.all([
    api('/api/LopHocPhan?maGv=' + encodeURIComponent(refId || '')),
    api('/api/SinhVien')
  ]);
  const teacherClassIds = new Set(lhp.map(x => x.Id));
  const teacherStudents = sv.filter(s => (s.BangDiem || []).some(b => teacherClassIds.has(b.MaLHP)));
  renderStatCards([
    ['Lớp đang phụ trách', lhp.length, 'bi-journal-bookmark', 'primary'],
    ['Sinh viên đang dạy', teacherStudents.length, 'bi-people', 'success'],
    ['Môn học phần', new Set(lhp.map(x => x.MaMon)).size, 'bi-book', 'info']
  ]);
  const hero = document.querySelector('.hero');
  if (hero) {
    hero.querySelector('h3').textContent = 'Bàn làm việc Giáo viên';
    hero.querySelector('p').textContent = 'Theo dõi lớp giảng dạy và nhập điểm học phần được phân công.';
  }
  const statCards = document.getElementById('statCards');
  if (statCards) {
    statCards.insertAdjacentHTML('afterend', `
      <div class="col-12 mt-4"><div class="card border-0 shadow-sm"><div class="card-header bg-white fw-bold text-primary">Lớp học phần đang phụ trách</div>
      <div class="table-responsive"><table class="table table-hover mb-0"><thead><tr><th>Mã LHP</th><th>Môn học</th><th>Học kỳ</th><th>Sĩ số</th></tr></thead><tbody>
      ${lhp.map(l => `<tr><td><code>${l.Id}</code></td><td>${l.TenMon}</td><td>${l.Hocky} ${l.NamHoc}</td><td>${l.SiSoHienTai || 0}/${l.SiSoToiDa}</td></tr>`).join('') || '<tr><td colspan="4" class="text-center text-muted">Chưa được phân công lớp.</td></tr>'}
      </tbody></table></div></div></div>`);
  }
}

async function loadStudentDashboard(refId) {
  if (!refId) return;
  const [sv, hocluc, monHocs] = await Promise.all([
    api('/api/SinhVien/' + encodeURIComponent(refId)),
    api('/api/SinhVien/' + encodeURIComponent(refId) + '/hocluc').catch(() => null),
    api('/api/MonHoc').catch(() => [])
  ]);
  _courseScoreConfig = new Map((monHocs || []).map(m => [m.Id, m]));
  const hero = document.querySelector('.hero');
  if (hero) {
    hero.querySelector('h3').textContent = `Xin chào, ${sv.HoTen}`;
    hero.querySelector('p').textContent = 'Cổng học tập cá nhân - xem điểm, chuẩn đầu ra và đăng ký học phần.';
  }
  renderStatCards([
    ['MSSV', sv.Id, 'bi-person-vcard', 'primary'],
    ['Lớp sinh hoạt', sv.LopSinhHoat || 'N/A', 'bi-people', 'success'],
    ['Tín chỉ đã học', (sv.BangDiem || []).reduce((s, b) => s + (b.SoTinChi || 0), 0), 'bi-award', 'warning'],
    ['GPA hệ 10', hocluc ? (hocluc.GPA == null ? '\u2014' : Number(hocluc.GPA).toFixed(2)) : '—', 'bi-bar-chart', 'info']
  ]);
  const statCards = document.getElementById('statCards');
  if (statCards) {
    statCards.insertAdjacentHTML('afterend', `<div class="col-12 mt-4"><div class="card border-0 shadow-sm"><div class="card-body"><h5 class="text-primary">Thông tin học tập</h5><p class="mb-1"><b>Khoa:</b> ${sv.TenKhoa || sv.MaKhoa || 'N/A'}</p><p class="mb-1"><b>Email:</b> ${sv.Email || 'N/A'}</p><p class="mb-1"><b>Trạng thái:</b> ${sv.TrangThai || 'Đang học'}</p><p class="mb-0"><b>Xếp loại:</b> ${hocluc ? xepLoaiBadge(hocluc.XepLoai) : 'Chưa đủ dữ liệu'}</p></div></div></div>`);
  }
}

function fillLopSelects(lops) {
  const apply = list => {
    ["filterLop", "bcLop", "exLop"].forEach(id => {
      const el = document.getElementById(id);
      if (!el) return;
      const cur = el.value;
      el.innerHTML = `<option value="">Tất cả lớp</option>` + list.map(l => `<option>${l}</option>`).join("");
      el.value = cur;
    });
  };
  if (lops) apply(lops);
  else {
    // Sử dụng endpoint khác nhau tùy theo role
    const role = localStorage.getItem('role');
    if (role === 'giaovien') {
      // Giáo viên chỉ thấy lớp học phần mình dạy
      const refId = localStorage.getItem('refId') || '';
      api('/api/lophocphan?maGv=' + encodeURIComponent(refId)).then(data => {
        const lopNames = data.map(item => item.Id + ' – ' + item.TenMon);
        apply(lopNames);
      }).catch(() => {});
    } else {
      // Admin và sinh viên dùng endpoint cũ
      api("/api/Reports/lopsinhhoat").then(apply).catch(() => {});
    }
  }
}

async function fillDiemLopSelect() {
  const el = document.getElementById('diemFilterLop');
  if (!el) return;

  const current = el.value;
  try {
    const list = await api('/api/lophocphan/dropdown');
    const validItems = (list || []).filter(item => {
      const value = (item.Value || item.Id || '').trim();
      return value.length > 0;
    });

    const options = validItems.map(item => {
      const value = item.Value || item.Id || '';
      const label = item.Id || item.Value || '';
      return `<option value="${value}">${label}</option>`;
    }).join('');

    el.innerHTML = `<option value="">Tất cả lớp học phần</option>${options}`;
    if (current) {
      const match = Array.from(el.options).some(option => option.value === current);
      if (match) el.value = current;
    }
  } catch (e) {
    console.warn('Không tải được danh sách lớp học phần cho điểm:', e);
    el.innerHTML = '<option value="">Tất cả lớp học phần</option>';
  }
}

function filterDiemLopOptions() {
  const input = document.getElementById('diemLopSearch');
  const select = document.getElementById('diemFilterLop');
  if (!input || !select) return;

  const q = (input.value || '').trim().toLowerCase();
  Array.from(select.options).forEach(option => {
    if (!option.value) {
      option.hidden = false;
      return;
    }
    const haystack = `${option.value} ${option.textContent || ''}`.toLowerCase();
    option.hidden = !!q && !haystack.includes(q);
  });
}

async function fillKhoaSelects(selectedId) {
  try {
    const khoas = await api("/api/Khoa");
    const options = khoas.map(k => `<option value="${k.Id}">${k.TenKhoa}</option>`).join("");
    const el = document.getElementById("svMaKhoa");
    if (el) {
      el.innerHTML = options;
      if (selectedId) el.value = selectedId;
    }
  } catch {}
}

let _sinhVienRows = [];
let _sinhVienPage = 1;
let _sinhVienPageSize = 10;
let _sinhVienLoaded = false;

function debounceClient(fn, delay = 250) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), delay);
  };
}

function renderTablePagination(containerId, total, page, pageSize, onPage, onPageSize) {
  const container = document.getElementById(containerId);
  if (!container) return;
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const start = total ? (page - 1) * pageSize + 1 : 0;
  const end = Math.min(page * pageSize, total);
  container.innerHTML = `
    <div class="table-page-summary">${start}–${end} / ${total}</div>
    <div class="d-flex align-items-center gap-2">
      <label class="small text-muted" for="${containerId}-size">Dòng/trang</label>
      <select id="${containerId}-size" class="form-select form-select-sm table-page-size" aria-label="Số dòng mỗi trang">
        ${[10,20,50].map(size => `<option value="${size}" ${size === pageSize ? 'selected' : ''}>${size}</option>`).join('')}
      </select>
      <button type="button" class="btn btn-sm btn-outline-secondary" data-page="prev" aria-label="Trang trước" ${page <= 1 ? 'disabled' : ''}><i class="bi bi-chevron-left"></i></button>
      <span class="small text-nowrap">${page} / ${pages}</span>
      <button type="button" class="btn btn-sm btn-outline-secondary" data-page="next" aria-label="Trang sau" ${page >= pages ? 'disabled' : ''}><i class="bi bi-chevron-right"></i></button>
    </div>`;
  container.querySelector('[data-page="prev"]')?.addEventListener('click', () => onPage(page - 1));
  container.querySelector('[data-page="next"]')?.addEventListener('click', () => onPage(page + 1));
  container.querySelector('select')?.addEventListener('change', e => onPageSize(Number(e.target.value)));
}

function renderSinhVienTable() {
  const table = document.getElementById('svTable');
  if (!table) return;
  const role = localStorage.getItem('role');
  const lop = (document.getElementById('filterLop')?.value || '').trim().toLocaleLowerCase('vi');
  const q = (document.getElementById('filterQ')?.value || '').trim().toLocaleLowerCase('vi');
  const rows = _sinhVienRows.filter(sv => {
    const matchesClass = !lop || (sv.LopSinhHoat || '').toLocaleLowerCase('vi') === lop;
    const haystack = `${sv.Id || ''} ${sv.HoTen || ''} ${sv.Email || ''}`.toLocaleLowerCase('vi');
    return matchesClass && (!q || haystack.includes(q));
  });
  const count = document.getElementById('svCount');
  if (count) count.textContent = `Tổng ${_sinhVienRows.length} sinh viên · ${rows.length} kết quả`;

  const pages = Math.max(1, Math.ceil(rows.length / _sinhVienPageSize));
  _sinhVienPage = Math.min(_sinhVienPage, pages);
  const visible = rows.slice((_sinhVienPage - 1) * _sinhVienPageSize, _sinhVienPage * _sinhVienPageSize);
  if (!rows.length) {
    table.innerHTML = `<tr><td colspan="6" class="text-center py-4 text-muted"><i class="bi bi-search me-1"></i>${_sinhVienLoaded ? 'Không có sinh viên phù hợp với bộ lọc.' : 'Chưa có sinh viên.'}</td></tr>`;
  } else {
    table.innerHTML = visible.map(sv => {
      const adminActions = role === 'admin' ? `
        <button class="btn btn-sm btn-outline-success" data-bs-toggle="tooltip" title="Chỉnh sửa" aria-label="Chỉnh sửa sinh viên" onclick="editSv('${sv.Id}')"><i class="bi bi-pencil"></i></button>
        <button class="btn btn-sm btn-outline-danger" data-bs-toggle="tooltip" title="Xóa" aria-label="Xóa sinh viên" onclick="xoaSv('${sv.Id}')"><i class="bi bi-trash"></i></button>` : '';
      return `<tr>
        <td><code>${sv.Id}</code></td><td>${sv.HoTen}</td><td>${sv.LopSinhHoat || ''}</td>
        <td>${sv.TenKhoa || sv.MaKhoa || ''}</td>
        <td><span class="badge bg-secondary">${sv.TrangThai || 'Đang học'}</span></td>
        <td class="text-end"><div class="table-actions">
          <button class="btn btn-sm btn-outline-primary" data-bs-toggle="tooltip" title="Xem hồ sơ" aria-label="Xem hồ sơ sinh viên" onclick="xemSv('${sv.Id}')"><i class="bi bi-eye"></i></button>
          ${adminActions}
        </div></td>
      </tr>`;
    }).join('');
    table.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(el => bootstrap.Tooltip.getOrCreateInstance(el));
  }
  renderTablePagination('svPagination', rows.length, _sinhVienPage, _sinhVienPageSize,
    page => { _sinhVienPage = page; renderSinhVienTable(); },
    size => { _sinhVienPageSize = size; _sinhVienPage = 1; renderSinhVienTable(); });
}

const debouncedFilterSinhVien = debounceClient(() => {
  _sinhVienPage = 1;
  renderSinhVienTable();
});

async function loadSinhVien() {
  const table = document.getElementById('svTable');
  if (!table) return;
  table.innerHTML = '<tr><td colspan="6" class="text-center py-4"><div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>Đang tải danh sách sinh viên...</td></tr>';
  const count = document.getElementById('svCount');
  if (count) count.textContent = 'Đang tải danh sách...';
  const addBtn = document.querySelector('button[data-bs-target="#modalSinhVien"]');
  if (addBtn) addBtn.style.display = localStorage.getItem('role') === 'admin' ? '' : 'none';
  try {
    _sinhVienRows = await api('/api/SinhVien');
    _sinhVienLoaded = true;
    _sinhVienPage = 1;
    renderSinhVienTable();
  } catch (e) {
    table.innerHTML = `<tr><td colspan="6" class="text-center py-4 text-danger">Không tải được danh sách sinh viên.<br/><button class="btn btn-sm btn-outline-primary mt-2" onclick="loadSinhVien()"><i class="bi bi-arrow-clockwise me-1"></i>Thử lại</button></td></tr>`;
    if (count) count.textContent = 'Không tải được dữ liệu';
    toast('Không tải được danh sách sinh viên: ' + (e.message || 'Lỗi máy chủ'), 'danger');
  }
}

async function editSv(id) {
  const sv = await api("/api/SinhVien/" + id);
  await fillKhoaSelects(sv.MaKhoa);
  document.getElementById("svIsEdit").value = sv.Id;
  document.getElementById("svId").disabled = false;
  document.getElementById("svId").value = sv.Id;
  document.getElementById("svId").classList.remove("is-invalid");
  document.getElementById("svHoTen").value = sv.HoTen;
  document.getElementById("svGioiTinh").value = sv.GioiTinh || "Nam";
  document.getElementById("svEmail").value = sv.Email || "";
  document.getElementById("svDienThoai").value = sv.DienThoai || "";
  document.getElementById("svLop").value = sv.LopSinhHoat || "";
  document.getElementById("svKhoaHoc").value = sv.KhoaHoc || "";
  document.getElementById("svTrangThaiDiv").style.display = "block";
  document.getElementById("svCreateAccountDiv").style.display = "none";
  document.getElementById("svTaoTaiKhoan").checked = false;
  document.getElementById("svMatKhauTaiKhoan").value = "";
  document.getElementById("svTrangThai").value = sv.TrangThai || "Đang học";
  clearModalError("modalSinhVien");
  document.getElementById("svModalTitle").innerText = "Chỉnh sửa hồ sơ Sinh viên";
  new bootstrap.Modal(document.getElementById("modalSinhVien")).show();
}

async function xemSv(id) {
  const detailEl = document.getElementById('svDetail');
  showPage('chitiet');
  detailEl.innerHTML = `<button class="btn btn-outline-secondary btn-sm mb-3" onclick="showPage('list')"><i class="bi bi-arrow-left me-1"></i>Quay lại danh sách</button>
    <div class="card"><div class="card-body text-center text-muted py-4"><span class="spinner-border spinner-border-sm me-2" role="status"></span>Đang tải hồ sơ sinh viên...</div></div>`;
  let sv;
  try {
    sv = await api("/api/SinhVien/" + id);
  } catch (error) {
    detailEl.innerHTML = `<button class="btn btn-outline-secondary btn-sm mb-3" onclick="showPage('list')"><i class="bi bi-arrow-left me-1"></i>Quay lại danh sách</button>
      <div class="alert alert-danger">Không tải được hồ sơ sinh viên. <button class="btn btn-sm btn-outline-danger ms-2" onclick="xemSv('${id}')">Thử lại</button></div>`;
    return;
  }
  let hocluc = null;
  try { hocluc = await api("/api/SinhVien/" + id + "/hocluc"); } catch { /* empty bangdiem */ }
  const grades = sv.BangDiem || [];
  detailEl.innerHTML = `
    <button class="btn btn-outline-secondary btn-sm mb-3" onclick="showPage('list')">
      <i class="bi bi-arrow-left me-1"></i> Quảy lại danh sách
    </button>
    <div class="card">
      <div class="card-body">
        <div class="d-flex justify-content-between align-items-start">
          <div>
            <h5 class="fw-bold">${sv.HoTen} <small class="text-muted fw-normal">${sv.Id}</small></h5>
            <p class="text-muted mb-0">Lớp ${sv.LopSinhHoat} · Khoa: ${sv.TenKhoa || sv.MaKhoa || "N/A"} · ${sv.Email || ''} · ${sv.TrangThai || ""}</p>
          </div>
          ${hocluc ? `<div class="text-end">
            <div class="gpa-display">${(hocluc.GPA == null ? '\u2014' : Number(hocluc.GPA).toFixed(2))}</div>
            <div>${xepLoaiBadge(hocluc.XepLoai)}</div>
          </div>` : ""}
        </div>
      </div>
    </div>
    <div class="table-scroll-card mt-3">
      <div class="table-responsive table-scroll-area">
        <table class="table table-sm table-bordered table-hover mb-0">
          <thead class="table-light"><tr><th>Mã</th><th>Môn</th><th class="text-end">TC</th><th>GV</th><th class="text-end">CC</th><th class="text-end">GK</th><th class="text-end">TH</th><th class="text-end">CK</th><th class="text-end">Tổng</th></tr></thead>
          <tbody id="svDetailGrades"></tbody>
        </table>
      </div>
      <div id="svDetailPagination" class="table-pagination"></div>
    </div>`;
  renderStudentDetailGrades(grades);
  showPage("chitiet");
}

let _svDetailPage = 1;
let _svDetailPageSize = 10;
function renderStudentDetailGrades(grades) {
  const body = document.getElementById('svDetailGrades');
  if (!body) return;
  const pages = Math.max(1, Math.ceil(grades.length / _svDetailPageSize));
  _svDetailPage = Math.min(_svDetailPage, pages);
  const visible = grades.slice((_svDetailPage - 1) * _svDetailPageSize, _svDetailPage * _svDetailPageSize);
  body.innerHTML = visible.length ? visible.map(b => {
    const hasScore = gradeComplete(b);
    return `<tr>
    <td>${b.MaMon}</td><td>${b.TenMon}</td><td class="text-end">${b.SoTinChi}</td>
    <td>${b.GiangVien?.HoTen || b.GiangVien?.TenGV || ''}</td>
    <td class="text-end">${b.DiemThanhPhan?.ChuyenCan ?? ''}</td><td class="text-end">${b.DiemThanhPhan?.GiuaKy ?? ''}</td>
    <td class="text-end">${b.DiemThanhPhan?.ThucHanh ?? ''}</td><td class="text-end">${b.DiemThanhPhan?.CuoiKy ?? ''}</td>
    <td class="text-end"><b>${hasScore ? b.DiemTongKet : gradeStatus(b)}</b> (${hasScore ? (b.DiemChu || '\u0110\u00e3 nh\u1eadp \u0111i\u1ec3m') : gradeStatus(b)})</td></tr>`;
  }).join('')
    : '<tr><td colspan="9" class="text-center text-muted py-3">Chưa có điểm.</td></tr>';
  renderTablePagination('svDetailPagination', grades.length, _svDetailPage, _svDetailPageSize,
    page => { _svDetailPage = page; renderStudentDetailGrades(grades); },
    size => { _svDetailPageSize = size; _svDetailPage = 1; renderStudentDetailGrades(grades); });
}

function openCreateSv() {
  document.getElementById("svIsEdit").value = "";
  document.getElementById("svId").disabled = false;
  document.getElementById("svId").classList.remove("is-invalid");
  ["svId","svHoTen","svEmail","svDienThoai","svLop","svKhoaHoc"].forEach(id => {
    const el = document.getElementById(id);
    if (el) { el.value = ""; el.classList.remove("is-invalid"); }
  });
  document.getElementById("svGioiTinh").value = "Nam";
  const ttDiv = document.getElementById("svTrangThaiDiv");
  if (ttDiv) ttDiv.style.display = "none";
  document.getElementById("svCreateAccountDiv").style.display = "block";
  document.getElementById("svTaoTaiKhoan").checked = false;
  document.getElementById("svMatKhauTaiKhoan").value = "";
  toggleCreateStudentAccount();
  document.getElementById("svLop").value = "14DHTH01";
  document.getElementById("svKhoaHoc").value = "K14";
  fillKhoaSelects("K01");
  clearModalError("modalSinhVien");
  document.getElementById("svModalTitle").innerText = "Thêm sinh viên mới";
}

function toggleCreateStudentAccount() {
  const checked = document.getElementById("svTaoTaiKhoan").checked;
  const password = document.getElementById("svMatKhauTaiKhoan");
  password.disabled = !checked;
  password.required = checked;
}

async function saveSinhVien(e) {
  e.preventDefault();
  const form = e.currentTarget;
  clearModalError('modalSinhVien');
  if (!validateClientForm(form)) return;
  const saveButton = form.querySelector('[type="submit"]');
  if (saveButton?.disabled) return;
  if (saveButton) saveButton.disabled = true;
  try {
    await saveSinhVienCore(e);
  } finally {
    if (saveButton) saveButton.disabled = false;
  }
}

async function saveSinhVienCore(e) {
  e.preventDefault();
  clearModalError("modalSinhVien");

  const oldId   = document.getElementById("svIsEdit").value;  // rỗng = Thêm mới
  const newId   = document.getElementById("svId").value.trim();
  const idField = document.getElementById("svId");
  const taoTaiKhoan = !oldId && document.getElementById("svTaoTaiKhoan").checked;
  const matKhauTaiKhoan = document.getElementById("svMatKhauTaiKhoan").value;

  if (!newId) {
    showModalError("modalSinhVien", "MSSV không được để trống.");
    idField.classList.add("is-invalid"); idField.focus(); return;
  }
  if (taoTaiKhoan && matKhauTaiKhoan.trim().length < 6) {
    showModalError("modalSinhVien", "Mật khẩu tài khoản sinh viên phải có tối thiểu 6 ký tự.");
    document.getElementById("svMatKhauTaiKhoan").focus();
    return;
  }

  // Kiểm tra trùng MSSV: nếu thêm mới HOẶC đổi MSSV sang ID khác
  if (!oldId || newId !== oldId) {
    try {
      await api("/api/SinhVien/" + encodeURIComponent(newId));
      // Nếu không throw → ID đã tồn tại
      showModalError("modalSinhVien", `MSSV "${newId}" đã tồn tại trong hệ thống. Vui lòng chọn mã khác.`);
      idField.classList.add("is-invalid"); idField.focus(); return;
    } catch { /* 404 = chưa tồn tại, tiếp tục */ }
  }

  let existingBangDiem = [];
  if (oldId) {
    try { existingBangDiem = (await api("/api/SinhVien/" + oldId)).BangDiem || []; } catch { }
  }

  const body = {
    Id: newId,
    HoTen: document.getElementById("svHoTen").value,
    GioiTinh: document.getElementById("svGioiTinh").value,
    Email: document.getElementById("svEmail").value,
    DienThoai: document.getElementById("svDienThoai").value,
    LopSinhHoat: document.getElementById("svLop").value,
    MaKhoa: document.getElementById("svMaKhoa").value,
    KhoaHoc: document.getElementById("svKhoaHoc").value,
    TrangThai: document.getElementById("svTrangThai")?.value || "Đang học",
    BangDiem: existingBangDiem,
    TaoTaiKhoan: taoTaiKhoan,
    MatKhauTaiKhoan: taoTaiKhoan ? matKhauTaiKhoan : null
  };

  try {
    if (oldId && newId !== oldId) {
      // Đổi MSSV: xóa record cũ, tạo mới với ID mới
      await api("/api/SinhVien/" + encodeURIComponent(oldId), { method: "DELETE" });
      await api("/api/SinhVien", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body)
      });
    } else {
      await api("/api/SinhVien" + (oldId ? `/${encodeURIComponent(newId)}` : ""), {
        method: oldId ? "PUT" : "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body)
      });
    }
    bootstrap.Modal.getInstance(document.getElementById("modalSinhVien")).hide();
    toast(oldId ? `Cập nhật sinh viên ${newId} thành công!` : `Đã thêm sinh viên ${newId}`);
    loadSinhVien();
  } catch (err) {
    showModalError("modalSinhVien", "Lỗi khi lưu: " + err.message);
  }
}

async function xoaSv(id) {
  if (!await confirmAction(`Xóa sinh viên ${id}?`, { title: 'Xóa sinh viên', confirmText: 'Xóa', variant: 'danger' })) return;
  await api("/api/SinhVien/" + id, { method: "DELETE" });
  toast("Đã xóa " + id, "warning");
  loadSinhVien();
}

async function loadLhp() {
  if (typeof loadLhpPage === 'function') { loadLhpPage(); return; }
  const role = localStorage.getItem('role');
  const addBtn = document.querySelector('button[data-bs-target="#modalLhp"]');
  if (addBtn) addBtn.style.display = (role === 'admin') ? '' : 'none';
  const list = await api('/api/LopHocPhan');
  const tbody = document.getElementById('lhpTable');
  if (!tbody) return;
  tbody.innerHTML = list.map(l => {
    const adminActions = role === 'admin' ? `
        <button class="btn btn-sm btn-outline-success" onclick="editLhp('${l.Id}')"><i class="bi bi-pencil"></i></button>
        <button class="btn btn-sm btn-outline-danger" onclick="xoaLhp('${l.Id}')"><i class="bi bi-trash"></i></button>` : '<span class="text-muted small">Chỉ xem</span>';
    
    const chamDiemBtn = `<button class="btn btn-sm btn-primary me-1" onclick="chamDiemLhp('${l.Id}')" title="Chấm điểm lớp này">
      <i class="bi bi-clipboard-check"></i> Chấm điểm
    </button>`;
    
    return `<tr>
      <td><code>${l.Id}</code></td><td>${l.TenMon}</td>
      <td>${l.Hocky} ${l.NamHoc}</td><td>${getTeacherName(l.GiangVien)}</td>
      <td>${l.PhongHoc || ''}</td><td>${l.LichHoc || ''}</td><td>${l.SiSoToiDa}</td>
      <td class="text-end">${chamDiemBtn}${adminActions}</td></tr>`;
  }).join('');
}

async function editLhp(id) {
  try {
    const l = await api("/api/LopHocPhan/" + id);
    
    // Đảm bảo dữ liệu dropdown đã load
    if (!window._allKhoas || !window._allMonHocs || !window._allGiangViens) {
      console.warn('Dropdown data not loaded yet, waiting...');
      // Wait for dropdown data to load
      await new Promise(resolve => {
        const checkData = () => {
          if (window._allKhoas && window._allMonHocs && window._allGiangViens) {
            resolve();
          } else {
            setTimeout(checkData, 100);
          }
        };
        checkData();
      });
    }
    
    const enrolledCount = Number(l.SiSoHienTai || 0);
    const hasEnrollments = enrolledCount > 0;
    document.getElementById("lhpIsEdit").value = l.Id;
    document.getElementById("lhpId").value = l.Id;
    document.getElementById("lhpId").disabled = hasEnrollments;
    document.getElementById("lhpId").classList.remove("is-invalid");
    clearModalError("modalLhp");
    
    document.getElementById("lhpHk").value = l.Hocky;
    document.getElementById("lhpHk").disabled = hasEnrollments;
    document.getElementById("lhpNam").value = l.NamHoc;
    document.getElementById("lhpNam").disabled = hasEnrollments;
    const lockNotice = document.getElementById("lhpEnrollmentLockNotice");
    if (lockNotice) lockNotice.classList.toggle('d-none', !hasEnrollments);
    const capacityInput = document.getElementById("lhpSiso");
    capacityInput.min = String(Math.max(1, Math.min(enrolledCount, Number(l.SiSoToiDa || 1))));
    document.getElementById("lhpPhong").value = l.PhongHoc || "";
    document.getElementById("lhpLich").value = l.LichHoc || "";
    document.getElementById("lhpSiso").value = l.SiSoToiDa;
    // Số tín chỉ
    const soTinChiEl = document.getElementById("lhpSoTinChi");
    if (soTinChiEl) {
      soTinChiEl.disabled = hasEnrollments;
      soTinChiEl.value = l.SoTinChi || 3;
      soTinChiEl.classList.remove("is-invalid");
    }
    // Trạng thái mở/đóng lớp học phần (mặc định true để tương thích dữ liệu cũ)
    const daMo = l.DaMo !== false;
    document.getElementById("lhpDaMo").checked = daMo;
    updateDaMoLabel();
    document.getElementById("lhpChoPhepGvNhapSuaDiem").checked = l.ChoPhepGvNhapSuaDiem === true;
    
    // Tìm khoa từ môn học
    const monHoc = window._allMonHocs.find(m => m.Id === l.MaMon);
    if (monHoc) {
      // Set khoa và môn học
      window.setKhoaDisplay(monHoc.MaKhoa);
      window.setMonDisplay(l.MaMon, l.TenMon);
      if (soTinChiEl && !l.SoTinChi && monHoc.SoTinChi) {
        soTinChiEl.value = monHoc.SoTinChi;
      }
    } else {
      // Fallback: điền trực tiếp nếu không tìm thấy môn trong DB
      document.getElementById("lhpMaMon").value = l.MaMon;
      document.getElementById("lhpTenMon").value = l.TenMon;
      document.getElementById("lhpMonText").textContent = `${l.TenMon} (${l.MaMon})`;
    }
    
    // Set giảng viên
    if (l.GiangVien?.MaGV) {
      window.setGvDisplay(l.GiangVien.MaGV, getTeacherName(l.GiangVien));
    }
    ['lhpKhoaDropdown', 'lhpMonDropdown'].forEach(fieldId => {
      const field = document.getElementById(fieldId);
      if (field) field.disabled = hasEnrollments;
    })
    
    document.getElementById("lhpModalTitle").innerText = "Chỉnh sửa Lớp học phần";
    new bootstrap.Modal(document.getElementById("modalLhp")).show();
  } catch (err) {
    toast("Lỗi khi tải thông tin lớp học phần: " + err.message, "danger");
  }
}

function openCreateLhp() {
  document.getElementById("lhpIsEdit").value = "";
  document.getElementById("lhpId").disabled = false;
  const lockNotice = document.getElementById('lhpEnrollmentLockNotice');
  if (lockNotice) lockNotice.classList.add('d-none');
  document.getElementById('lhpSiso').min = '1';
  ['lhpHk', 'lhpNam', 'lhpDaMo', 'lhpChoPhepGvNhapSuaDiem', 'lhpKhoaDropdown', 'lhpMonDropdown', 'lhpGvDropdown', 'lhpSoTinChi'].forEach(fieldId => {
    const field = document.getElementById(fieldId);
    if (field) field.disabled = false;
  });
  ["lhpId","lhpMaMon","lhpTenMon","lhpPhong","lhpLich"].forEach(id => {
    const el = document.getElementById(id);
    if (el) { el.value = ""; el.classList.remove("is-invalid"); }
  });
  // Reset dropdown fields
  document.getElementById("lhpMaKhoa").value = "";
  document.getElementById("lhpMaGv").value = "";
  document.getElementById("lhpTenGv").value = "";

  document.getElementById("lhpHk").value = "HK1";
  document.getElementById("lhpNam").value = "2026-2027";
  document.getElementById("lhpSiso").value = "50";
  const soTinChiEl = document.getElementById("lhpSoTinChi");
  if (soTinChiEl) {
    soTinChiEl.value = "3";
    soTinChiEl.classList.remove("is-invalid");
  }
  // Trạng thái mở/đóng: mặc định mở cho lớp mới
  document.getElementById("lhpDaMo").checked = true;
  document.getElementById("lhpChoPhepGvNhapSuaDiem").checked = false;
  updateDaMoLabel();
  clearModalError("modalLhp");
  document.getElementById("lhpModalTitle").innerText = "Thêm Lớp học phần mới";
}

async function saveLhp(e) {
  e.preventDefault();
  const form = e.currentTarget;
  clearModalError('modalLhp');
  if (!validateClientForm(form)) return;
  const saveButton = form.querySelector('[type="submit"]');
  if (saveButton?.disabled) return;
  const originalHtml = saveButton?.innerHTML;
  if (saveButton) {
    saveButton.disabled = true;
    saveButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Đang lưu...';
  }
  try {
    await saveLhpCore(e);
  } finally {
    if (saveButton) {
      saveButton.disabled = false;
      saveButton.innerHTML = originalHtml;
    }
  }
}

async function saveLhpCore(e) {
  e.preventDefault();
  clearModalError("modalLhp");

  const oldId   = document.getElementById("lhpIsEdit").value;
  const newId   = document.getElementById("lhpId").value.trim();
  const idField = document.getElementById("lhpId");

  if (!newId) {
    showModalError("modalLhp", "Mã LHP không được để trống.");
    idField.classList.add("is-invalid"); idField.focus(); return;
  }

  // Kiểm tra đã chọn khoa và giảng viên chưa
  const maKhoa = document.getElementById("lhpMaKhoa").value;
  const maGv = document.getElementById("lhpMaGv").value;
  const tenGv = document.getElementById("lhpTenGv").value;

  if (!maKhoa) {
    showModalError("modalLhp", "Vui lòng chọn khoa.");
    document.getElementById('lhpKhoaDropdown').classList.add('is-invalid');
    document.getElementById('lhpKhoaFeedback').classList.add('d-block');
    document.getElementById('lhpKhoaDropdown').focus();
    return;
  }

  if (!maGv || !tenGv) {
    showModalError("modalLhp", "Vui lòng chọn giảng viên.");
    document.getElementById('lhpGvDropdown').classList.add('is-invalid');
    document.getElementById('lhpGvFeedback').classList.add('d-block');
    document.getElementById('lhpGvDropdown').focus();
    return;
  }

  if (!document.getElementById('lhpMaMon').value) {
    showModalError("modalLhp", "Vui lòng chọn môn học.");
    document.getElementById('lhpMonDropdown').classList.add('is-invalid');
    document.getElementById('lhpMonFeedback').classList.add('d-block');
    document.getElementById('lhpMonDropdown').focus();
    return;
  }

  const soTinChi = parseInt(document.getElementById("lhpSoTinChi")?.value, 10);
  if (isNaN(soTinChi) || soTinChi <= 0) {
    showModalError("modalLhp", "Số tín chỉ phải lớn hơn 0.");
    document.getElementById("lhpSoTinChi")?.classList.add("is-invalid");
    document.getElementById("lhpSoTinChi")?.focus();
    return;
  }

  // Kiểm tra trùng Mã LHP
  if (!oldId || newId !== oldId) {
    try {
      await api("/api/LopHocPhan/" + encodeURIComponent(newId));
      showModalError("modalLhp", `Mã LHP "${newId}" đã tồn tại. Vui lòng chọn mã khác.`);
      const feedback = idField.parentElement.querySelector('.invalid-feedback');
      if (feedback) feedback.textContent = 'Mã lớp học phần đã tồn tại. Vui lòng chọn mã khác.';
      idField.classList.add("is-invalid"); idField.focus(); return;
    } catch { /* 404 = chưa tồn tại */ }
  }

  const body = {
    Id: newId,
    MaMon: document.getElementById("lhpMaMon").value,
    TenMon: document.getElementById("lhpTenMon").value,
    SoTinChi: soTinChi,
    Hocky: document.getElementById("lhpHk").value,
    NamHoc: document.getElementById("lhpNam").value,
    GiangVien: {
      MaGV: maGv,
      HoTen: tenGv
    },
    PhongHoc: document.getElementById("lhpPhong").value,
    LichHoc: document.getElementById("lhpLich").value,
    SiSoToiDa: Number(document.getElementById("lhpSiso").value || 50),
    DaMo: document.getElementById("lhpDaMo").checked,
    ChoPhepGvNhapSuaDiem: document.getElementById("lhpChoPhepGvNhapSuaDiem").checked
  };

  try {
    if (oldId && newId !== oldId) {
      // Đổi Mã LHP: xóa cũ, thêm mới
      await api("/api/LopHocPhan/" + encodeURIComponent(oldId), { method: "DELETE" });
      await api("/api/LopHocPhan", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body)
      });
    } else {
      await api("/api/LopHocPhan" + (oldId ? `/${encodeURIComponent(newId)}` : ""), {
        method: oldId ? "PUT" : "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body)
      });
    }
    bootstrap.Modal.getInstance(document.getElementById("modalLhp")).hide();
    toast(oldId ? `Cập nhật LHP ${newId} thành công!` : `Đã thêm lớp học phần ${newId}`);
    loadLhp();
  } catch (err) {
    showModalError("modalLhp", "Lỗi khi lưu: " + err.message);
  }
}

async function xoaLhp(id) {
  if (!await confirmAction(`Xóa lớp học phần ${id}?`, { title: 'Xóa lớp học phần', confirmText: 'Xóa', variant: 'danger' })) return;
  await api("/api/LopHocPhan/" + id, { method: "DELETE" });
  loadLhp();
}

// Chuyển đến trang chấm điểm cho lớp học phần cụ thể
function chamDiemLhp(maLhp) {
  window.location.href = `/diem.html?lhp=${encodeURIComponent(maLhp)}`;
}

// Cache danh sách SV đã tải để lọc client-side
let _diemSvCache = [];
let _diemPage = 1;
let _diemPageSize = 10;
let _transcriptCache = [];
let _transcriptPage = 1;
let _transcriptPageSize = 10;
let _currentFilterLhp = '';
let _courseScoreConfig = new Map();
let _courseIdByLhp = new Map();
function gradeComplete(b) {
  const d = b?.DiemThanhPhan;
  const course = _courseScoreConfig.get(b?.MaMon);
  const usesPractice = !course || Number(course.ThucHanh) > 0;
  return b?.DiemTongKet != null && d?.ChuyenCan != null && d?.GiuaKy != null &&
    d?.CuoiKy != null && (!usesPractice || d?.ThucHanh != null);
}
function gradeStatus(b) {
  const d = b?.DiemThanhPhan;
  return ['ChuyenCan','GiuaKy','ThucHanh','CuoiKy'].some(k => d?.[k] != null)
    ? 'Ch\u01b0a ho\u00e0n t\u1ea5t \u0111i\u1ec3m' : 'Ch\u01b0a c\u00f3 \u0111i\u1ec3m';
}
function configurePracticeInput(id, maMon) {
  const input = document.getElementById(id);
  if (!input) return;
  const course = _courseScoreConfig.get(maMon);
  const applies = !course || Number(course.ThucHanh) > 0;
  input.disabled = !applies;
  input.required = false;
  if (!applies) input.value = '';
}
function optionalScore(id) {
  const input = document.getElementById(id);
  return !input || input.disabled || input.value.trim() === '' ? null : Number(input.value);
}
let _scoreEditableLhpIds = new Set();

async function initQuanLyDiemPage() {
  const role = localStorage.getItem('role');
  const refId = localStorage.getItem('refId');

  if (role === 'sinhvien') {
    const managementView = document.getElementById('adminTeacherDiemView');
    const studentView = document.getElementById('studentTranscriptView');
    if (managementView) managementView.style.display = 'none';
    if (studentView) studentView.style.display = 'block';
    const downloadPdfBtn = document.getElementById('downloadTranscriptPdfBtn');
    if (downloadPdfBtn) downloadPdfBtn.style.display = '';
    document.getElementById('printDateString').textContent = 'Ngày in: ' + new Date().toLocaleDateString('vi-VN');
    if (refId) await loadStudentTranscript(refId);
    return;
  }

  const managementView = document.getElementById('adminTeacherDiemView');
  if (managementView) managementView.style.display = 'block';
  const studentView = document.getElementById('studentTranscriptView');
  if (studentView) studentView.style.display = 'none';

  const roleLabel = role === 'giaovien' ? 'Chấm điểm các lớp được phân công' : 'Quản lý & Chấm điểm toàn trường';
  const subTitle = document.getElementById('diemSubTitle');
  if (subTitle) subTitle.textContent = roleLabel;

  const refLhp = new URLSearchParams(window.location.search).get('lhp');
  await fillLhpSelect(role === 'giaovien' ? null : undefined);
  await loadDiemSvList(refLhp);
}

async function downloadTranscriptPdf() {
  const refId = localStorage.getItem('refId');
  if (!refId) {
    toast('Không xác định được mã sinh viên.', 'danger');
    return;
  }

  try {
    const response = await fetch(`/api/SinhVien/${encodeURIComponent(refId)}/bangdiem/pdf`, {
      headers: { Authorization: `Bearer ${localStorage.getItem('token') || ''}` }
    });
    if (!response.ok) {
      if (response.status === 401) {
        localStorage.clear();
        window.location.href = '/login.html';
      }
      const message = await response.text();
      throw new Error(message || 'Không tải được bảng điểm PDF.');
    }

    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `BangDiem_${refId}.pdf`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  } catch (error) {
    toast('Lỗi tải PDF: ' + error.message, 'danger');
  }
}

async function loadStudentTranscript(svId) {
  const [sv, hocluc, monHocs] = await Promise.all([
    api('/api/SinhVien/' + encodeURIComponent(svId)),
    api('/api/SinhVien/' + encodeURIComponent(svId) + '/hocluc').catch(() => null),
    api('/api/MonHoc').catch(() => [])
  ]);
  _courseScoreConfig = new Map((monHocs || []).map(m => [m.Id, m]));
  document.getElementById('svProfileHoTen').textContent = `${sv.HoTen} (${sv.Id})`;
  document.getElementById('svProfileDetail').textContent = `Lớp: ${sv.LopSinhHoat || 'N/A'} | Khoa: ${sv.TenKhoa || sv.MaKhoa || 'N/A'} | Email: ${sv.Email || 'N/A'}`;
  const bangDiem = sv.BangDiem || [];
  const tongTc = bangDiem.reduce((s, b) => s + (b.SoTinChi || 0), 0);
  document.getElementById('svSumTc').textContent = tongTc;
  document.getElementById('svGpa10').textContent = hocluc?.GPA != null ? Number(hocluc.GPA).toFixed(2) : '\u2014';
  document.getElementById('svXepLoaiBadge').innerHTML = hocluc ? xepLoaiBadge(hocluc.XepLoai) : '<span class="badge bg-secondary">Chưa đủ dữ liệu</span>';

  _transcriptCache = bangDiem;
  document.getElementById('svTranscriptTableBody').innerHTML = bangDiem.length ? bangDiem.map(b => {
    const hasScore = gradeComplete(b);
    return `
    <tr>
      <td class="text-center"><code>${b.MaLHP}</code></td>
      <td><b>${b.TenMon}</b><br/><small class="text-muted">${b.MaMon}</small></td>
      <td class="text-center">${b.SoTinChi}</td>
      <td>${getTeacherName(b.GiangVien)}</td>
      <td class="text-center">${b.DiemThanhPhan?.ChuyenCan ?? '—'}</td>
      <td class="text-center">${b.DiemThanhPhan?.GiuaKy ?? '—'}</td>
      <td class="text-center">${b.DiemThanhPhan?.ThucHanh ?? '—'}</td>
      <td class="text-center">${b.DiemThanhPhan?.CuoiKy ?? '—'}</td>
      <td class="text-center fw-bold text-primary">${hasScore ? Number(b.DiemTongKet).toFixed(1) : gradeStatus(b)}</td>
      <td class="text-center"><span class="badge bg-light text-dark border">${hasScore ? (b.DiemChu || '\u0110\u00e3 nh\u1eadp \u0111i\u1ec3m') : gradeStatus(b)}</span></td>
      <td class="text-center">${!hasScore ? `<span class="badge bg-secondary">${gradeStatus(b)}</span>` : Number(b.DiemTongKet) >= 4 ? '<span class="badge bg-success">\u0110\u1ea1t</span>' : '<span class="badge bg-danger">Ch\u01b0a \u0111\u1ea1t</span>'}</td>
    </tr>`;
  }).join('') : '<tr><td colspan="11" class="text-center py-4 text-muted">Chưa có môn học nào.</td></tr>';

  applyTranscriptPage();
  const cloRows = [];
  bangDiem.forEach(b => {
    (b.DiemCLO || []).forEach(clo => {
      const complete = gradeComplete(b);
      const result = !complete ? '<span class="badge bg-secondary">Ch\u01b0a \u0111\u00e1nh gi\u00e1</span>' : clo.KetQua === '\u0110\u1ea1t'
        ? '<span class="badge bg-success">\u0110\u1ea1t</span>' : clo.KetQua === 'Kh\u00f4ng \u0111\u1ea1t'
          ? '<span class="badge bg-danger">Kh\u00f4ng \u0111\u1ea1t</span>' : '<span class="badge bg-secondary">Ch\u01b0a \u0111\u00e1nh gi\u00e1</span>';
      cloRows.push(`<tr><td class="text-center"><code>${clo.MaCLO}</code></td><td>${b.TenMon}</td><td>Chu\u1ea9n \u0111\u1ea7u ra ${clo.MaCLO}</td><td class="text-center">\u2014</td><td class="text-center">${complete ? (clo.DiemDat ?? '\u2014') : '\u2014'}</td><td class="text-center">${result}</td></tr>`);
    });
  });
  document.getElementById('svCloTableBody').innerHTML = cloRows.length ? cloRows.join('') : '<tr><td colspan="6" class="text-center py-3 text-muted">Chưa có dữ liệu chuẩn đầu ra CLO.</td></tr>';
}

// Lọc bảng điểm sinh viên theo từ khóa
function filterSvTranscriptTable() {
  _transcriptPage = 1;
  applyTranscriptPage();
}

function applyTranscriptPage() {
  const tbody = document.getElementById('svTranscriptTableBody');
  if (!tbody) return;
  const query = (document.getElementById('svTranscriptSearch')?.value || '').trim().toLocaleLowerCase('vi');
  const rows = [...tbody.querySelectorAll('tr')].filter(row => row.cells.length > 1);
  const filtered = rows.filter(row => !query || row.textContent.toLocaleLowerCase('vi').includes(query));
  const pages = Math.max(1, Math.ceil(filtered.length / _transcriptPageSize));
  _transcriptPage = Math.min(_transcriptPage, pages);
  rows.forEach(row => row.hidden = true);
  filtered.slice((_transcriptPage - 1) * _transcriptPageSize, _transcriptPage * _transcriptPageSize).forEach(row => row.hidden = false);
  renderTablePagination('svTranscriptPagination', filtered.length, _transcriptPage, _transcriptPageSize,
    page => { _transcriptPage = page; applyTranscriptPage(); }, size => { _transcriptPageSize = size; _transcriptPage = 1; applyTranscriptPage(); });
}

async function loadDiemSvList(refLhp) {
  const role = localStorage.getItem('role');
  const lop = document.getElementById('diemFilterLop')?.value || '';
  
  const tbody = document.getElementById('diemSvTableBody');
  if (!tbody) return;
  tbody.innerHTML = '<tr><td colspan="6" class="text-center py-4 text-muted"><div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>Đang tải...</td></tr>';

  try {
    let list = [];
    
    if (role === 'giaovien') {
      const refId = localStorage.getItem('refId') || '';
      
      if (lop) {
        const classData = await api(`/api/lophocphan/${encodeURIComponent(lop)}/sinhvien`);
        list = classData.sinhViens || [];
        _currentFilterLhp = lop;
      } else {
        const lhpList = await api('/api/lophocphan?maGv=' + encodeURIComponent(refId));
        const allStudentIds = new Set();

        for (const lhp of lhpList) {
          try {
            const classData = await api(`/api/lophocphan/${lhp.Id}/sinhvien`);
            (classData.sinhViens || []).forEach(sv => {
              if (!allStudentIds.has(sv.Id)) {
                allStudentIds.add(sv.Id);
                list.push(sv);
              }
            });
          } catch (e) {
            console.warn(`Không thể tải lớp ${lhp.Id}:`, e);
          }
        }

        _currentFilterLhp = '';
      }
    } else {
      if (lop) {
        const classData = await api(`/api/lophocphan/${encodeURIComponent(lop)}/sinhvien`);
        list = classData.sinhViens || [];
        _currentFilterLhp = lop;
      } else {
        list = await api('/api/SinhVien');
        _currentFilterLhp = '';
      }
    }

    if (refLhp) {
      list = list.filter(s => {
        const bd = s.BangDiem;
        if (!bd) return false;
        const arr = Array.isArray(bd) ? bd : [bd];
        return arr.some(b => b.MaLHP === refLhp);
      });
      _currentFilterLhp = refLhp;
    }

    window._currentFilterLhp = _currentFilterLhp;

    _diemSvCache = list;
    await fillDiemLopSelect();
    renderDiemSvTable(list);
  } catch (e) {
    tbody.innerHTML = '<tr><td colspan="6" class="text-center py-3 text-danger">Lỗi tải dữ liệu: ' + e.message + '</td></tr>';
    console.error('Lỗi loadDiemSvList:', e);
  }
}

function filterDiemTable() {
  const q = (document.getElementById('diemSearchText').value || '').trim().toLowerCase();
  if (!q) return renderDiemSvTable(_diemSvCache);
  const filtered = _diemSvCache.filter(s =>
    (s.Id || '').toLowerCase().includes(q) ||
    (s.HoTen || '').toLowerCase().includes(q)
  );
  renderDiemSvTable(filtered);
}

function renderDiemSvTableCore(list) {
  const role = localStorage.getItem('role');
  const tbody = document.getElementById('diemSvTableBody');
  const countEl = document.getElementById('diemSvCount');
  countEl.textContent = list.length ? `${list.length} sinh viên` : '';

  if (!list.length) {
    tbody.innerHTML = '<tr><td colspan="6" class="text-center py-4 text-muted">Không có sinh viên nào.</td></tr>';
    return;
  }

  tbody.innerHTML = list.map(sv => {
    const bangDiemRaw = Array.isArray(sv.BangDiem) ? sv.BangDiem : (sv.BangDiem ? [sv.BangDiem] : []);
    const bangDiem = _currentFilterLhp ? bangDiemRaw.filter(b => b.MaLHP === _currentFilterLhp) : bangDiemRaw;

    let gpaHtml = '<span class="text-muted small">—</span>';
    let xepLoaiHtml = '<span class="text-muted small">—</span>';

    if (bangDiem.length > 0) {
      const graded = bangDiem.filter(gradeComplete);
      if (graded.length) {
      const totalTc = graded.reduce((sum, b) => sum + (b.SoTinChi || 0), 0);
      const gpa = totalTc > 0
        ? graded.reduce((sum, b) => sum + b.DiemTongKet * (b.SoTinChi || 0), 0) / totalTc
        : graded.reduce((sum, b) => sum + b.DiemTongKet, 0) / graded.length;
      gpaHtml = `<span class="fw-bold text-primary">${Number(gpa || 0).toFixed(2)}</span>`;
      xepLoaiHtml = xepLoaiBadge(
        gpa >= 9 ? 'Xuất sắc' : gpa >= 8 ? 'Giỏi' : gpa >= 7 ? 'Khá' : gpa >= 5 ? 'Trung bình' : 'Yếu'
      );
      }
    }

    const canEdit = role === 'admin' || (role === 'giaovien' && _scoreEditableLhpIds.has(_currentFilterLhp));
    let inlineActionHtml = '';
    if (canEdit && _currentFilterLhp) {
      const bdList = bangDiem.filter(b => b.MaLHP === _currentFilterLhp);
      inlineActionHtml = bdList.map(bd => `
        <button class="btn btn-sm btn-outline-success me-1" title="Sửa điểm: ${bd.TenMon}"
          onclick="openModalEditDiem('${sv.Id}', '${bd.MaLHP}', '${bd.TenMon}', ${bd.DiemThanhPhan?.ChuyenCan ?? 'null'}, ${bd.DiemThanhPhan?.GiuaKy ?? 'null'}, ${bd.DiemThanhPhan?.ThucHanh ?? 'null'}, ${bd.DiemThanhPhan?.CuoiKy ?? 'null'})">
          <i class="bi bi-pencil-square me-1"></i>Sửa
        </button>
        <button class="btn btn-sm btn-outline-danger me-1" title="Xóa điểm: ${bd.TenMon}"
          onclick="xoaMonKhoiBangDiem('${sv.Id}', '${bd.MaLHP}', '${bd.TenMon}')">
          <i class="bi bi-trash me-1"></i>Xóa điểm
        </button>
        ${role === 'admin' ? `<button class="btn btn-sm btn-danger me-1" title="Xóa sinh viên khỏi lớp: ${bd.TenMon}" onclick="xoaSinhVienKhoiLop('${sv.Id}', '${bd.MaLHP}', '${bd.TenMon}')"><i class="bi bi-person-dash me-1"></i>Xóa khỏi lớp</button>` : ''}`).join('');
    }

    return `
      <tr>
        <td><code class="fw-semibold">${sv.Id}</code></td>
        <td class="fw-semibold">${sv.HoTen}</td>
        <td><span class="badge bg-light text-dark border">${sv.LopSinhHoat || 'N/A'}</span></td>
        <td class="text-center">${gpaHtml}</td>
        <td class="text-center">${xepLoaiHtml}</td>
        <td class="text-end text-nowrap">
          ${inlineActionHtml}
          <button class="btn btn-sm btn-outline-primary" onclick="openChiTietDiem('${sv.Id}', '${_currentFilterLhp || ''}')">
            <i class="bi bi-eye me-1"></i> ${_currentFilterLhp ? 'Xem' : 'Xem chi tiết'}
          </button>
        </td>
      </tr>`;
  }).join('');
}

// Mở modal chi tiết điểm thành phần của 1 sinh viên
async function openChiTietDiem(svId, filterMaLhp) {
  const role = localStorage.getItem('role');
  const currentRefId = localStorage.getItem('refId');
  // Nếu không truyền vào thì dùng filter hiện tại
  if (filterMaLhp === undefined) filterMaLhp = _currentFilterLhp || '';

  // Đặt MSSV để modal push dùng được
  document.getElementById('modalPushSvId').value = svId;

  // Chỉ Admin được thêm đăng ký học phần từ giao diện quản lý điểm.
  const modalPushBtn = document.querySelector('#modalChiTietDiem button[onclick="openModalPushFromChiTiet()"]');
  if (modalPushBtn) {
    modalPushBtn.style.display = role === 'admin' ? '' : 'none';
  }

  // Hiển thị modal ngay với trạng thái loading
  document.getElementById('chiTietSvHoTen').textContent = svId;
  document.getElementById('chiTietSvInfo').textContent = 'Đang tải...';
  document.getElementById('chiTietGpaBadge').innerHTML = '';
  document.getElementById('chiTietDiemBody').innerHTML =
    '<tr><td colspan="11" class="text-center py-4"><div class="spinner-border spinner-border-sm text-primary me-2"></div>Đang tải...</td></tr>';
  new bootstrap.Modal(document.getElementById('modalChiTietDiem')).show();

  try {
    const sv = await api("/api/SinhVien/" + encodeURIComponent(svId));
    let hocluc = null;
    try { hocluc = await api("/api/SinhVien/" + encodeURIComponent(svId) + "/hocluc"); } catch { }

    // Lấy thông tin giảng viên hiện tại của các lớp học phần để kiểm tra quyền
    let currentLhpTeachers = {};
    let currentLhpScorePermissions = {};
    if (role === 'giaovien') {
      try {
        // Lấy thông tin giảng viên hiện tại từ collection LopHocPhan
        const lhpList = await api('/api/lophocphan?maGv=' + encodeURIComponent(currentRefId));
        currentLhpTeachers = Object.fromEntries(
          lhpList.map(lhp => [lhp.Id, lhp.GiangVien?.MaGV])
        );
        currentLhpScorePermissions = Object.fromEntries(
          lhpList.map(lhp => [lhp.Id, lhp.ChoPhepGvNhapSuaDiem === true])
        );
        console.log('Current teacher assignments:', currentLhpTeachers);
      } catch (e) {
        console.warn('Could not fetch current teacher assignments:', e);
      }
    }

    document.getElementById('chiTietSvHoTen').textContent = `${sv.HoTen} (${sv.Id})`;
    document.getElementById('chiTietSvInfo').textContent =
      `Lớp: ${sv.LopSinhHoat || 'Chưa xếp'} | Khoa: ${sv.TenKhoa || sv.MaKhoa || 'N/A'} | Khóa: ${sv.KhoaHoc || 'N/A'}`;

    if (hocluc) {
      document.getElementById('chiTietGpaBadge').innerHTML = `
        <span class="text-muted small me-2">GPA Tích lũy (Hệ 10):</span>
        <span class="fs-5 fw-bold text-primary me-2">${(hocluc.GPA == null ? '\u2014' : Number(hocluc.GPA).toFixed(2))}</span>
        ${xepLoaiBadge(hocluc.XepLoai)}`;
    } else {
      document.getElementById('chiTietGpaBadge').innerHTML = '<span class="badge bg-secondary">Chưa đủ dữ liệu GPA</span>';
    }

    const tbody = document.getElementById('chiTietDiemBody');
    // Nếu đang lọc theo lớp học phần cụ thể, chỉ hiển thị điểm của lớp đó
    // Nếu là giáo viên và không có filter cụ thể, chỉ hiển thị các lớp mà giáo viên đó dạy
    let bangDiemHienThi = sv.BangDiem || [];
    if (filterMaLhp) {
      bangDiemHienThi = bangDiemHienThi.filter(b => b.MaLHP === filterMaLhp);
    } else if (role === 'giaovien') {
      bangDiemHienThi = bangDiemHienThi.filter(b => b.GiangVien?.MaGV === currentRefId);
    }

    if (bangDiemHienThi.length === 0) {
      tbody.innerHTML = filterMaLhp
        ? `<tr><td colspan="11" class="text-center py-4 text-muted">Sinh viên này chưa có điểm cho lớp học phần <code>${filterMaLhp}</code>.</td></tr>`
        : '<tr><td colspan="11" class="text-center py-4 text-muted">Sinh viên này chưa có điểm môn học nào.</td></tr>';
    } else {
      tbody.innerHTML = bangDiemHienThi.map(b => {
        const coQuyenSua = role === 'admin' || (
          role === 'giaovien' &&
          currentLhpTeachers[b.MaLHP] === currentRefId &&
          currentLhpScorePermissions[b.MaLHP] === true
        );

        const complete = gradeComplete(b);
        const thaoTacHtml = coQuyenSua ? `
            <button class="btn btn-sm btn-outline-success me-1" title="Sửa điểm ($set)"
              onclick="openModalEditDiem('${sv.Id}', '${b.MaLHP}', '${b.TenMon}', ${b.DiemThanhPhan?.ChuyenCan ?? 'null'}, ${b.DiemThanhPhan?.GiuaKy ?? 'null'}, ${b.DiemThanhPhan?.ThucHanh ?? 'null'}, ${b.DiemThanhPhan?.CuoiKy ?? 'null'})">
              <i class="bi bi-pencil-square"></i> Sửa
            </button>
            <button class="btn btn-sm btn-outline-danger" title="Xóa điểm"
              onclick="xoaMonKhoiBangDiem('${sv.Id}', '${b.MaLHP}', '${b.TenMon}')">
              <i class="bi bi-trash"></i> Xóa điểm
            </button>` : `<span class="text-muted small">Chỉ xem</span>`;

        const xoaKhoiLopHtml = role === 'admin' ? `<button class="btn btn-sm btn-danger ms-1" title="Xóa khỏi lớp" onclick="xoaSinhVienKhoiLop('${sv.Id}', '${b.MaLHP}', '${b.TenMon}')"><i class="bi bi-person-dash"></i> Xóa khỏi lớp</button>` : '';

        return `
        <tr>
          <td><code class="fw-semibold">${b.MaLHP}</code></td>
          <td>
            <div class="fw-bold">${b.TenMon}</div>
            <small class="text-muted">${b.MaMon}</small>
          </td>
          <td><span class="badge bg-info text-dark">${b.SoTinChi} TC</span></td>
          <td><small>${getTeacherName(b.GiangVien)}</small></td>
          <td class="text-center">${b.DiemThanhPhan?.ChuyenCan ?? '-'}</td>
          <td class="text-center">${b.DiemThanhPhan?.GiuaKy ?? '-'}</td>
          <td class="text-center">${b.DiemThanhPhan?.ThucHanh ?? '-'}</td>
          <td class="text-center">${b.DiemThanhPhan?.CuoiKy ?? '-'}</td>
          <td class="text-center fw-bold text-primary">${complete ? b.DiemTongKet : gradeStatus(b)}</td>
          <td class="text-center"><span class="badge bg-light text-dark border">${complete ? (b.DiemChu || '\u2014') : gradeStatus(b)}</span></td>
          <td class="text-end">${thaoTacHtml}${xoaKhoiLopHtml}</td>
        </tr>`;
      }).join('');
    }

    // Lưu svId hiện tại cho các hàm push/edit/xóa biết cần reload lại modal
    document.getElementById('modalChiTietDiem').dataset.svId = sv.Id;
    document.getElementById('modalChiTietDiem').dataset.maKhoa = sv.MaKhoa || '';

  } catch (err) {
    document.getElementById('chiTietDiemBody').innerHTML =
      `<tr><td colspan="11" class="text-center py-3 text-danger">Không tìm thấy sinh viên: ${svId}</td></tr>`;
  }
}

function cleanupModalBackdrop() {
  document.querySelectorAll('.modal-backdrop').forEach(el => el.remove());
  document.body.classList.remove('modal-open');
  document.body.style.removeProperty('overflow');
  document.body.style.removeProperty('padding-right');
}

function openModalPushFromChiTiet() {
  if (localStorage.getItem('role') !== 'admin') {
    toast('Chỉ Admin được thêm sinh viên vào lớp học phần.', 'warning');
    return;
  }
  const modal = document.getElementById('modalChiTietDiem');
  const svId = modal.dataset.svId || '';
  const maKhoa = modal.dataset.maKhoa || '';
  document.getElementById('modalPushSvId').value = svId;
  fillLhpSelect(maKhoa);
  // Ẩn modal chi tiết tạm, mở modal push
  bootstrap.Modal.getInstance(modal).hide();
  new bootstrap.Modal(document.getElementById('modalPushDiem')).show();
}

function openModalPush() {
  if (localStorage.getItem('role') !== 'admin') {
    toast('Chỉ Admin được thêm sinh viên vào lớp học phần.', 'warning');
    return;
  }
  fillLhpSelect();
}

async function submitPushDiemModal(e) {
  e.preventDefault();
  if (localStorage.getItem('role') !== 'admin') {
    toast('Chỉ Admin được thêm sinh viên vào lớp học phần.', 'warning');
    return;
  }
  const svId = document.getElementById('modalPushSvId').value.trim();
  const sel = document.getElementById('modalPushLhp');
  const opt = sel.selectedOptions[0];
  if (!opt) return toast("Vui lòng chọn Lớp học phần!", "warning");

  const monHocs = await api("/api/MonHoc");
  const mh = monHocs.find(m => m.Id === opt.dataset.mamon);

  const body = {
    MaLHP: sel.value,
    MaMon: opt.dataset.mamon,
    TenMon: opt.dataset.ten,
    SoTinChi: mh?.SoTinChi || 3,
    GiangVien: { MaGV: opt.dataset.gv, HoTen: opt.dataset.gvten, TenGV: opt.dataset.gvten },
    Hocky: opt.dataset.hk,
    NamHoc: opt.dataset.nam,
    DiemThanhPhan: {
      ChuyenCan: optionalScore('modalPushCc'),
      GiuaKy: optionalScore('modalPushGk'),
      ThucHanh: optionalScore('modalPushTh'),
      CuoiKy: optionalScore('modalPushCk')
    }
  };

  try {
    await api("/api/SinhVien/" + encodeURIComponent(svId) + "/diem", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    bootstrap.Modal.getInstance(document.getElementById('modalPushDiem')).hide();
    toast(`Đã thêm môn "${body.TenMon}" vào bảng điểm của ${svId}.`);
    loadDiemSvList();
    openChiTietDiem(svId);
  } catch (err) {
    toast("Lỗi khi thêm môn: " + err.message, "danger");
  }
}

function openModalEditDiem(svId, maLhp, tenMon, cc, gk, th, ck) {
  document.getElementById('editDiemSvId').value = svId;
  document.getElementById('editDiemMaLhp').value = maLhp;
  document.getElementById('editDiemTenMon').innerText = tenMon;
  document.getElementById('editDiemLhpLabel').innerText = "Mã LHP: " + maLhp;
  document.getElementById('editDiemCc').value = cc ?? '';
  document.getElementById('editDiemGk').value = gk ?? '';
  document.getElementById('editDiemTh').value = th ?? '';
  configurePracticeInput('editDiemTh', _courseIdByLhp.get(maLhp));
  document.getElementById('editDiemCk').value = ck ?? '';
  bootstrap.Modal.getOrCreateInstance(document.getElementById('modalEditDiem')).show();
}

async function submitEditDiemModal(e) {
  e.preventDefault();
  const svId = document.getElementById('editDiemSvId').value;
  const maLhp = document.getElementById('editDiemMaLhp').value;
  const body = {
    ChuyenCan: optionalScore('editDiemCc'),
    GiuaKy: optionalScore('editDiemGk'),
    ThucHanh: optionalScore('editDiemTh'),
    CuoiKy: optionalScore('editDiemCk')
  };

  try {
    await api(`/api/SinhVien/${encodeURIComponent(svId)}/diem/${encodeURIComponent(maLhp)}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    const editModal = bootstrap.Modal.getInstance(document.getElementById('modalEditDiem'));
    if (editModal) editModal.hide();
    document.getElementById('modalEditDiem').addEventListener('hidden.bs.modal', () => {
      cleanupModalBackdrop();
      openChiTietDiem(svId);
    }, { once: true });
    toast(`Đã cập nhật điểm môn ${maLhp}.`);
    loadDiemSvList();
  } catch (err) {
    toast("Lỗi khi sửa điểm: " + err.message, "danger");
  }
}

async function xoaMonKhoiBangDiem(svId, maLhp, tenMon) {
  if (!await confirmAction('Bạn có chắc chắn muốn xóa toàn bộ điểm của sinh viên này trong học phần? Sinh viên vẫn được giữ trong danh sách lớp.', { title: 'Xóa điểm', confirmText: 'Xóa điểm', variant: 'danger' })) return;
  try {
    await api(`/api/SinhVien/${encodeURIComponent(svId)}/diem/${encodeURIComponent(maLhp)}`, {
      method: "DELETE"
    });
    toast(`Đã xóa điểm môn "${tenMon}". Sinh viên vẫn ở trong lớp.`);
    loadDiemSvList();
    openChiTietDiem(svId);
  } catch (err) {
    toast("Lỗi khi xóa môn: " + err.message, "danger");
  }
}

async function xoaSinhVienKhoiLop(svId, maLhp, tenMon) {
  if (!await confirmAction(`Xóa sinh viên ${svId} khỏi lớp học phần "${tenMon}" (${maLhp})? Đăng ký sẽ bị hủy và sĩ số lớp giảm.`, { title: 'Xóa khỏi lớp học phần', confirmText: 'Xóa khỏi lớp', variant: 'danger' })) return;
  try {
    await api(`/api/SinhVien/${encodeURIComponent(svId)}/lophocphan/${encodeURIComponent(maLhp)}`, { method: 'DELETE' });
    toast(`Đã xóa ${svId} khỏi lớp ${maLhp}.`);
    loadDiemSvList();
    openChiTietDiem(svId);
  } catch (err) { toast('Lỗi khi xóa sinh viên khỏi lớp: ' + err.message, 'danger'); }
}

async function fillLhpSelect(maKhoa) {
  const [allLhp, allMonHoc] = await Promise.all([
    api("/api/LopHocPhan"),
    api("/api/MonHoc")
  ]);

  _courseScoreConfig = new Map(allMonHoc.map(m => [m.Id, m]));
  _courseIdByLhp = new Map(allLhp.map(l => [l.Id, l.MaMon]));
  // Teachers can only enter scores in sections explicitly enabled by an admin.
  if (localStorage.getItem('role') === 'giaovien') {
    _scoreEditableLhpIds = new Set(allLhp.filter(l => l.ChoPhepGvNhapSuaDiem === true).map(l => l.Id));
  }

  // Lọc LHP theo khoa: chỉ giữ lớp có MaMon thuộc MonHoc của khoa đó
  let filtered = localStorage.getItem('role') === 'giaovien'
    ? allLhp.filter(l => l.ChoPhepGvNhapSuaDiem === true)
    : allLhp;
  if (maKhoa) {
    const maMonOfKhoa = new Set(
      allMonHoc.filter(m => m.MaKhoa === maKhoa).map(m => m.Id)
    );
    filtered = allLhp.filter(l => maMonOfKhoa.has(l.MaMon));
  }

  const makeOption = l =>
    `<option value="${l.Id}" data-mamon="${l.MaMon}" data-ten="${l.TenMon}" data-hk="${l.Hocky}" data-nam="${l.NamHoc}" data-gv="${l.GiangVien?.MaGV || ""}" data-gvten="${getTeacherName(l.GiangVien)}">${l.Id} – ${l.TenMon}</option>`;

  const header = maKhoa && filtered.length < allLhp.length
    ? `<option value="" disabled>── ${filtered.length} lớp thuộc khoa ${maKhoa} ──</option>`
    : `<option value="" disabled>── ${allLhp.length} lớp học phần ──</option>`;

  const options = header + filtered.map(makeOption).join("");

  const p1 = document.getElementById("pushLhp");
  if (p1) p1.innerHTML = options;
  const p2 = document.getElementById("modalPushLhp");
  if (p2) {
    p2.innerHTML = options;
    p2.onchange = () => configurePracticeInput('modalPushTh', p2.selectedOptions[0]?.dataset.mamon);
    configurePracticeInput('modalPushTh', p2.selectedOptions[0]?.dataset.mamon);
  }
}

// Table paging and debounced client filtering for the grading views.
const _renderDiemSvTableAll = renderDiemSvTableCore;
let _diemSearchTimer;
function filterDiemTable() {
  clearTimeout(_diemSearchTimer);
  _diemSearchTimer = setTimeout(() => {
    _diemPage = 1;
    const q = (document.getElementById('diemSearchText')?.value || '').trim().toLocaleLowerCase('vi');
    const rows = _diemSvCache.filter(s => `${s.Id || ''} ${s.HoTen || ''}`.toLocaleLowerCase('vi').includes(q));
    renderDiemSvTable(rows);
  }, 180);
}
function renderDiemSvTable(list) {
  const count = document.getElementById('diemSvCount');
  if (count) count.textContent = `${list.length}/${_diemSvCache.length} sinh viên`;
  const size = _diemPageSize;
  const pages = Math.max(1, Math.ceil(list.length / size));
  _diemPage = Math.min(_diemPage, pages);
  const pageItems = list.slice((_diemPage - 1) * size, _diemPage * size);
  _renderDiemSvTableAll(pageItems);
  if (count) count.textContent = `${list.length}/${_diemSvCache.length} sinh viên`;
  renderTablePagination('diemSvPagination', list.length, _diemPage, size,
    page => { _diemPage = page; filterDiemTableNow(); }, pageSize => { _diemPageSize = pageSize; _diemPage = 1; filterDiemTableNow(); });
}
function filterDiemTableNow() {
  const q = (document.getElementById('diemSearchText')?.value || '').trim().toLocaleLowerCase('vi');
  renderDiemSvTable(_diemSvCache.filter(s => `${s.Id || ''} ${s.HoTen || ''}`.toLocaleLowerCase('vi').includes(q)));
}

async function loadBaoCao() {
  fillLopSelects();
  try { const courses = await api('/api/MonHoc'); _courseScoreConfig = new Map(courses.map(m => [m.Id, m])); } catch { _courseScoreConfig = new Map(); }
  const role = localStorage.getItem('role');
  const refId = localStorage.getItem('refId');
  const lop = document.getElementById('bcLop')?.value || '';
  
  const tbody = document.getElementById('bcTable');
  tbody.innerHTML = '<tr class="loading-row"><td colspan="7" class="text-center text-muted"><div class="spinner-border spinner-border-sm text-primary me-2"></div>Đang tải...</td></tr>';

  try {
    let list = [];
    
    if (role === 'giaovien') {
      if (!refId) throw new Error('Không xác định được mã giảng viên.');

      // Lấy danh sách lớp học phần mà giáo viên dạy
      const lhpList = await api('/api/lophocphan?maGv=' + encodeURIComponent(refId));

      if (!lhpList.length) {
        tbody.innerHTML = '<tr><td colspan="7" class="text-center py-4 text-muted">Bạn chưa được phân công dạy lớp nào.</td></tr>';
        return;
      }

      // Nếu chọn lớp cụ thể thì chỉ lấy lớp đó, ngược lại lấy tất cả
      // lop có dạng "LHP_MH001_HK1_2026 – Cơ sở dữ liệu NoSQL"
      const selectedMaLhp = lop ? lop.split(' – ')[0].trim() : null;
      const lhpToLoad = selectedMaLhp
        ? lhpList.filter(l => l.Id === selectedMaLhp)
        : lhpList;

      // Tập hợp mã LHP mà giáo viên đang phụ trách (dùng để lọc BangDiem)
      const maLhpSet = new Set(lhpToLoad.map(l => l.Id));

      // Thu thập điểm từng sinh viên qua tất cả lớp học phần
      // Key: svId, Value: { Id, HoTen, LopSinhHoat, diemEntries: [] }
      const studentMap = new Map();

      for (const lhp of lhpToLoad) {
        try {
          const classData = await api(`/api/lophocphan/${lhp.Id}/sinhvien`);
          for (const sv of (classData.sinhViens || [])) {
            if (!studentMap.has(sv.Id)) {
              studentMap.set(sv.Id, {
                Id: sv.Id,
                HoTen: sv.HoTen,
                LopSinhHoat: sv.LopSinhHoat || 'N/A',
                diemEntries: []
              });
            }
            // BangDiem có thể là array hoặc object đơn (endpoint trả 1 bản ghi cho lớp đó)
            const rawBD = sv.BangDiem;
            const bangDiem = Array.isArray(rawBD) ? rawBD : (rawBD ? [rawBD] : []);
            // Chỉ thêm các điểm thuộc lớp học phần mà giáo viên dạy
            for (const b of bangDiem) {
              if (maLhpSet.has(b.MaLHP)) {
                studentMap.get(sv.Id).diemEntries.push(b);
              }
            }
          }
        } catch (e) {
          console.warn(`Không thể tải lớp ${lhp.Id}:`, e);
        }
      }

      // Tính GPA và xếp loại cho từng sinh viên dựa trên điểm đã thu thập
      const studentReports = [];
      for (const [, data] of studentMap) {
        const filteredBD = data.diemEntries.filter(gradeComplete);
        const tongTinChi = filteredBD.reduce((s, b) => s + (b.SoTinChi || 0), 0);
        const tongDiemHeSo = filteredBD.reduce((s, b) => s + (b.DiemTongKet || 0) * (b.SoTinChi || 0), 0);
        const gpa = tongTinChi > 0 ? Math.round((tongDiemHeSo / tongTinChi) * 100) / 100 : null;
        let xepLoai = 'Chưa có điểm';
        if (filteredBD.length) {
          xepLoai = 'Yếu';
          if (gpa >= 9) xepLoai = 'Xuất sắc';
          else if (gpa >= 8) xepLoai = 'Giỏi';
          else if (gpa >= 7) xepLoai = 'Khá';
          else if (gpa >= 5) xepLoai = 'Trung bình';
        }
        studentReports.push({
          Id: data.Id,
          HoTen: data.HoTen,
          LopSinhHoat: data.LopSinhHoat,
          TongTinChi: tongTinChi,
          GPA: gpa,
          XepLoai: xepLoai
        });
      }

      list = studentReports;
      
    } else {
      // Admin có thể xuất báo cáo tất cả sinh viên
      const qs = lop ? '?lop=' + encodeURIComponent(lop) : '';
      list = await api('/api/Reports/hocluc' + qs);
    }

    // Stat cards
    const countEl = document.getElementById('bcCountBadge');
    if (countEl) countEl.textContent = `${list.length} sinh viên`;

    const statEl = document.getElementById('bcStatCards');
    if (statEl && list.length) {
      const gpas = list.filter(r => r.GPA != null).map(r => Number(r.GPA));
      const avg = gpas.length ? (gpas.reduce((a,b)=>a+b,0)/gpas.length).toFixed(2) : '?';
      const max = gpas.length ? Math.max(...gpas).toFixed(2) : '?';
      const min = gpas.length ? Math.min(...gpas).toFixed(2) : '?';
      const pass = list.filter(r => r.GPA != null && Number(r.GPA) >= 5).length;
      const failed = list.filter(r => r.GPA != null && Number(r.GPA) < 5).length;
      statEl.innerHTML = [
        ['Tổng sinh viên', list.length, 'bi-people-fill', 'primary'],
        ['GPA trung bình', avg, 'bi-bar-chart-fill', 'info'],
        ['GPA cao nhất', max, 'bi-trophy-fill', 'success'],
        ['GPA thấp nhất', min, 'bi-arrow-down-circle', 'warning'],
        ['Đạt (GPA ≥ 5)', pass, 'bi-check-circle-fill', 'success'],
        ['Không đạt', list.length - pass, 'bi-x-circle-fill', 'danger'],
      ].map(([t,n,i,c]) => {
        const bgMap={primary:'#dbeafe',success:'#dcfce7',warning:'#fef9c3',info:'#e0f2fe',danger:'#fee2e2'};
        const fgMap={primary:'#1d4ed8',success:'#15803d',warning:'#a16207',info:'#0369a1',danger:'#b91c1c'};
        return `<div class="col-6 col-md-2"><div class="card stat-card p-3">
          <div class="d-flex justify-content-between align-items-center">
            <div><div class="text-muted small mb-1">${t}</div><div class="fs-5 fw-bold" style="color:${fgMap[c]}">${n}</div></div>
            <div class="stat-icon" style="background:${bgMap[c]};color:${fgMap[c]}"><i class="bi ${i}"></i></div>
          </div></div></div>`;
      }).join('');
    }

    // Phân bố xếp loại
    const distEl = document.getElementById('bcDistribution');
    if (distEl && list.length) {
      const xlMap = { 'Xuất sắc':0,'Giỏi':0,'Khá':0,'Trung bình':0,'Yếu':0 };
      list.forEach(r => { if (xlMap[r.XepLoai] !== undefined) xlMap[r.XepLoai]++; });
      const xlStyle = {
        'Xuất sắc': { bg:'#fef3c7', fg:'#92400e', icon:'bi-star-fill' },
        'Giỏi':     { bg:'#dcfce7', fg:'#166534', icon:'bi-award-fill' },
        'Khá':      { bg:'#dbeafe', fg:'#1e40af', icon:'bi-hand-thumbs-up-fill' },
        'Trung bình':{ bg:'#f3f4f6', fg:'#374151', icon:'bi-dash-circle' },
        'Yếu':      { bg:'#fee2e2', fg:'#991b1b', icon:'bi-exclamation-triangle-fill' },
      };
      distEl.innerHTML = Object.entries(xlMap).map(([xl, cnt]) => {
        const s = xlStyle[xl]; const pct = list.length ? Math.round(cnt/list.length*100) : 0;
        return `<div class="col">
          <div class="p-3 rounded-3 text-center" style="background:${s.bg};">
            <i class="bi ${s.icon} fs-3 mb-2 d-block" style="color:${s.fg}"></i>
            <div class="fw-bold fs-4" style="color:${s.fg}">${cnt}</div>
            <div class="small fw-semibold" style="color:${s.fg}">${xl}</div>
            <div class="small text-muted">${pct}%</div>
          </div>
        </div>`;
      }).join('');
    }

    // Bảng chi tiết
    if (!list.length) {
      tbody.innerHTML = '<tr><td colspan="7"><div class="empty-state"><i class="bi bi-inbox"></i><div>Không có dữ liệu</div></div></td></tr>';
      return;
    }
    tbody.innerHTML = list.map((r, idx) => {
      const gpa = r.GPA == null ? null : Number(r.GPA);
      const gpaClass = gpa == null ? '' : gpa >= 8 ? 'score-pass' : gpa >= 5 ? 'score-avg' : 'score-fail';
      const studentId = r.Id ?? r._id ?? '';
      return `<tr>
        <td class="text-muted small">${idx+1}</td>
        <td><code class="fw-semibold text-primary">${studentId}</code></td>
        <td class="fw-semibold">${r.HoTen}</td>
        <td><span class="badge bg-light text-dark border">${r.LopSinhHoat}</span></td>
        ${role !== 'giaovien' ? `<td class="text-center">${r.TongTinChi} TC</td>` : ''}
        <td class="text-center"><span class="${gpaClass} fs-6">${gpa == null ? '?' : gpa.toFixed(2)}</span></td>
        <td class="text-center">${xepLoaiBadge(r.XepLoai)}</td>
      </tr>`;
    }).join('');
  } catch(e) {
    tbody.innerHTML = `<tr><td colspan="7" class="text-center text-danger py-3"><i class="bi bi-exclamation-triangle me-2"></i>${e.message}</td></tr>`;
  }
}

function exportSv() {
  const lop = document.getElementById("exLop").value;
  const qs = lop ? "?lop=" + encodeURIComponent(lop) : "";
  const token = localStorage.getItem('token');
  // Dùng fetch để gắn Authorization rồi tải file
  fetch("/api/ImportExport/sinhvien" + qs, { headers: { Authorization: `Bearer ${token}` } })
    .then(async response => {
      if (!response.ok) throw new Error(await response.text() || response.statusText);
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = lop ? `bangdiem_${lop}.json` : 'bangdiem_sinhvien.json';
      a.click();
      URL.revokeObjectURL(url);
    })
    .catch(err => toast('Lỗi export: ' + err.message, 'danger'));
}

async function importSv() {
  const f = document.getElementById("imFile").files[0];
  if (!f) return toast("Chọn file JSON", "warning");
  const fd = new FormData();
  fd.append("file", f);
  try {
    const data = await api("/api/ImportExport/sinhvien", { method: "POST", body: fd });
    toast(`Import xong: ${data.imported} SV (upsert ${data.upserted}, sửa ${data.modified})`);
  } catch (err) {
    toast('Lỗi import: ' + err.message, 'danger');
  }
}

// ==================== QUẢN LÝ TÀI KHOẢN (ADMIN) ====================
let _accountCache = [];
let _accountPage = 1;
let _accountPageSize = 10;

async function loadAccounts() {
  const body = document.getElementById('accTableBody');
  if (!body) return;
  body.innerHTML = '<tr><td colspan="6" class="text-center py-4"><span class="spinner-border spinner-border-sm text-primary me-2" role="status"></span>Đang tải danh sách tài khoản...</td></tr>';
  const count = document.getElementById('accCountBadge');
  if (count) count.textContent = 'Đang tải danh sách...';
  try {
    _accountCache = await api('/api/TaiKhoan');
    _accountPage = 1;
    renderAccounts();
  } catch (err) {
    body.innerHTML = '<tr><td colspan="6" class="text-center text-danger py-4">Không tải được danh sách tài khoản.<br/><button class="btn btn-sm btn-outline-primary mt-2" onclick="loadAccounts()"><i class="bi bi-arrow-clockwise me-1"></i>Thử lại</button></td></tr>';
    if (count) count.textContent = 'Không tải được dữ liệu';
    toast('Không tải được danh sách tài khoản: ' + err.message, 'danger');
  }
}

function accountRoleBadge(role) {
  return role === 'admin' ? '<span class="badge bg-danger">Admin</span>' : role === 'giaovien' ? '<span class="badge bg-warning text-dark">Giáo viên</span>' : '<span class="badge bg-info text-dark">Sinh viên</span>';
}

function renderAccounts() {
  const body = document.getElementById('accTableBody');
  const count = document.getElementById('accCountBadge');
  if (!body) return;
  const role = document.getElementById('accFilterRole')?.value || '';
  const q = (document.getElementById('accSearchText')?.value || '').trim().toLocaleLowerCase('vi');
  const list = _accountCache.filter(a => {
    const roleMatch = !role || a.Role === role;
    const haystack = (a.Id || '') + ' ' + (a.HoTen || '') + ' ' + (a.RefId || '');
    return roleMatch && (!q || haystack.toLocaleLowerCase('vi').includes(q));
  });
  if (count) count.textContent = 'Tổng ' + _accountCache.length + ' tài khoản · ' + list.length + ' kết quả';
  const pages = Math.max(1, Math.ceil(list.length / _accountPageSize));
  _accountPage = Math.min(_accountPage, pages);
  const visible = list.slice((_accountPage - 1) * _accountPageSize, _accountPage * _accountPageSize);
  body.innerHTML = visible.length ? visible.map(a =>
    '<tr><td><code class="fw-semibold">' + a.Id + '</code></td>' +
    '<td>' + a.HoTen + '</td><td>' + accountRoleBadge(a.Role) + '</td>' +
    '<td>' + (a.RefId ? '<code>' + a.RefId + '</code>' : '<span class="text-muted">—</span>') + '</td>' +
    '<td>' + (a.IsActive ? '<span class="badge bg-success"><i class="bi bi-check-circle me-1"></i>Hoạt động</span>' : '<span class="badge bg-secondary"><i class="bi bi-lock me-1"></i>Đã khóa</span>') + '</td>' +
    '<td class="text-end"><div class="table-actions">' +
    '<button class="btn btn-sm btn-outline-primary" data-bs-toggle="tooltip" data-account-action="edit" data-account-id="' + encodeURIComponent(a.Id) + '" title="Chỉnh sửa" aria-label="Chỉnh sửa tài khoản"><i class="bi bi-pencil"></i></button>' +
    '<button class="btn btn-sm ' + (a.IsActive ? 'btn-outline-warning' : 'btn-outline-success') + '" data-bs-toggle="tooltip" data-account-action="status" data-account-id="' + encodeURIComponent(a.Id) + '" data-active-next="' + (!a.IsActive) + '" title="' + (a.IsActive ? 'Vô hiệu hóa' : 'Kích hoạt') + '" aria-label="' + (a.IsActive ? 'Vô hiệu hóa' : 'Kích hoạt') + ' tài khoản"><i class="bi ' + (a.IsActive ? 'bi-lock' : 'bi-unlock') + '"></i></button>' +
    '<button class="btn btn-sm btn-outline-danger" data-bs-toggle="tooltip" data-account-action="delete" data-account-id="' + encodeURIComponent(a.Id) + '" title="Xóa" aria-label="Xóa tài khoản"><i class="bi bi-trash"></i></button>' +
    '</div></td></tr>'
  ).join('') : '<tr><td colspan="6" class="text-center py-4 text-muted">Không có tài khoản phù hợp.</td></tr>';
  body.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(el => bootstrap.Tooltip.getOrCreateInstance(el));
  body.querySelectorAll('[data-account-action="edit"]').forEach(el => el.addEventListener('click', () => openModalEditAccount(decodeURIComponent(el.dataset.accountId))));
  body.querySelectorAll('[data-account-action="status"]').forEach(el => el.addEventListener('click', () => toggleAccountStatus(decodeURIComponent(el.dataset.accountId), el.dataset.activeNext === 'true')));
  body.querySelectorAll('[data-account-action="delete"]').forEach(el => el.addEventListener('click', () => deleteAccount(decodeURIComponent(el.dataset.accountId))));
  renderTablePagination('accPagination', list.length, _accountPage, _accountPageSize,
    page => { _accountPage = page; renderAccounts(); },
    size => { _accountPageSize = size; _accountPage = 1; renderAccounts(); });
}

const debouncedFilterAccounts = debounceClient(() => { _accountPage = 1; renderAccounts(); });
function openModalCreateAccount() {
  document.getElementById('modalAccountTitle').innerHTML = '<i class="bi bi-person-plus-fill me-1"></i> Thêm tài khoản';
  document.getElementById('accIsEdit').value = '';
  document.getElementById('accUsername').value = '';
  document.getElementById('accUsername').disabled = false;
  document.getElementById('accPassword').value = '';
  document.getElementById('accPassword').required = true;
  document.getElementById('accPasswordRequired').style.display = '';
  document.getElementById('accPasswordHelp').style.display = 'none';
  document.getElementById('accHoTen').value = '';
  document.getElementById('accRole').value = 'sinhvien';
  document.getElementById('accRefId').value = '';
  document.getElementById('accIsActive').checked = true;
  new bootstrap.Modal(document.getElementById('modalAccount')).show();
}

async function openModalEditAccount(id) {
  const a = await api('/api/TaiKhoan/' + encodeURIComponent(id));
  document.getElementById('modalAccountTitle').innerHTML = '<i class="bi bi-pencil-square me-1"></i> Sửa tài khoản';
  document.getElementById('accIsEdit').value = a.Id;
  document.getElementById('accUsername').value = a.Id;
  document.getElementById('accUsername').disabled = true;
  document.getElementById('accPassword').value = '';
  document.getElementById('accPassword').required = false;
  document.getElementById('accPasswordRequired').style.display = 'none';
  document.getElementById('accPasswordHelp').style.display = '';
  document.getElementById('accHoTen').value = a.HoTen;
  document.getElementById('accRole').value = a.Role;
  document.getElementById('accRefId').value = a.RefId || '';
  document.getElementById('accIsActive').checked = a.IsActive;
  new bootstrap.Modal(document.getElementById('modalAccount')).show();
}

async function submitSaveAccount(e) {
  e.preventDefault();
  const form = e.currentTarget;
  if (!validateClientForm(form)) return;
  const saveButton = document.getElementById('accSaveBtn');
  if (saveButton?.disabled) return;
  const saveButtonHtml = saveButton?.innerHTML;
  if (saveButton) {
    saveButton.disabled = true;
    saveButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Đang lưu...';
  }
  const id = document.getElementById('accUsername').value.trim();
  const oldId = document.getElementById('accIsEdit').value;
  const payload = {
    Password: document.getElementById('accPassword').value || null,
    Role: document.getElementById('accRole').value,
    RefId: document.getElementById('accRefId').value.trim() || null,
    HoTen: document.getElementById('accHoTen').value.trim(),
    IsActive: document.getElementById('accIsActive').checked
  };
  try {
    if (oldId) await api('/api/TaiKhoan/' + encodeURIComponent(oldId), { method: 'PUT', headers: {'Content-Type': 'application/json'}, body: JSON.stringify(payload) });
    else await api('/api/TaiKhoan', { method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify({...payload, Id: id, Password: document.getElementById('accPassword').value}) });
    bootstrap.Modal.getInstance(document.getElementById('modalAccount')).hide();
    toast(oldId ? 'Đã cập nhật tài khoản.' : 'Đã tạo tài khoản mới.');
    loadAccounts();
  } catch (err) { toast('Lỗi lưu tài khoản: ' + err.message, 'danger'); }
  finally {
    if (saveButton) {
      saveButton.disabled = false;
      saveButton.innerHTML = saveButtonHtml;
    }
  }
}

async function toggleAccountStatus(id, isActive) {
  if (!await confirmAction(isActive ? `Kích hoạt tài khoản ${id}?` : `Vô hiệu hóa tài khoản ${id}?`, { title: isActive ? 'Kích hoạt tài khoản' : 'Vô hiệu hóa tài khoản', confirmText: isActive ? 'Kích hoạt' : 'Vô hiệu hóa', variant: isActive ? 'primary' : 'danger' })) return;
  try { await api('/api/TaiKhoan/' + encodeURIComponent(id) + '/status', {method:'PUT', headers:{'Content-Type':'application/json'}, body: JSON.stringify(isActive)}); toast(isActive ? 'Đã kích hoạt tài khoản.' : 'Đã khóa tài khoản.'); loadAccounts(); }
  catch (err) { toast(err.message, 'danger'); }
}

async function deleteAccount(id) {
  if (!await confirmAction(`Xóa vĩnh viễn tài khoản ${id}? Thao tác này không thể hoàn tác.`, { title: 'Xóa tài khoản', confirmText: 'Xóa vĩnh viễn', variant: 'danger' })) return;
  try { await api('/api/TaiKhoan/' + encodeURIComponent(id), {method:'DELETE'}); toast('Đã xóa tài khoản.', 'warning'); loadAccounts(); }
  catch (err) { toast(err.message, 'danger'); }
}

// ==================== ĐĂNG KÝ HỌC PHẦN (SINH VIÊN) ====================
let _openLhpCache = [];

async function loadDangKyHocPhanPage() {
  const svId = localStorage.getItem('refId');
  if (!svId) return;
  try {
    const [sv, lhp] = await Promise.all([
      api('/api/SinhVien/' + encodeURIComponent(svId)),
      api('/api/LopHocPhan')
    ]);
    document.getElementById('studentInfoBadge').innerHTML = `<span class="badge bg-primary">${sv.HoTen} - ${sv.Id}</span>`;
    const registered = sv.BangDiem || [];
    const regIds = new Set(registered.map(b => b.MaLHP));
    const lhpById = new Map(lhp.map(item => [item.Id, item]));
    document.getElementById('countRegistered').textContent = registered.length;
    document.getElementById('sumTcRegistered').textContent = registered.reduce((s, b) => s + (b.SoTinChi || 0), 0);
    document.getElementById('registeredTableBody').innerHTML = registered.length ? registered.map(b => {
      const hasScore = b.DiemTongKet != null && ['ChuyenCan', 'GiuaKy', 'ThucHanh', 'CuoiKy'].some(key => b.DiemThanhPhan?.[key] != null);
      const classIsOpen = lhpById.get(b.MaLHP)?.DaMo === true;
      const scoreStatus = hasScore
        ? `<span class="badge bg-success">${b.DiemTongKet} (${b.DiemChu || 'Đã nhập điểm'})</span>`
        : '<span class="badge bg-secondary">Chưa có điểm</span>';
      const action = hasScore
        ? '<span class="badge bg-success"><i class="bi bi-check-circle me-1"></i>Đã có điểm</span>'
        : classIsOpen
          ? `<button class="btn btn-sm btn-outline-danger" onclick="huyDangKy('${b.MaLHP}', '${b.TenMon}')"><i class="bi bi-x-circle me-1"></i>Hủy</button>`
          : '<span class="badge bg-secondary"><i class="bi bi-lock me-1"></i>Lớp đã đóng</span>';
      return `<tr><td><code>${b.MaLHP}</code></td><td><b>${b.TenMon}</b><br/><small class="text-muted">${b.MaMon}</small></td><td>${b.SoTinChi}</td><td>${getTeacherName(b.GiangVien)}</td><td>${b.Hocky} ${b.NamHoc}</td><td>${scoreStatus}</td><td class="text-end">${action}</td></tr>`;
    }).join('') : '<tr><td colspan="7" class="text-center py-3 text-muted">Chưa đăng ký học phần nào.</td></tr>';
    // Chỉ hiển thị các lớp đang mở (DaMo = true) và chưa đăng ký
    _openLhpCache = lhp.filter(l => l.DaMo !== false && !regIds.has(l.Id));
    renderOpenLhp(_openLhpCache);
  } catch (err) { toast('Lỗi tải đăng ký học phần: ' + err.message, 'danger'); }
}

function filterOpenLhp() {
  const q = document.getElementById('searchLhpOpen').value.trim().toLowerCase();
  renderOpenLhp(_openLhpCache.filter(l => [l.Id, l.MaMon, l.TenMon, getTeacherName(l.GiangVien)].some(v => (v || '').toLowerCase().includes(q))));
}

function renderOpenLhpCore(list) {
  const body = document.getElementById('openLhpTableBody');
  if (!body) return;
  body.innerHTML = list.length ? list.map(l => {
    const full = l.SiSoToiDa > 0 && (l.SiSoHienTai || 0) >= l.SiSoToiDa;
    const daMo = l.DaMo !== false;
    return `<tr><td><code>${l.Id}</code></td><td><b>${l.TenMon}</b><br/><small class="text-muted">${l.MaMon}</small></td><td>${l.SoTinChi || '—'}</td><td>${getTeacherName(l.GiangVien)}</td><td>${l.LichHoc || '—'}<br/><small class="text-muted">${l.PhongHoc || ''}</small></td><td class="text-center">${l.SiSoHienTai || 0}/${l.SiSoToiDa} ${full ? '<span class="badge bg-danger">Đầy</span>' : '<span class="badge bg-success">Còn chỗ</span>'}</td><td class="text-end">${daMo ? `<button class="btn btn-sm btn-primary" ${full ? 'disabled' : ''} onclick="dangKyHocPhan('${l.Id}', '${l.TenMon}')"><i class="bi bi-plus-circle me-1"></i>Đăng ký</button>` : '<span class="badge bg-secondary"><i class="bi bi-lock me-1"></i>Đã đóng</span>'}</td></tr>`;
  }).join('') : '<tr><td colspan="7" class="text-center py-4 text-muted">Không còn lớp học phần phù hợp.</td></tr>';
}

async function dangKyHocPhan(maLhp, tenMon) {
  if (!await confirmAction(`Đăng ký môn ${tenMon} (${maLhp})?`, { title: 'Đăng ký học phần', confirmText: 'Đăng ký' })) return;
  try { await api('/api/SinhVien/dangky/' + encodeURIComponent(maLhp), {method:'POST'}); toast('Đăng ký học phần thành công.'); loadDangKyHocPhanPage(); }
  catch (err) { toast(err.message, 'danger'); }
}

async function huyDangKy(maLhp, tenMon) {
  if (!await confirmAction(`Hủy đăng ký môn ${tenMon}?`, { title: 'Hủy đăng ký', confirmText: 'Hủy đăng ký', variant: 'danger' })) return;
  try { await api('/api/SinhVien/huydangky/' + encodeURIComponent(maLhp), {method:'DELETE'}); toast('Đã hủy đăng ký.', 'warning'); loadDangKyHocPhanPage(); }
  catch (err) { toast(err.message, 'danger'); }
}

const _renderOpenLhpOriginal = renderOpenLhpCore;
let _openLhpPage = 1, _openLhpPageSize = 10, _openLhpTimer;
function filterOpenLhp() {
  clearTimeout(_openLhpTimer);
  _openLhpTimer = setTimeout(() => {
    const q = (document.getElementById('searchLhpOpen')?.value || '').trim().toLocaleLowerCase('vi');
    const semester = document.getElementById('semesterFilter')?.value || '';
    const list = _openLhpCache.filter(l => (!semester || `${l.Hocky} ${l.NamHoc}` === semester) &&
      [l.Id, l.MaMon, l.TenMon, getTeacherName(l.GiangVien)].some(v => (v || '').toLocaleLowerCase('vi').includes(q)));
    _openLhpPage = 1;
    renderOpenLhp(list);
  }, 180);
}
function renderOpenLhp(list) {
  const size = _openLhpPageSize;
  const pages = Math.max(1, Math.ceil(list.length / size));
  _openLhpPage = Math.min(_openLhpPage, pages);
  const page = list.slice((_openLhpPage - 1) * size, _openLhpPage * size);
  _renderOpenLhpOriginal(page);
  document.querySelectorAll('#openLhpTableBody tr').forEach(row => {
    const capacityCell = row.cells[5];
    if (!capacityCell || capacityCell.querySelector('.progress')) return;
    const match = capacityCell.textContent.match(/(\d+)\s*\/\s*(\d+)/);
    if (!match) return;
    const used = Number(match[1]), max = Number(match[2]);
    const pct = max ? Math.min(100, Math.round(used / max * 100)) : 0;
    capacityCell.insertAdjacentHTML('beforeend', `<div class="progress mt-1" role="progressbar" aria-valuenow="${pct}" aria-valuemin="0" aria-valuemax="100"><div class="progress-bar ${pct >= 100 ? 'bg-danger' : ''}" style="width:${pct}%"></div></div>`);
  });
  const semesters = [...new Set(_openLhpCache.map(l => `${l.Hocky} ${l.NamHoc}`))].sort();
  const select = document.getElementById('semesterFilter');
  if (select && select.options.length === 1) select.insertAdjacentHTML('beforeend', semesters.map(v => `<option>${v}</option>`).join(''));
  renderTablePagination('openLhpPagination', list.length, _openLhpPage, size,
    pageNo => { _openLhpPage = pageNo; filterOpenLhpNow(); }, pageSize => { _openLhpPageSize = pageSize; _openLhpPage = 1; filterOpenLhpNow(); });
}
function filterOpenLhpNow() {
  const q = (document.getElementById('searchLhpOpen')?.value || '').trim().toLocaleLowerCase('vi');
  const semester = document.getElementById('semesterFilter')?.value || '';
  renderOpenLhp(_openLhpCache.filter(l => (!semester || `${l.Hocky} ${l.NamHoc}` === semester) &&
    [l.Id, l.MaMon, l.TenMon, getTeacherName(l.GiangVien)].some(v => (v || '').toLocaleLowerCase('vi').includes(q))));
}

const _clientTableState = new Map();
function observeClientTable(tbodyId, pagerId, searchId) {
  const tbody = document.getElementById(tbodyId);
  if (!tbody) return;
  const state = { page: 1, size: 10, busy: false };
  _clientTableState.set(tbodyId, state);
  const update = () => {
    if (state.busy) return;
    const query = (document.getElementById(searchId)?.value || '').trim().toLocaleLowerCase('vi');
    const rows = [...tbody.querySelectorAll('tr')].filter(row => row.cells.length > 1);
    const shown = rows.filter(row => !query || row.textContent.toLocaleLowerCase('vi').includes(query));
    state.page = Math.min(state.page, Math.max(1, Math.ceil(shown.length / state.size)));
    rows.forEach(row => row.hidden = true);
    shown.slice((state.page - 1) * state.size, state.page * state.size).forEach(row => row.hidden = false);
    renderTablePagination(pagerId, shown.length, state.page, state.size,
      page => { state.page = page; update(); }, size => { state.size = size; state.page = 1; update(); });
  };
  const observer = new MutationObserver(() => { state.page = 1; update(); });
  state.update = update;
  observer.observe(tbody, { childList: true });
  setTimeout(update, 0);
}
function filterReportRows() { const state = _clientTableState.get('bcTable'); if (state) { state.page = 1; state.update(); } }

