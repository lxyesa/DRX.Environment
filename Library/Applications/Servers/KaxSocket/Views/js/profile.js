// ================ 交互脚本：包含后端交互（登录检查、读取/保存资料） ==================
    // 所有注释使用中文，遵循项目约定

    const saveBtn = document.getElementById('saveBtn');
    const cancelBtn = document.getElementById('cancelBtn');
    // 包含自定义的 <input-box> 组件，确保脚本能找到所有输入控件
    const formInputs = Array.from(document.querySelectorAll('#profileForm input, #profileForm textarea, #profileForm input-box'));

    // 记录初始数据（将在从后端加载后填充），便于取消恢复
    let originalProfile = { name: '', handle: '', email: '', role: '', bio: '', signature: '', avatarSrc: '' };

    // 从 URL 中提取目标用户 uid（若存在），用于访问他人资料
    let targetUid = null;
    const pathParts = window.location.pathname.split('/').filter(p => p);
    if (pathParts.length >= 2 && pathParts[0] === 'profile' && pathParts[1]) {
      targetUid = pathParts[1];
    }

    // 当前登录用户的 uid（将在加载资料后设置）
    let currentUserUid = null;

    // 判断是否为查看他人资料
    let isViewingOtherProfile = false;

    // 格式化 Unix 时间（秒）为本地可读字符串
    function formatUnix(ts) {
      if (!ts || ts <= 0) return '-';
      try { return new Date(ts * 1000).toLocaleString(); } catch (e) { return '-'; }
    }

    // 将后端 permissionGroup 映射为可读角色名
    function mapPermissionToRole(n) {
      switch (Number(n)) {
        case 0: return '控制台';
        case 1: return 'Root';
        case 2: return '管理员';
        default: return '普通用户';
      }
    }

    // 显示错误页面并隐藏主要内容
    function showErrorPage(message = '资料不存在或已被删除。请检查 UID 是否正确。') {
      const errorContainer = document.getElementById('errorContainer');
      const mainContent = document.getElementById('mainContent');
      const errorMsg = errorContainer.querySelector('.error-message');
      
      if (errorMsg) {
        errorMsg.textContent = message;
      }
      
      errorContainer.classList.add('show');
      mainContent.style.display = 'none';
    }

    // 从后端读取用户资料并填充到表单（若未登录则重定向到 /login）
    async function loadProfileFromServer() {
      const token = localStorage.getItem('kax_login_token');
      if (!token) { location.href = '/login'; return; }

      try {
        // 确定要加载的资料端点：若指定了 targetUid 则加载他人资料，否则加载自己的
        const endpoint = targetUid ? `/api/user/profile/${targetUid}` : '/api/user/profile';
        
        // 检查本地缓存的头像，快速显示
        const cachedAvatar = localStorage.getItem('userAvatarCache');
        const cacheTimestamp = localStorage.getItem('userAvatarCacheTime');
        const now = Date.now();
        const cacheExpiry = 24 * 60 * 60 * 1000; // 24小时缓存

        if (cachedAvatar && cacheTimestamp && (now - parseInt(cacheTimestamp)) < cacheExpiry) {
          if (cachedAvatar !== '/default-avatar.jpg') {
            avatarImg.src = cachedAvatar;
            avatarImg.style.display = 'block';
            avatarInitials.style.display = 'none';
          } else {
            avatarImg.style.display = 'none';
            avatarInitials.style.display = 'block';
          }
        }

        const resp = await fetch(endpoint, { headers: { 'Authorization': 'Bearer ' + token } });
        if (resp.status === 200) {
          const data = await resp.json();
          const user = data.user || '';
          const displayName = data.displayName || user;
          const email = data.email || '';
          const bio = data.bio || '';
          const registeredAt = data.registeredAt || 0;
          const lastLoginAt = data.lastLoginAt || 0;
          const roleText = mapPermissionToRole(data.permissionGroup);

          // 新增：后端返回的 id 与封禁信息
          const uid = (typeof data.id !== 'undefined') ? data.id : null;
          const isBanned = !!data.isBanned;
          const banReason = data.banReason || '';
          const banExpiresAt = data.banExpiresAt || 0;

          // 先处理后端返回的持久化头像（若存在）
          const serverAvatar = data.avatarUrl || '';
          if (serverAvatar) {
            avatarImg.src = serverAvatar;
            avatarImg.style.display = 'block';
            avatarInitials.style.display = 'none';
            // 缓存头像URL
            localStorage.setItem('userAvatarCache', serverAvatar);
            localStorage.setItem('userAvatarCacheTime', Date.now().toString());
          }
          else {
            avatarImg.style.display = 'none';
            avatarInitials.style.display = 'block';
            // 缓存默认头像标记
            localStorage.setItem('userAvatarCache', '/default-avatar.jpg');
            localStorage.setItem('userAvatarCacheTime', Date.now().toString());
          }

          // 填充界面和表单
          document.getElementById('displayName').textContent = displayName;
          document.getElementById('displayHandle').textContent = '@' + user + ' • ' + roleText;
          document.getElementById('inputName').value = displayName;
          document.getElementById('inputHandle').value = user;
          document.getElementById('inputEmail').value = email;
          document.getElementById('inputRole').value = roleText;
          document.getElementById('inputBio').value = bio;
          document.getElementById('inputSignature').value = data.signature || '';

          const leftEmail = document.getElementById('email'); if (leftEmail) { leftEmail.textContent = email; leftEmail.title = email; }
          document.getElementById('joined').textContent = formatUnix(registeredAt);
          document.getElementById('lastLogin').textContent = formatUnix(lastLoginAt);

          // 填充新增的 UID 与封禁状态
          const uidEl = document.getElementById('uid'); if (uidEl) { uidEl.textContent = uid ? String(uid) : '-'; }
          const banEl = document.getElementById('banStatus'); if (banEl) {
            if (isBanned) {
              banEl.textContent = `是（到期: ${formatUnix(banExpiresAt)}${banReason ? ' 原因: ' + banReason : ''}）`;
              banEl.style.color = 'var(--danger)';
            } else {
              banEl.textContent = '否';
              banEl.style.color = '';
            }
          }

          // 填充统计数字（后端返回或 0）
          try {
            document.getElementById('statResourceCount').textContent = (data.resourceCount || 0).toString();
            document.getElementById('statGold').textContent = (data.gold || 0).toLocaleString();
            // 额外更新信息行中的金币数
            const goldRow = document.getElementById('gold');
            if (goldRow) goldRow.textContent = (data.gold || 0).toLocaleString();
          } catch (e) { /* 忽略 DOM 更新错误 */ }

          originalProfile = { name: displayName, handle: user, email: email, role: roleText, bio: bio, signature: data.signature || '', avatarSrc: serverAvatar || (avatarImg.src || '') };

          // 设置当前登录用户的 uid
          if (uid) {
            currentUserUid = uid;
          }

          // 判断是否查看他人资料：若指定了 targetUid 且与当前用户 uid 不同，则为查看他人资料
          if (targetUid && currentUserUid && targetUid !== String(currentUserUid)) {
            isViewingOtherProfile = true;
          } else {
            isViewingOtherProfile = false;
          }

          // 控制编辑功能的显示/隐藏
          updateEditableState();
        } else if (resp.status === 401) {
          // token 无效或过期
          localStorage.removeItem('kax_login_token');
          location.href = '/login';
        } else if (resp.status === 403) {
          alert('账号被封禁，无法访问资料页。');
          location.href = '/login';
        } else if (resp.status === 404) {
          // 用户不存在
          showErrorPage('抱歉，你访问的用户资料不存在或已被删除。请检查 UID 是否正确。');
        } else {
          console.warn('读取用户资料失败：', resp.status);
          showErrorPage('加载资料失败，请稍后重试。');
        }
      } catch (err) {
        console.error('加载用户资料时发生错误：', err);
        showErrorPage('加载资料时发生错误，请稍后重试。');
      }
    }

    // 根据是否查看他人资料来控制编辑功能的显示/隐藏
    function updateEditableState() {
      const profileForm = document.getElementById('profileForm');
      const saveBtn = document.getElementById('saveBtn');
      const cancelBtn = document.getElementById('cancelBtn');
      const avatarElement = document.querySelector('.avatar');
      const avatarOverlay = document.querySelector('.avatar-overlay');
      const avatarPlus = document.querySelector('.avatar-plus');

      if (isViewingOtherProfile) {
        // 隐藏编辑表单
        if (profileForm) profileForm.style.display = 'none';
        
        // 隐藏保存/取消按钮
        if (saveBtn) saveBtn.style.display = 'none';
        if (cancelBtn) cancelBtn.style.display = 'none';

        // 禁用头像修改：移除悬停效果和点击功能
        if (avatarElement) {
          avatarElement.style.cursor = 'default';
          avatarElement.style.pointerEvents = 'none';
        }
        if (avatarOverlay) avatarOverlay.style.display = 'none';
        if (avatarPlus) avatarPlus.style.display = 'none';
      } else {
        // 显示编辑表单
        if (profileForm) profileForm.style.display = 'block';
        
        // 显示保存/取消按钮
        if (saveBtn) saveBtn.style.display = 'inline-block';
        if (cancelBtn) cancelBtn.style.display = 'inline-block';

        // 启用头像修改
        if (avatarElement) {
          avatarElement.style.cursor = 'pointer';
          avatarElement.style.pointerEvents = 'auto';
        }
        if (avatarOverlay) avatarOverlay.style.display = 'flex';
        if (avatarPlus) avatarPlus.style.display = 'block';
      }
    }

    // 表单提交：保存到后端 /api/user/profile（需要登录）
    document.getElementById('profileForm').addEventListener('submit', async (ev) => {
      ev.preventDefault();

      // 防止在查看他人资料时提交表单
      if (isViewingOtherProfile) {
        alert('无法编辑他人资料');
        return;
      }

      const token = localStorage.getItem('kax_login_token');
      if (!token) { location.href = '/login'; return; }

      const displayName = document.getElementById('inputName').value.trim();
      const email = document.getElementById('inputEmail').value.trim();
      const bio = document.getElementById('inputBio').value || '';
      const signature = document.getElementById('inputSignature').value || '';

      saveBtn.disabled = true;
      try {
        // 1) 若用户选择了新的头像文件，先上传头像（avatarFile 为隐藏的 file input）
        const avatarFileEl = document.getElementById('avatarFile');
        if (avatarFileEl && avatarFileEl.files && avatarFileEl.files.length > 0) {
          const file = avatarFileEl.files[0];
          const fd = new FormData();
          fd.append('avatar', file, file.name);
          const upResp = await fetch('/api/user/avatar', { method: 'POST', headers: { 'Authorization': 'Bearer ' + token }, body: fd });
          const upJson = await upResp.json().catch(() => ({}));
          if (upResp.status === 200 || upResp.status === 201) {
            if (upJson.url) {
              avatarImg.src = upJson.url;
              avatarImg.style.display = 'block';
              avatarInitials.style.display = 'none';
              originalProfile.avatarSrc = upJson.url;
              // 更新头像缓存
              localStorage.setItem('userAvatarCache', upJson.url);
              localStorage.setItem('userAvatarCacheTime', Date.now().toString());
            }
          } else if (upResp.status === 401) {
            localStorage.removeItem('kax_login_token'); location.href = '/login'; return;
          } else {
            alert(upJson.message || '头像上传失败');
            saveBtn.disabled = false; return;
          }
        }

        // 2) 提交资料更新请求（需要包含 targetUid 参数以验证权限）
        const resp = await fetch('/api/user/profile', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
          body: JSON.stringify({ displayName: displayName, email: email, bio: bio, signature: signature, targetUid: currentUserUid })
        });

        const result = await resp.json().catch(() => ({}));
        if (resp.status === 200) {
          // 成功：同步 UI 与缓存
          document.getElementById('displayName').textContent = displayName || originalProfile.name;
          originalProfile.name = displayName || originalProfile.name;
          originalProfile.email = email || originalProfile.email;
          originalProfile.bio = bio || originalProfile.bio;
          originalProfile.signature = signature || originalProfile.signature;

          const leftEmail = document.getElementById('email'); if (leftEmail) { leftEmail.textContent = originalProfile.email; leftEmail.title = originalProfile.email; }
          alert(result.message || '资料已保存');
        } else if (resp.status === 401) {
          localStorage.removeItem('kax_login_token');
          location.href = '/login';
        } else {
          alert(result.message || ('保存失败：' + resp.status));
        }
      } catch (err) {
        console.error(err);
        alert('无法连接到服务器');
      } finally {
        saveBtn.disabled = false;
      }
    });

    // 取消：恢复到初始值（示例行为）
    cancelBtn.addEventListener('click', () => {
      document.getElementById('inputName').value = originalProfile.name;
      document.getElementById('inputHandle').value = originalProfile.handle;
      document.getElementById('inputEmail').value = originalProfile.email;
      document.getElementById('inputRole').value = originalProfile.role;
      document.getElementById('inputBio').value = originalProfile.bio;
      document.getElementById('inputSignature').value = originalProfile.signature;
      // 恢复左侧邮箱显示与 title
      const leftEmail = document.getElementById('email');
      if (leftEmail) { leftEmail.textContent = originalProfile.email; leftEmail.title = originalProfile.email; }
      // 恢复头像预览（从缓存或原始值）
      if (originalProfile.avatarSrc && !originalProfile.avatarSrc.endsWith('/default-avatar.jpg')) {
        avatarImg.src = originalProfile.avatarSrc;
        avatarImg.style.display = 'block';
        avatarInitials.style.display = 'none';
      } else {
        avatarImg.style.display = 'none';
        avatarInitials.style.display = 'block';
      }
    });

    // 头像上传预览（文件输入位于 .avatar 内，点击头像触发）
    const avatarFile = document.getElementById('avatarFile');
    const avatarImg = document.getElementById('avatarImg');
    const avatarInitials = document.getElementById('avatarInitials');
    const avatarContainer = document.getElementById('avatarContainer');

    // 点击 / 回车 / 空格 触发文件选择（仅在查看自己的资料时）
    avatarContainer.addEventListener('click', () => {
      if (!isViewingOtherProfile) {
        avatarFile.click();
      }
    });
    avatarContainer.addEventListener('keydown', (e) => {
      if (!isViewingOtherProfile && (e.key === 'Enter' || e.key === ' ')) {
        e.preventDefault();
        avatarFile.click();
      }
    });

    avatarFile.addEventListener('change', (ev) => {
      const file = ev.target.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = function (e) {
        avatarImg.src = e.target.result;
        avatarImg.style.display = 'block';
        avatarInitials.style.display = 'none';
        // 新头像仅为本地预览（不实际上传）
      };
      reader.readAsDataURL(file);
    });



    // 其他按钮：示例行为
    document.getElementById('logoutBtn').addEventListener('click', () => {
      localStorage.removeItem('kax_login_token');
      location.href = '/login';
    });
    document.getElementById('backBtn').addEventListener('click', () => location.href = '/');

    document.getElementById('changePwBtn').addEventListener('click', async () => {
      const pwOldEl = document.getElementById('pwOld');
      const pw1El = document.getElementById('pw1');
      const pw2El = document.getElementById('pw2');
      if (!pwOldEl || !pw1El || !pw2El) {
        alert('修改密码表单未加载完毕，请刷新页面后重试。');
        return;
      }

      const oldPw = pwOldEl.value || '';
      const newPw = pw1El.value || '';
      const confirmPw = pw2El.value || '';

      if (!oldPw) { alert('请输入当前密码'); return; }
      if (newPw.length < 8) { alert('新密码长度至少 8 位'); return; }
      if (newPw !== confirmPw) { alert('两次新密码不匹配'); return; }

      const token = localStorage.getItem('kax_login_token');
      if (!token) { location.href = '/login'; return; }

      const btn = document.getElementById('changePwBtn');
      if (!btn) return;
      btn.disabled = true;
      try {
        const resp = await fetch('/api/user/password', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
          body: JSON.stringify({ oldPassword: oldPw, newPassword: newPw, confirmPassword: confirmPw })
        });

        const result = await resp.json().catch(() => ({}));
        if (resp.status === 200) {
          alert(result.message || '密码已更新');
          pwOldEl.value = '';
          pw1El.value = '';
          pw2El.value = '';
        } else if (resp.status === 401) {
          localStorage.removeItem('kax_login_token');
          location.href = '/login';
        } else {
          alert(result.message || ('修改失败：' + resp.status));
        }
      } catch (err) {
        console.error(err);
        alert('无法连接到服务器');
      } finally { btn.disabled = false; }
    });

    // 加载用户的激活资产列表
    async function loadActiveAssets() {
      const token = localStorage.getItem('kax_login_token');
      if (!token) return;

      const assetsLoading = document.getElementById('assetsLoading');
      const assetsEmpty = document.getElementById('assetsEmpty');
      const assetsList = document.getElementById('assetsList');
      const assetsCount = document.getElementById('assetsCount');

      try {
        const resp = await fetch('/api/user/assets/active', {
          headers: { 'Authorization': 'Bearer ' + token }
        });

        if (resp.status === 200) {
          const result = await resp.json().catch(() => ({}));
          const assets = result.data || [];

          assetsLoading.style.display = 'none';

          if (assets.length === 0) {
            assetsEmpty.style.display = 'block';
            assetsCount.textContent = '0 个';
          } else {
            assetsEmpty.style.display = 'none';
            assetsCount.textContent = `${assets.length} 个`;

            // 使用异步方式为每个 asset 请求名称（并缓存），避免阻塞主渲染
            const assetNameCache = {};
            async function fetchAssetName(id) {
              if (assetNameCache[id]) return assetNameCache[id];
              try {
                const r = await fetch(`/api/asset/name/${id}`);
                if (r.status === 200) {
                  const j = await r.json().catch(() => ({}));
                  assetNameCache[id] = j.name || `资源 #${id}`;
                  return assetNameCache[id];
                }
              } catch (e) { /* ignore */ }
              assetNameCache[id] = `资源 #${id}`;
              return assetNameCache[id];
            }

            assetsList.innerHTML = '';
            for (const asset of assets) {
              const activatedTime = new Date(asset.activatedAt).toLocaleString();
              let expiresText = '';
              let remainingText = '';

              if (asset.expiresAt === 0) {
                expiresText = '永久有效';
                remainingText = '无限期';
              } else {
                const expiresTime = new Date(asset.expiresAt);
                expiresText = expiresTime.toLocaleString();

                if (asset.remainingSeconds < 0) {
                  remainingText = '已过期';
                } else if (asset.remainingSeconds === 0) {
                  remainingText = '即将过期';
                } else {
                  const days = Math.floor(asset.remainingSeconds / 86400);
                  const hours = Math.floor((asset.remainingSeconds % 86400) / 3600);
                  if (days > 0) {
                    remainingText = `${days} 天 ${hours} 小时`;
                  } else if (hours > 0) {
                    remainingText = `${hours} 小时`;
                  } else {
                    remainingText = `${asset.remainingSeconds} 秒`;
                  }
                }
              }

              const name = await fetchAssetName(asset.assetId);

              assetsList.insertAdjacentHTML('beforeend', `
                <div style="padding:12px;border-radius:8px;background:rgba(255,255,255,0.01);border:1px solid rgba(255,255,255,0.05);">
                  <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:8px;">
                    <div style="font-weight:600;color:var(--muted-strong);">${name}</div>
                    <div style="font-size:0.85rem;padding:4px 8px;border-radius:6px;background:${asset.remainingSeconds < 0 ? 'rgba(239,68,68,0.1)' : 'rgba(34,197,94,0.1)'};">
                      <span style="color:${asset.remainingSeconds < 0 ? 'var(--danger)' : 'var(--success)'};">${remainingText}</span>
                    </div>
                  </div>
                  <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;font-size:0.9rem;">
                    <div>
                      <div style="color:var(--muted);margin-bottom:2px;">激活时间</div>
                      <div style="color:var(--muted-strong);">${activatedTime}</div>
                    </div>
                    <div>
                      <div style="color:var(--muted);margin-bottom:2px;">过期时间</div>
                      <div style="color:var(--muted-strong);">${expiresText}</div>
                    </div>
                  </div>
                  <div class="asset-action-buttons" data-asset-id="${asset.assetId}" data-asset-name="${name}">
                    <button class="asset-action-btn" data-action="changePlan">更变计划</button>
                    <button class="asset-action-btn danger" data-action="unsubscribe">取消订阅</button>
                  </div>
                </div>
              `);
            }
          }
        } else if (resp.status === 401) {
          localStorage.removeItem('kax_login_token');
          location.href = '/login';
        } else {
          assetsLoading.style.display = 'none';
          assetsEmpty.style.display = 'block';
          assetsEmpty.textContent = '无法加载资产列表';
        }
      } catch (err) {
        console.error('加载激活资产时发生错误：', err);
        assetsLoading.style.display = 'none';
        assetsEmpty.style.display = 'block';
        assetsEmpty.textContent = '加载失败，请重试';
      }
    }

    // CDK 激活处理
    const cdkInput = document.getElementById('cdkInput');
    const activateCdkBtn = document.getElementById('activateCdkBtn');
    const cdkMessage = document.getElementById('cdkMessage');
    const cdkResult = document.getElementById('cdkResult');
    const cdkResultDetails = document.getElementById('cdkResultDetails');

    activateCdkBtn.addEventListener('click', async () => {
      const cdkCode = cdkInput.value || cdkInput.textContent.trim();
      if (!cdkCode) {
        // 显示错误：CDK为空
        cdkMessage.style.display = 'block';
        cdkMessage.style.background = 'rgba(239,68,68,0.1)';
        cdkMessage.style.borderColor = 'rgba(239,68,68,0.3)';
        cdkMessage.style.color = 'var(--danger)';
        cdkMessage.textContent = '错误：CDK为空，请输入有效的 CDK 代码';
        activateCdkBtn.textContent = '激活失败';
        setTimeout(() => {
          activateCdkBtn.textContent = '激活';
        }, 2000);
        return;
      }

      const token = localStorage.getItem('kax_login_token');
      if (!token) {
        location.href = '/login';
        return;
      }

      activateCdkBtn.disabled = true;
      activateCdkBtn.textContent = '激活中...';
      cdkMessage.style.display = 'none';
      cdkResult.style.display = 'none';

      try {
        const resp = await fetch('/api/cdk/activate', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': 'Bearer ' + token
          },
          body: JSON.stringify({ code: cdkCode })
        });

        const result = await resp.json().catch(() => ({}));

        if (resp.status === 200) {
          // 激活成功
          cdkResult.style.display = 'block';
          const details = [];
          if (result.assetId > 0) details.push(`获得资源 #${result.assetId}`);
          if (result.goldValue > 0) details.push(`+${result.goldValue} 金币`);
          if (result.description) details.push(result.description);
          cdkResultDetails.textContent = details.length > 0 ? details.join(' • ') : '资源已添加至您的库中';
          
          cdkInput.value = '';
          activateCdkBtn.textContent = '激活成功';
          
          // 2秒后恢复按钮
          setTimeout(() => {
            activateCdkBtn.textContent = '激活';
          }, 2000);

          // 刷新用户数据以显示更新的CDK数量
          try {
            await loadProfileFromServer();
            await loadActiveAssets();
          } catch (e) { /* 忽略刷新错误 */ }
        } else if (resp.status === 401) {
          // 未授权
          localStorage.removeItem('kax_login_token');
          location.href = '/login';
        } else {
          // 错误处理
          cdkMessage.style.display = 'block';
          cdkMessage.style.background = 'rgba(239,68,68,0.1)';
          cdkMessage.style.borderColor = 'rgba(239,68,68,0.3)';
          cdkMessage.style.color = 'var(--danger)';

          // 根据错误码显示相应的错误信息
          const code = result.code;
          if (code === 1) {
            cdkMessage.textContent = '错误：CDK为空';
            activateCdkBtn.textContent = '激活失败';
          } else if (code === 2) {
            cdkMessage.textContent = '错误：CDK错误或不存在';
            activateCdkBtn.textContent = '激活失败';
          } else if (code === 3) {
            cdkMessage.textContent = '错误：CDK已被使用';
            activateCdkBtn.textContent = '激活失败';
          } else {
            cdkMessage.textContent = result.message || ('激活失败：' + resp.status);
            activateCdkBtn.textContent = '激活失败';
          }

          setTimeout(() => {
            activateCdkBtn.textContent = '激活';
          }, 2000);
        }
      } catch (err) {
        console.error('CDK激活请求失败：', err);
        cdkMessage.style.display = 'block';
        cdkMessage.style.background = 'rgba(239,68,68,0.1)';
        cdkMessage.style.borderColor = 'rgba(239,68,68,0.3)';
        cdkMessage.style.color = 'var(--danger)';
        cdkMessage.textContent = '错误：无法连接到服务器';
        activateCdkBtn.textContent = '激活失败';
        setTimeout(() => {
          activateCdkBtn.textContent = '激活';
        }, 2000);
      } finally {
        activateCdkBtn.disabled = false;
      }
    });

    // CDK输入框回车激活
    cdkInput.addEventListener('keypress', (e) => {
      if (e.key === 'Enter') {
        activateCdkBtn.click();
      }
    });

    // 初始化页面：先获取当前用户信息，再根据 targetUid 决定加载自己或他人的资料
    async function initializePage() {
      const token = localStorage.getItem('kax_login_token');
      if (!token) { location.href = '/login'; return; }

      try {
        // 先获取当前登录用户的 uid
        const currentResp = await fetch('/api/user/profile', { headers: { 'Authorization': 'Bearer ' + token } });
        if (currentResp.status === 200) {
          const currentData = await currentResp.json();
          currentUserUid = (typeof currentData.id !== 'undefined') ? currentData.id : null;

          // 若指定了 targetUid 且与当前用户不同，则标记为查看他人资料
          if (targetUid && currentUserUid && targetUid !== String(currentUserUid)) {
            isViewingOtherProfile = true;
          }
        }
      } catch (err) {
        console.error('获取当前用户信息失败：', err);
      }

      // 加载资料（自己或他人）
      await loadProfileFromServer();
      await loadActiveAssets();

      // 初始化：隐藏头像 img（如无真实图片）
      if (!avatarImg.src || avatarImg.src.endsWith('/default-avatar.jpg')) {
        avatarImg.style.display = 'none';
        avatarInitials.style.display = 'block';
      }

      // 初始化：为邮箱设置 title（方便 hover 查看完整文本）
      try {
        const emailEl = document.getElementById('email');
        if (emailEl && (!emailEl.title || emailEl.title.trim() === '')) {
          emailEl.title = emailEl.textContent.trim();
        }
      } catch (error) {
        console.warn('Failed to set email title:', error);
      }
    }

    // 页面加载完成后初始化
    initializePage();

// =============== 弹出卡片管理函数 ===============
    // 当前选中的资产ID和名称（用于更变计划/取消订阅）
    let currentAssetId = null;
    let currentAssetName = null;
    let selectedPlanId = null;
    // 全局缓存当前可用套餐列表，供确认弹框使用
    let availablePlans = [];

    // 打开更变计划弹窗
    function openChangePlanModal(assetId, assetName) {
      currentAssetId = assetId;
      currentAssetName = assetName;
      selectedPlanId = null;

      document.getElementById('planModalAssetName').textContent = assetName;
      
      // 从后端加载套餐列表
      loadAvailablePlans();

      document.getElementById('changePlanModal').classList.add('show');
    }

    // 关闭更变计划弹窗
    function closePlanModal() {
      document.getElementById('changePlanModal').classList.remove('show');
      currentAssetId = null;
      currentAssetName = null;
      selectedPlanId = null;
      // 隐藏任何可能显示的提示框
      document.getElementById('planModalConfirm').style.display = 'none';
      document.getElementById('planModalMessage').style.display = 'none';
    }

    // 打开取消订阅弹窗
    function openUnsubscribeModal(assetId, assetName) {
      currentAssetId = assetId;
      currentAssetName = assetName;

      document.getElementById('unsubscribeModalAssetName').textContent = assetName;
      document.getElementById('unsubscribeModal').classList.add('show');
    }

    // 关闭取消订阅弹窗
    function closeUnsubscribeModal() {
      document.getElementById('unsubscribeModal').classList.remove('show');
      currentAssetId = null;
      currentAssetName = null;
    }

    // 加载可用套餐列表（示例实现，需根据后端API调整）
    async function loadAvailablePlans() {
      const planList = document.getElementById('planList');
      planList.innerHTML = '<div style="color: var(--muted); text-align: center; padding: 20px;">加载套餐中...</div>';

      try {
        const token = localStorage.getItem('kax_login_token');
        if (!token) { location.href = '/login'; return; }

        // 调用后端API获取套餐列表
        const resp = await fetch(`/api/asset/${currentAssetId}/plans`, {
          headers: { 'Authorization': 'Bearer ' + token }
        });

        if (resp.status === 200) {
          const result = await resp.json().catch(() => ({}));
          const plans = result.plans || [];
          // cache globally for confirmation stage
          availablePlans = plans;

          if (plans.length === 0) {
            planList.innerHTML = '<div style="color: var(--muted); text-align: center; padding: 20px;">暂无可用套餐</div>';
          } else {
            planList.innerHTML = plans.map(plan => `
              <div class="plan-item" data-plan-id="${plan.id}" onclick="selectPlan(${plan.id}, this)">
                <div class="plan-name">
                  <div style="font-weight: 600; color: var(--muted-strong);">${plan.name}</div>
                  <div style="font-size: 0.85rem; color: var(--muted); margin-top: 2px;">${plan.duration}</div>
                </div>
                <div class="plan-price">💰 ${(plan.price || 0).toFixed(2)}</div>
              </div>
            `).join('');
          }
        } else if (resp.status === 401) {
          localStorage.removeItem('kax_login_token');
          location.href = '/login';
        } else {
          planList.innerHTML = '<div style="color: var(--danger); text-align: center; padding: 20px;">加载套餐失败</div>';
        }
      } catch (err) {
        console.error('加载套餐失败：', err);
        planList.innerHTML = '<div style="color: var(--danger); text-align: center; padding: 20px;">网络错误</div>';
      }
    }

    // 选择套餐
    function selectPlan(planId, element) {
      // 移除之前选中的样式
      document.querySelectorAll('.plan-item').forEach(el => {
        el.classList.remove('selected');
      });
      // 添加当前选中样式
      element.classList.add('selected');
      selectedPlanId = planId;
    }

    // 首次点击：显示确认提示框
    document.getElementById('confirmChangePlanBtn').addEventListener('click', () => {
      if (!selectedPlanId) {
        alert('请先选择要更变的套餐');
        return;
      }
      // 计算费用
      const plan = availablePlans.find(p => p.id === selectedPlanId);
      const cost = plan ? (plan.price || 0) : 0;
      const costEl = document.getElementById('planModalConfirmCost');
      costEl.textContent = `💰 ${cost.toFixed(2)}`;
      document.getElementById('planModalConfirm').style.display = 'block';
    });

    // 取消确认
    document.getElementById('planModalConfirmNo').addEventListener('click', () => {
      document.getElementById('planModalConfirm').style.display = 'none';
    });

    // 真正提交更变请求
    document.getElementById('planModalConfirmYes').addEventListener('click', async () => {
      document.getElementById('planModalConfirm').style.display = 'none';
      const token = localStorage.getItem('kax_login_token');
      if (!token) { location.href = '/login'; return; }

      const btn = document.getElementById('confirmChangePlanBtn');
      btn.disabled = true;
      btn.textContent = '处理中...';

      try {
        const resp = await fetch(`/api/asset/${currentAssetId}/changePlan`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': 'Bearer ' + token
          },
          body: JSON.stringify({ planId: selectedPlanId })
        });

        const result = await resp.json().catch(() => ({}));
        const msgEl = document.getElementById('planModalMessage');
        if (resp.status === 200) {
          msgEl.style.display = 'block';
          msgEl.style.background = 'rgba(34,197,94,0.1)';
          msgEl.style.color = 'var(--success)';
          msgEl.textContent = `成功更变套餐！需支付 💰 ${(result.cost || 0).toFixed(2)}`;
          setTimeout(() => {
            closePlanModal();
            loadActiveAssets();
          }, 1500);
        } else if (resp.status === 401) {
          localStorage.removeItem('kax_login_token');
          location.href = '/login';
        } else {
          msgEl.style.display = 'block';
          msgEl.style.background = 'rgba(239,68,68,0.1)';
          msgEl.style.color = 'var(--danger)';
          msgEl.textContent = result.message || ('更变失败：' + resp.status);
        }
      } catch (err) {
        console.error('更变套餐请求失败：', err);
        alert('无法连接到服务器');
      } finally {
        btn.disabled = false;
        btn.textContent = '确认更变';
      }
    });

    // 确认取消订阅
    document.getElementById('confirmUnsubscribeBtn').addEventListener('click', async () => {
      const token = localStorage.getItem('kax_login_token');
      if (!token) { location.href = '/login'; return; }

      const btn = document.getElementById('confirmUnsubscribeBtn');
      btn.disabled = true;
      btn.textContent = '取消中...';

      try {
        const resp = await fetch(`/api/asset/${currentAssetId}/unsubscribe`, {
          method: 'POST',
          headers: {
            'Authorization': 'Bearer ' + token
          }
        });

        const result = await resp.json().catch(() => ({}));

        if (resp.status === 200) {
          alert(result.message || '订阅已取消');
          closeUnsubscribeModal();
          // 刷新激活资产列表
          await loadActiveAssets();
        } else if (resp.status === 401) {
          localStorage.removeItem('kax_login_token');
          location.href = '/login';
        } else {
          alert(result.message || ('取消失败：' + resp.status));
        }
      } catch (err) {
        console.error('取消订阅请求失败：', err);
        alert('无法连接到服务器');
      } finally {
        btn.disabled = false;
        btn.textContent = '确认取消订阅';
      }
    });

    // 为资产按钮绑定点击事件（事件委托）
    document.addEventListener('click', (e) => {
      const btn = e.target.closest('.asset-action-btn');
      if (!btn) return;

      const action = btn.dataset.action;
      const container = btn.closest('.asset-action-buttons');
      if (!container) return;

      const assetId = container.dataset.assetId;
      const assetName = container.dataset.assetName;

      if (action === 'changePlan') {
        openChangePlanModal(assetId, assetName);
      } else if (action === 'unsubscribe') {
        openUnsubscribeModal(assetId, assetName);
      }
    });

    // 关闭模态框：点击背景
    document.querySelectorAll('.modal-overlay').forEach(overlay => {
      overlay.addEventListener('click', (e) => {
        if (e.target === overlay) {
          overlay.classList.remove('show');
          // 如果是更变计划弹窗，重置相关提示内容
          if (overlay.id === 'changePlanModal') {
            closePlanModal();
          }
        }
      });
    });
