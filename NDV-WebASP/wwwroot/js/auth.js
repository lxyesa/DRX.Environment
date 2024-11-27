// API 基础URL - 确保与后端地址匹配
const API_BASE_URL = window.location.origin + '/api';

// 存储和获取 token 的函数
const setToken = (token) => localStorage.setItem('auth_token', token);
const getToken = () => localStorage.getItem('auth_token');
const removeToken = () => localStorage.removeItem('auth_token');

// 登录函数
async function handleLogin(savedUsername = null, savedPassword = null) {
    const username = savedUsername || document.getElementById('username').value;
    const password = savedPassword || document.getElementById('password').value;
    const machineCode = generateMachineCode(); // 生成机器码

    try {
        const response = await fetch(`${API_BASE_URL}/user/login`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            credentials: 'include', // 添加这行
            body: JSON.stringify({
                username,
                password,
                machineCode
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        
        if (data.success) {
            setToken(data.token);
            // 保存用户凭证
            localStorage.setItem('username', username);
            localStorage.setItem('password', password);
            updateUIAfterLogin(data.user);
            alert('登录成功！');
        } else {
            alert(data.message || '登录失败');
        }
    } catch (error) {
        console.error('登录出错:', error);
        alert('登录时发生错误: ' + error.message);
    }
}

// 注册函数
async function handleRegister() {
    const username = document.getElementById('username').value;
    const password = document.getElementById('password').value;
    const email = prompt('请输入邮箱地址：');

    if (!email) return;

    try {
        const response = await fetch(`${API_BASE_URL}/user/register`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            credentials: 'include', // 添加这行
            body: JSON.stringify({
                username,
                password,
                email
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        
        if (data.success) {
            alert('注册成功！请登录');
            // 可以直接调用登录函数
            await handleLogin(username, password);
        } else {
            alert(data.message || '注册失败');
        }
    } catch (error) {
        console.error('注册出错:', error);
        alert('注册时发生错误: ' + error.message);
    }
}

// 获取用户资料函数
async function getUserProfile() {
    const token = getToken();
    if (!token) return;

    try {
        const response = await fetch(`${API_BASE_URL}/user/profile`, {
            headers: {
                'Authorization': `Bearer ${token}`
            },
            credentials: 'include', // 添加这行
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        updateUIAfterLogin(data);
    } catch (error) {
        console.error('获取用户资料失败:', error);
    }
}

// 更新UI显示
function updateUIAfterLogin(user) {
    const avatar = document.getElementById('头像');
    const usernameElement = document.getElementById('用户名');
    const authForm = document.getElementById('auth-form');

    if (avatar) avatar.src = `https://api.dicebear.com/7.x/initials/svg?seed=${user.username}`;
    if (usernameElement) {
        usernameElement.textContent = user.username;
        usernameElement.href = '#';
    }
    if (authForm) authForm.style.display = 'none';
}

// 生成机器码
function generateMachineCode() {
    // 这里用简单的随机数模拟，实际应该使用更复杂的算法
    let machineCode = localStorage.getItem('machine_code');
    if (!machineCode) {
        machineCode = Math.random().toString(36).substring(2) + Date.now().toString(36);
        localStorage.setItem('machine_code', machineCode);
    }
    return machineCode;
}

// 添加登出函数
async function handleLogout() {
    const token = getToken();
    if (!token) return;

    try {
        const response = await fetch(`${API_BASE_URL}/user/logout`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            },
            credentials: 'include', // 添加这行
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        removeToken();
        location.reload();
    } catch (error) {
        console.error('登出失败:', error);
    }
}

// 修改登录状态检查
async function checkLoginStatus() {
    const token = getToken();
    if (!token) return;

    try {
        const response = await fetch(`${API_BASE_URL}/user/profile`, {
            headers: {
                'Authorization': `Bearer ${token}`
            },
            credentials: 'include', // 添加这行
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        updateUIAfterLogin(data);
    } catch (error) {
        console.error('获取用户资料失败:', error);
    }
}

// 修改页面加载时的检查
document.addEventListener('DOMContentLoaded', () => {
    checkLoginStatus();
});