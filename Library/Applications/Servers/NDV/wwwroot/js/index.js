// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// 表单确认回调函数
function formConfirmCallback() {
    console.log('用户确认了表单操作');
}

// 成功确认回调
function successConfirmCallback() {
    console.log('用户确认了操作');
}

// 当DOM加载完成后执行
document.addEventListener('DOMContentLoaded', function() {
    // 初始动画 - 不需要设置頁面顶部元素的动画，因为我们已经在CSS中设置了
    
    // 时间显示和品牌信任区域的动画
    animateElement(document.querySelector('.time-display'), 'animation-fadeInUp', 900);
    animateElement(document.querySelector('.brands-trust'), 'animation-fadeInUp', 1200);
    animateElement(document.querySelector('.features-title'), 'animation-fadeInUp', 1500);
    
    // 设置滚动监听
    setupScrollObserver();
    
    // 表单输入效果
    setupFormEffects();
    
    // 表单提交处理
    setupFormSubmission();
    
    // 社交登录按钮事件
    setupSocialButtons();
    
    // 滚动动画
    setupSmoothScroll();
});

// 表单提交处理
function setupFormSubmission() {
    const signupForm = document.querySelector('#signupForm');
    if (!signupForm) return;
    
    signupForm.addEventListener('submit', function(e) {
        // 阻止默认提交行为，改用AJAX提交
        e.preventDefault();
        
        // 创建动画效果
        const btn = document.querySelector('.btn-submit');
        const originalText = btn.innerHTML;
        btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> 处理中...';
        btn.disabled = true;
        
        // 输入框禁用状态
        const inputs = document.querySelectorAll('.form-control');
        inputs.forEach(input => {
            input.setAttribute('readonly', 'true');
        });
        
        // 获取表单数据
        const formData = new FormData(this);
        const email = formData.get('email');
        
        // 首先发送验证邮件
        fetch('/api/EmailVerification/send', {
            method: 'POST',
            body: formData
        })
        .then(response => {
            if (!response.ok) {
                return response.json().then(data => {
                    throw data;
                });
            }
            return response.json();
        })
        .then(data => {
            if (data.success) {
                // 邮件发送成功，跳转到等待验证页面
                window.location.href = `/EmailVerificationWaiting?email=${encodeURIComponent(email)}`;
            } else {
                throw data;
            }
        })
        .catch(error => {
            // 显示错误消息
            if (window.errorMessageBoxAPI) {
                // 更新错误详情
                if (error && error.message) {
                    const errorDetail = document.getElementById('errorDetail');
                    if (errorDetail) {
                        errorDetail.innerHTML = `<strong>错误信息：</strong> ${error.message}`;
                    }
                }
                
                window.errorMessageBoxAPI.show();
            }
            console.error('提交错误:', error);
            
            // 恢复按钮状态
            btn.innerHTML = originalText;
            btn.disabled = false;
            
            // 恢复输入框状态
            inputs.forEach(input => {
                input.removeAttribute('readonly');
            });
        });
    });
}

// 添加成功效果
function addSuccessEffect(buttonElement) {
    buttonElement.innerHTML = '<i class="fas fa-check"></i> 提交成功';
    buttonElement.style.backgroundColor = '#2ecc71';
}

// 社交登录按钮事件
function setupSocialButtons() {
    document.querySelectorAll('.social-btn').forEach(btn => {
        btn.addEventListener('click', function() {
            alert('社交登录功能即将上线，敬请期待！');
        });
    });
}

// 滚动动画
function setupSmoothScroll() {
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            
            const targetId = this.getAttribute('href');
            const targetElement = document.querySelector(targetId);
            
            if (targetElement) {
                window.scrollTo({
                    top: targetElement.offsetTop - 100,
                    behavior: 'smooth'
                });
            }
        });
    });
}

// 测试消息框显示
function showSuccessMessage() {
    if (window.successMessageBoxAPI) {
        window.successMessageBoxAPI.show();
    }
}

function showErrorMessage() {
    if (window.errorMessageBoxAPI) {
        window.errorMessageBoxAPI.show();
    }
}

// 动画元素函数
function animateElement(element, animationClass, delay = 0) {
    if (!element) return;
    
    // 确保元素在应用动画前是可见的
    element.style.opacity = '1';
    
    setTimeout(() => {
        element.classList.add(animationClass);
    }, delay);
}

// 设置表单效果
function setupFormEffects() {
    const formInputs = document.querySelectorAll('.form-control');
    
    formInputs.forEach(input => {
        // 获取焦点时添加活跃效果
        input.addEventListener('focus', function() {
            this.parentNode.classList.add('input-active');
        });
        
        // 失去焦点时移除活跃效果
        input.addEventListener('blur', function() {
            if (this.value === '') {
                this.parentNode.classList.remove('input-active');
            }
        });
        
        // 如果已有值，保持活跃效果
        if (input.value !== '') {
            input.parentNode.classList.add('input-active');
        }
    });
}

// 设置滚动监听
function setupScrollObserver() {
    // 定义需要观察的元素选择器和其对应的动画类
    const observeConfig = [
        { selector: '.feature-card', animationClass: 'animation-fadeInUp', staggered: true },
        { selector: '.benefit-item', animationClass: 'animation-fadeInLeft', staggered: true },
        { selector: '.signup-section', animationClass: 'animation-fadeInUp', staggered: false },
        { selector: '.footer', animationClass: 'animation-fadeIn', staggered: false }
    ];
    
    // 创建交叉观察器实例
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            // 检查元素是否可见
            if (entry.isIntersecting) {
                // 元素进入视口，添加动画类
                const target = entry.target;
                const config = observeConfig.find(c => target.matches(c.selector));
                
                if (config) {
                    // 确保元素可见
                    target.style.opacity = '1';
                    
                    if (config.staggered && target.parentElement) {
                        // 查找同级元素的索引，用于计算延迟
                        const siblings = Array.from(target.parentElement.children);
                        const index = siblings.indexOf(target);
                        animateElement(target, config.animationClass, index * 200);
                    } else {
                        animateElement(target, config.animationClass, 0);
                    }
                    
                    // 动画已经触发，不再需要观察
                    observer.unobserve(target);
                }
            }
        });
    }, {
        root: null, // 使用视口作为参考
        rootMargin: '0px', // 视口边缘
        threshold: 0.15 // 当目标元素15%可见时触发
    });
    
    // 开始观察所有配置的元素
    observeConfig.forEach(config => {
        document.querySelectorAll(config.selector).forEach(element => {
            observer.observe(element);
        });
    });
}
