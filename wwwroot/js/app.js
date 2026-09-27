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
  const box = document.getElementById("alertBox");
  if (!box) return;
  box.innerHTML =
    `<div class="alert alert-${type} alert-dismissible fade show py-2">${msg}
       <button class="btn-close" data-bs-dismiss="alert"></button></div>`;
  setTimeout(() => { try { box.innerHTML = ""; } catch {} }, 5000);
}

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
    toast("Không kết nối được API / MongoDB: " + e.message, "danger");
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
  const [sv, hocluc] = await Promise.all([
    api('/api/SinhVien/' + encodeURIComponent(refId)),
    api('/api/SinhVien/' + encodeURIComponent(refId) + '/hocluc').catch(() => null)
  ]);
  const hero = document.querySelector('.hero');
  if (hero) {
    hero.querySelector('h3').textContent = `Xin chào, ${sv.HoTen}`;
    hero.querySelector('p').textContent = 'Cổng học tập cá nhân - xem điểm, chuẩn đầu ra và đăng ký học phần.';
  }
  renderStatCards([
    ['MSSV', sv.Id, 'bi-person-vcard', 'primary'],
    ['Lớp sinh hoạt', sv.LopSinhHoat || 'N/A', 'bi-people', 'success'],
    ['Tín chỉ đã học', (sv.BangDiem || []).reduce((s, b) => s + (b.SoTinChi || 0), 0), 'bi-award', 'warning'],
    ['GPA hệ 10', hocluc ? Number(hocluc.GPA).toFixed(2) : '—', 'bi-bar-chart', 'info']
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

async function loadSinhVien() {
  const table = document.getElementById("svTable");
  if (!table) return;
  table.innerHTML = '<tr><td colspan="6" class="text-center py-4"><div class="spinner-border spinner-border-sm text-primary me-2"></div>Đang tải...</td></tr>';
  try {
  await fillKhoaSelects();
  const role = localStorage.getItem('role');
  const addBtn = document.querySelector('button[data-bs-target="#modalSinhVien"]');
  if (addBtn) addBtn.style.display = (role === 'admin') ? '' : 'none';

  const lop = document.getElementById("filterLop")?.value || "";
  const q = document.getElementById("filterQ")?.value || "";
  const qs = new URLSearchParams();
  if (lop) qs.set("lop", lop);
  if (q) qs.set("q", q);
  const list = await api("/api/SinhVien?" + qs.toString());
  table.innerHTML = list.map(sv => {
    const adminActions = role === 'admin' ? `
        <button class="btn btn-sm btn-outline-success" onclick="editSv('${sv.Id}')"><i class="bi bi-pencil"></i></button>
        <button class="btn btn-sm btn-outline-danger" onclick="xoaSv('${sv.Id}')"><i class="bi bi-trash"></i></button>` : '';

    return `
    <tr>
      <td><code>${sv.Id}</code></td>
      <td>${sv.HoTen}</td>
      <td>${sv.LopSinhHoat || ""}</td>
      <td>${sv.TenKhoa || sv.MaKhoa || ""}</td>
      <td><span class="badge bg-secondary">${sv.TrangThai || "Đang học"}</span></td>
      <td class="text-end">
        <button class="btn btn-sm btn-outline-primary" onclick="xemSv('${sv.Id}')"><i class="bi bi-eye"></i></button>
        ${adminActions}
      </td>
    </tr>`;
  }).join("") || '<tr><td colspan="6" class="text-center py-4 text-muted">Không có sinh viên phù hợp.</td></tr>';
  } catch (e) {
    table.innerHTML = `<tr><td colspan="6" class="text-center py-4 text-danger">Không tải được dữ liệu sinh viên: ${e.message || 'Lỗi máy chủ'}</td></tr>`;
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
  document.getElementById("svTrangThai").value = sv.TrangThai || "Đang học";
  clearModalError("modalSinhVien");
  document.getElementById("svModalTitle").innerText = "Chỉnh sửa hồ sơ Sinh viên";
  new bootstrap.Modal(document.getElementById("modalSinhVien")).show();
}

async function xemSv(id) {
  const sv = await api("/api/SinhVien/" + id);
  let hocluc = null;
  try { hocluc = await api("/api/SinhVien/" + id + "/hocluc"); } catch { /* empty bangdiem */ }
  const rows = (sv.BangDiem || []).map(b => `
    <tr>
      <td>${b.MaMon}</td>
      <td>${b.TenMon}</td>
      <td>${b.SoTinChi}</td>
      <td>${b.GiangVien?.HoTen || b.GiangVien?.TenGV || ""}</td>
      <td>${b.DiemThanhPhan?.ChuyenCan ?? ""}</td>
      <td>${b.DiemThanhPhan?.GiuaKy ?? ""}</td>
      <td>${b.DiemThanhPhan?.ThucHanh ?? ""}</td>
      <td>${b.DiemThanhPhan?.CuoiKy ?? ""}</td>
      <td><b>${b.DiemTongKet}</b> (${b.DiemChu})</td>
    </tr>`).join("");
  const detailEl = document.getElementById("svDetail");
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
            <div class="gpa-display">${Number(hocluc.GPA).toFixed(2)}</div>
            <div>${xepLoaiBadge(hocluc.XepLoai)}</div>
          </div>` : ""}
        </div>
      </div>
    </div>
    <div class="table-responsive mt-3">
      <table class="table table-sm table-bordered">
        <thead><tr><th>Mã</th><th>Môn</th><th>TC</th><th>GV</th><th>CC</th><th>GK</th><th>TH</th><th>CK</th><th>Tổng</th></tr></thead>
        <tbody>${rows || '<tr><td colspan="9" class="text-center text-muted">Chưa có điểm</td></tr>'}</tbody>
      </table>
    </div>`;
  showPage("chitiet");
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
  document.getElementById("svLop").value = "14DHTH01";
  document.getElementById("svKhoaHoc").value = "K14";
  fillKhoaSelects("K01");
  clearModalError("modalSinhVien");
  document.getElementById("svModalTitle").innerText = "Thêm sinh viên mới";
}

async function saveSinhVien(e) {
  e.preventDefault();
  clearModalError("modalSinhVien");

  const oldId   = document.getElementById("svIsEdit").value;  // rỗng = Thêm mới
  const newId   = document.getElementById("svId").value.trim();
  const idField = document.getElementById("svId");

  if (!newId) {
    showModalError("modalSinhVien", "MSSV không được để trống.");
    idField.classList.add("is-invalid"); idField.focus(); return;
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
    BangDiem: existingBangDiem
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
  if (!confirm("Xóa " + id + "?")) return;
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
    
    document.getElementById("lhpIsEdit").value = l.Id;
    document.getElementById("lhpId").value = l.Id;
    document.getElementById("lhpId").disabled = false;
    document.getElementById("lhpId").classList.remove("is-invalid");
    clearModalError("modalLhp");
    
    document.getElementById("lhpHk").value = l.Hocky;
    document.getElementById("lhpNam").value = l.NamHoc;
    document.getElementById("lhpPhong").value = l.PhongHoc || "";
    document.getElementById("lhpLich").value = l.LichHoc || "";
    document.getElementById("lhpSiso").value = l.SiSoToiDa;
    // Số tín chỉ
    const soTinChiEl = document.getElementById("lhpSoTinChi");
    if (soTinChiEl) {
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
    
    document.getElementById("lhpModalTitle").innerText = "Chỉnh sửa Lớp học phần";
    new bootstrap.Modal(document.getElementById("modalLhp")).show();
  } catch (err) {
    toast("Lỗi khi tải thông tin lớp học phần: " + err.message, "danger");
  }
}

function openCreateLhp() {
  document.getElementById("lhpIsEdit").value = "";
  document.getElementById("lhpId").disabled = false;
  ['lhpHk', 'lhpNam', 'lhpDaMo', 'lhpChoPhepGvNhapSuaDiem', 'lhpKhoaDropdown', 'lhpMonDropdown', 'lhpGvDropdown'].forEach(fieldId => {
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
    return;
  }

  if (!maGv || !tenGv) {
    showModalError("modalLhp", "Vui lòng chọn giảng viên.");
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
  if (!confirm("Xóa " + id + "?")) return;
  await api("/api/LopHocPhan/" + id, { method: "DELETE" });
  loadLhp();
}

// Chuyển đến trang chấm điểm cho lớp học phần cụ thể
function chamDiemLhp(maLhp) {
  window.location.href = `/diem.html?lhp=${encodeURIComponent(maLhp)}`;
}

// Cache danh sách SV đã tải để lọc client-side
let _diemSvCache = [];
let _currentFilterLhp = '';
let _scoreEditableLhpIds = new Set();

async function initQuanLyDiemPage() {
  const role = localStorage.getItem('role');
  const refId = localStorage.getItem('refId');

  if (role === 'sinhvien') {
    const managementView = document.getElementById('adminTeacherDiemView');
    const studentView = document.getElementById('studentTranscriptView');
    if (managementView) managementView.style.display = 'none';
    if (studentView) studentView.style.display = 'block';
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

async function loadStudentTranscript(svId) {
  const [sv, hocluc] = await Promise.all([
    api('/api/SinhVien/' + encodeURIComponent(svId)),
    api('/api/SinhVien/' + encodeURIComponent(svId) + '/hocluc').catch(() => null)
  ]);
  document.getElementById('svProfileHoTen').textContent = `${sv.HoTen} (${sv.Id})`;
  document.getElementById('svProfileDetail').textContent = `Lớp: ${sv.LopSinhHoat || 'N/A'} | Khoa: ${sv.TenKhoa || sv.MaKhoa || 'N/A'} | Email: ${sv.Email || 'N/A'}`;
  const bangDiem = sv.BangDiem || [];
  const tongTc = bangDiem.reduce((s, b) => s + (b.SoTinChi || 0), 0);
  document.getElementById('svSumTc').textContent = tongTc;
  document.getElementById('svGpa10').textContent = hocluc ? Number(hocluc.GPA).toFixed(2) : '0.00';
  document.getElementById('svXepLoaiBadge').innerHTML = hocluc ? xepLoaiBadge(hocluc.XepLoai) : '<span class="badge bg-secondary">Chưa đủ dữ liệu</span>';

  document.getElementById('svTranscriptTableBody').innerHTML = bangDiem.length ? bangDiem.map(b => `
    <tr>
      <td class="text-center"><code>${b.MaLHP}</code></td>
      <td><b>${b.TenMon}</b><br/><small class="text-muted">${b.MaMon}</small></td>
      <td class="text-center">${b.SoTinChi}</td>
      <td>${getTeacherName(b.GiangVien)}</td>
      <td class="text-center">${b.DiemThanhPhan?.ChuyenCan ?? '—'}</td>
      <td class="text-center">${b.DiemThanhPhan?.GiuaKy ?? '—'}</td>
      <td class="text-center">${b.DiemThanhPhan?.ThucHanh ?? '—'}</td>
      <td class="text-center">${b.DiemThanhPhan?.CuoiKy ?? '—'}</td>
      <td class="text-center fw-bold text-primary">${Number(b.DiemTongKet || 0).toFixed(1)}</td>
      <td class="text-center"><span class="badge bg-light text-dark border">${b.DiemChu || 'Chưa có'}</span></td>
      <td class="text-center">${Number(b.DiemTongKet || 0) >= 4 ? '<span class="badge bg-success">Đạt</span>' : '<span class="badge bg-danger">Chưa đạt</span>'}</td>
    </tr>`).join('') : '<tr><td colspan="11" class="text-center py-4 text-muted">Chưa có môn học nào.</td></tr>';

  const cloRows = [];
  bangDiem.forEach(b => {
    (b.DiemCLO || []).forEach(clo => cloRows.push(`
      <tr><td class="text-center"><code>${clo.MaCLO}</code></td><td>${b.TenMon}</td><td>Chuẩn đầu ra ${clo.MaCLO}</td><td class="text-center">—</td><td class="text-center">${clo.DiemDat ?? 0}</td><td class="text-center">${clo.KetQua === 'Đạt' || Number(clo.DiemDat) >= 5 ? '<span class="badge bg-success">Đạt</span>' : '<span class="badge bg-secondary">Chưa đánh giá</span>'}</td></tr>`));
  });
  document.getElementById('svCloTableBody').innerHTML = cloRows.length ? cloRows.join('') : '<tr><td colspan="6" class="text-center py-3 text-muted">Chưa có dữ liệu chuẩn đầu ra CLO.</td></tr>';
}

// Lọc bảng điểm sinh viên theo từ khóa
function filterSvTranscriptTable() {
  const searchText = document.getElementById('svTranscriptSearch')?.value.toLowerCase().trim() || '';
  const tbody = document.getElementById('svTranscriptTableBody');
  if (!tbody) return;

  const rows = tbody.querySelectorAll('tr');
  rows.forEach(row => {
    if (row.cells.length <= 1) return; // Skip empty/loading rows
    
    const maLhp = row.cells[0]?.textContent?.toLowerCase() || '';
    const tenMon = row.cells[1]?.textContent?.toLowerCase() || '';
    const maMon = row.cells[1]?.querySelector('small')?.textContent?.toLowerCase() || '';
    
    const isVisible = !searchText || 
      maLhp.includes(searchText) || 
      tenMon.includes(searchText) || 
      maMon.includes(searchText);
    
    row.style.display = isVisible ? '' : 'none';
  });
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

function renderDiemSvTable(list) {
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
      const totalTc = bangDiem.reduce((sum, b) => sum + (b.SoTinChi || 0), 0);
      const gpa = totalTc > 0
        ? bangDiem.reduce((sum, b) => sum + (b.DiemTongKet || 0) * (b.SoTinChi || 0), 0) / totalTc
        : bangDiem.reduce((sum, b) => sum + (b.DiemTongKet || 0), 0) / bangDiem.length;
      gpaHtml = `<span class="fw-bold text-primary">${Number(gpa || 0).toFixed(2)}</span>`;
      xepLoaiHtml = xepLoaiBadge(
        gpa >= 9 ? 'Xuất sắc' : gpa >= 8 ? 'Giỏi' : gpa >= 7 ? 'Khá' : gpa >= 5 ? 'Trung bình' : 'Yếu'
      );
    }

    const canEdit = role === 'admin' || (role === 'giaovien' && _scoreEditableLhpIds.has(_currentFilterLhp));
    let inlineActionHtml = '';
    if (canEdit && _currentFilterLhp) {
      const bdList = bangDiem.filter(b => b.MaLHP === _currentFilterLhp);
      inlineActionHtml = bdList.map(bd => `
        <button class="btn btn-sm btn-outline-success me-1" title="Sửa điểm: ${bd.TenMon}"
          onclick="openModalEditDiem('${sv.Id}', '${bd.MaLHP}', '${bd.TenMon}', ${bd.DiemThanhPhan?.ChuyenCan ?? 0}, ${bd.DiemThanhPhan?.GiuaKy ?? 0}, ${bd.DiemThanhPhan?.ThucHanh ?? 0}, ${bd.DiemThanhPhan?.CuoiKy ?? 0})">
          <i class="bi bi-pencil-square me-1"></i>Sửa
        </button>
        <button class="btn btn-sm btn-outline-danger me-1" title="Xóa môn: ${bd.TenMon}"
          onclick="xoaMonKhoiBangDiem('${sv.Id}', '${bd.MaLHP}', '${bd.TenMon}')">
          <i class="bi bi-trash me-1"></i>Xóa
        </button>`).join('');
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

  // Ẩn/hiện nút thêm môn trong modal theo role
  const modalPushBtn = document.querySelector('#modalChiTietDiem button[onclick="openModalPushFromChiTiet()"]');
  if (modalPushBtn) {
    modalPushBtn.style.display = (role === 'sinhvien' || (role === 'giaovien' && _scoreEditableLhpIds.size === 0)) ? 'none' : '';
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
        <span class="fs-5 fw-bold text-primary me-2">${Number(hocluc.GPA).toFixed(2)}</span>
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

        const thaoTacHtml = coQuyenSua ? `
            <button class="btn btn-sm btn-outline-success me-1" title="Sửa điểm ($set)"
              onclick="openModalEditDiem('${sv.Id}', '${b.MaLHP}', '${b.TenMon}', ${b.DiemThanhPhan?.ChuyenCan ?? 0}, ${b.DiemThanhPhan?.GiuaKy ?? 0}, ${b.DiemThanhPhan?.ThucHanh ?? 0}, ${b.DiemThanhPhan?.CuoiKy ?? 0})">
              <i class="bi bi-pencil-square"></i> Sửa
            </button>
            <button class="btn btn-sm btn-outline-danger" title="Xóa môn ($pull)"
              onclick="xoaMonKhoiBangDiem('${sv.Id}', '${b.MaLHP}', '${b.TenMon}')">
              <i class="bi bi-trash"></i> Xóa
            </button>` : `<span class="text-muted small">Chỉ xem</span>`;

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
          <td class="text-center fw-bold text-primary">${b.DiemTongKet}</td>
          <td class="text-center"><span class="badge bg-light text-dark border">${b.DiemChu}</span></td>
          <td class="text-end">${thaoTacHtml}</td>
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
  fillLhpSelect();
}

async function submitPushDiemModal(e) {
  e.preventDefault();
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
      ChuyenCan: Number(document.getElementById('modalPushCc').value),
      GiuaKy: Number(document.getElementById('modalPushGk').value),
      ThucHanh: Number(document.getElementById('modalPushTh').value),
      CuoiKy: Number(document.getElementById('modalPushCk').value)
    }
  };

  try {
    await api("/api/SinhVien/" + encodeURIComponent(svId) + "/diem", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    bootstrap.Modal.getInstance(document.getElementById('modalPushDiem')).hide();
    toast(`Đã thêm môn "${body.TenMon}" vào bảng điểm của ${svId} ($push thành công)`);
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
  document.getElementById('editDiemCc').value = cc;
  document.getElementById('editDiemGk').value = gk;
  document.getElementById('editDiemTh').value = th;
  document.getElementById('editDiemCk').value = ck;
  bootstrap.Modal.getOrCreateInstance(document.getElementById('modalEditDiem')).show();
}

async function submitEditDiemModal(e) {
  e.preventDefault();
  const svId = document.getElementById('editDiemSvId').value;
  const maLhp = document.getElementById('editDiemMaLhp').value;
  const body = {
    ChuyenCan: Number(document.getElementById('editDiemCc').value),
    GiuaKy: Number(document.getElementById('editDiemGk').value),
    ThucHanh: Number(document.getElementById('editDiemTh').value),
    CuoiKy: Number(document.getElementById('editDiemCk').value)
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
    toast(`Đã cập nhật điểm môn ${maLhp} ($set + arrayFilters thành công)`);
    loadDiemSvList();
  } catch (err) {
    toast("Lỗi khi sửa điểm: " + err.message, "danger");
  }
}

async function xoaMonKhoiBangDiem(svId, maLhp, tenMon) {
  if (!confirm(`Bạn có chắc chắn muốn xóa môn "${tenMon}" (${maLhp}) khỏi bảng điểm của sinh viên ${svId}?`)) return;
  try {
    await api(`/api/SinhVien/${encodeURIComponent(svId)}/diem/${encodeURIComponent(maLhp)}`, {
      method: "DELETE"
    });
    toast(`Đã xóa môn "${tenMon}" khỏi bảng điểm ($pull thành công)`);
    loadDiemSvList();
    openChiTietDiem(svId);
  } catch (err) {
    toast("Lỗi khi xóa môn: " + err.message, "danger");
  }
}

async function fillLhpSelect(maKhoa) {
  const [allLhp, allMonHoc] = await Promise.all([
    api("/api/LopHocPhan"),
    api("/api/MonHoc")
  ]);

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
  if (p2) p2.innerHTML = options;
}

async function loadBaoCao() {
  fillLopSelects();
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
        const filteredBD = data.diemEntries;
        const tongTinChi = filteredBD.reduce((s, b) => s + (b.SoTinChi || 0), 0);
        const tongDiemHeSo = filteredBD.reduce((s, b) => s + (b.DiemTongKet || 0) * (b.SoTinChi || 0), 0);
        const gpa = tongTinChi > 0 ? Math.round((tongDiemHeSo / tongTinChi) * 100) / 100 : 0;
        let xepLoai = 'Yếu';
        if (gpa >= 9) xepLoai = 'Xuất sắc';
        else if (gpa >= 8) xepLoai = 'Giỏi';
        else if (gpa >= 7) xepLoai = 'Khá';
        else if (gpa >= 5) xepLoai = 'Trung bình';
        if (!filteredBD.length) xepLoai = 'Chưa có điểm';
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
      const gpas = list.map(r => Number(r.GPA));
      const avg = (gpas.reduce((a,b)=>a+b,0)/gpas.length).toFixed(2);
      const max = Math.max(...gpas).toFixed(2);
      const min = Math.min(...gpas).toFixed(2);
      const pass = list.filter(r => Number(r.GPA) >= 5).length;
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
      const gpa = Number(r.GPA);
      const gpaClass = gpa >= 8 ? 'score-pass' : gpa >= 5 ? 'score-avg' : 'score-fail';
      const studentId = r.Id ?? r._id ?? '';
      return `<tr>
        <td class="text-muted small">${idx+1}</td>
        <td><code class="fw-semibold text-primary">${studentId}</code></td>
        <td class="fw-semibold">${r.HoTen}</td>
        <td><span class="badge bg-light text-dark border">${r.LopSinhHoat}</span></td>
        ${role !== 'giaovien' ? `<td class="text-center">${r.TongTinChi} TC</td>` : ''}
        <td class="text-center"><span class="${gpaClass} fs-6">${gpa.toFixed(2)}</span></td>
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

async function loadAccounts() {
  const role = document.getElementById('accFilterRole')?.value || '';
  const q = document.getElementById('accSearchText')?.value?.trim() || '';
  const qs = new URLSearchParams();
  if (role) qs.set('role', role);
  if (q) qs.set('q', q);
  try {
    const list = await api('/api/TaiKhoan?' + qs.toString());
    _accountCache = list;
    renderAccounts(list);
  } catch (err) {
    const body = document.getElementById('accTableBody');
    if (body) body.innerHTML = `<tr><td colspan="6" class="text-center text-danger py-3">${err.message}</td></tr>`;
  }
}

function accountRoleBadge(role) {
  return role === 'admin' ? '<span class="badge bg-danger">Admin</span>' : role === 'giaovien' ? '<span class="badge bg-warning text-dark">Giáo viên</span>' : '<span class="badge bg-info text-dark">Sinh viên</span>';
}

function renderAccounts(list) {
  const body = document.getElementById('accTableBody');
  const count = document.getElementById('accCountBadge');
  if (!body) return;
  if (count) count.textContent = `${list.length} tài khoản`;
  body.innerHTML = list.length ? list.map(a => `
    <tr>
      <td><code class="fw-semibold">${a.Id}</code></td>
      <td>${a.HoTen}</td>
      <td>${accountRoleBadge(a.Role)}</td>
      <td>${a.RefId ? `<code>${a.RefId}</code>` : '<span class="text-muted">—</span>'}</td>
      <td class="text-center">${a.IsActive ? '<span class="badge bg-success"><i class="bi bi-check-circle me-1"></i>Hoạt động</span>' : '<span class="badge bg-secondary"><i class="bi bi-lock me-1"></i>Đã khóa</span>'}</td>
      <td class="text-end">
        <button class="btn btn-sm btn-outline-primary me-1" onclick="openModalEditAccount('${a.Id}')"><i class="bi bi-pencil"></i></button>
        <button class="btn btn-sm ${a.IsActive ? 'btn-outline-warning' : 'btn-outline-success'} me-1" onclick="toggleAccountStatus('${a.Id}', ${!a.IsActive})" title="${a.IsActive ? 'Khóa' : 'Kích hoạt'}"><i class="bi ${a.IsActive ? 'bi-lock' : 'bi-unlock'}"></i></button>
        <button class="btn btn-sm btn-outline-danger" onclick="deleteAccount('${a.Id}')"><i class="bi bi-trash"></i></button>
      </td>
    </tr>`).join('') : '<tr><td colspan="6" class="text-center py-4 text-muted">Không có tài khoản.</td></tr>';
}

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
}

async function toggleAccountStatus(id, isActive) {
  if (!confirm(isActive ? `Kích hoạt tài khoản ${id}?` : `Vô hiệu hóa tài khoản ${id}?`)) return;
  try { await api('/api/TaiKhoan/' + encodeURIComponent(id) + '/status', {method:'PUT', headers:{'Content-Type':'application/json'}, body: JSON.stringify(isActive)}); toast(isActive ? 'Đã kích hoạt tài khoản.' : 'Đã khóa tài khoản.'); loadAccounts(); }
  catch (err) { toast(err.message, 'danger'); }
}

async function deleteAccount(id) {
  if (!confirm(`Xóa vĩnh viễn tài khoản ${id}? Thao tác này không thể hoàn tác.`)) return;
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
      const hasScore = ['ChuyenCan', 'GiuaKy', 'ThucHanh', 'CuoiKy'].some(key => b.DiemThanhPhan?.[key] != null) || Number(b.DiemTongKet || 0) > 0;
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

function renderOpenLhp(list) {
  const body = document.getElementById('openLhpTableBody');
  if (!body) return;
  body.innerHTML = list.length ? list.map(l => {
    const full = l.SiSoToiDa > 0 && (l.SiSoHienTai || 0) >= l.SiSoToiDa;
    const daMo = l.DaMo !== false;
    return `<tr><td><code>${l.Id}</code></td><td><b>${l.TenMon}</b><br/><small class="text-muted">${l.MaMon}</small></td><td>${l.SoTinChi || '—'}</td><td>${getTeacherName(l.GiangVien)}</td><td>${l.LichHoc || '—'}<br/><small class="text-muted">${l.PhongHoc || ''}</small></td><td class="text-center">${l.SiSoHienTai || 0}/${l.SiSoToiDa} ${full ? '<span class="badge bg-danger">Đầy</span>' : '<span class="badge bg-success">Còn chỗ</span>'}</td><td class="text-end">${daMo ? `<button class="btn btn-sm btn-primary" ${full ? 'disabled' : ''} onclick="dangKyHocPhan('${l.Id}', '${l.TenMon}')"><i class="bi bi-plus-circle me-1"></i>Đăng ký</button>` : '<span class="badge bg-secondary"><i class="bi bi-lock me-1"></i>Đã đóng</span>'}</td></tr>`;
  }).join('') : '<tr><td colspan="7" class="text-center py-4 text-muted">Không còn lớp học phần phù hợp.</td></tr>';
}

async function dangKyHocPhan(maLhp, tenMon) {
  if (!confirm(`Đăng ký môn ${tenMon} (${maLhp})?`)) return;
  try { await api('/api/SinhVien/dangky/' + encodeURIComponent(maLhp), {method:'POST'}); toast('Đăng ký học phần thành công.'); loadDangKyHocPhanPage(); }
  catch (err) { toast(err.message, 'danger'); }
}

async function huyDangKy(maLhp, tenMon) {
  if (!confirm(`Hủy đăng ký môn ${tenMon}?`)) return;
  try { await api('/api/SinhVien/huydangky/' + encodeURIComponent(maLhp), {method:'DELETE'}); toast('Đã hủy đăng ký.', 'warning'); loadDangKyHocPhanPage(); }
  catch (err) { toast(err.message, 'danger'); }
}

