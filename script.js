// 主题切换功能封装为单独函数，便于在样式加载完成后执行
function setupThemeToggle() {
    const themeToggle = document.getElementById('themeToggle');
    if (!themeToggle) return;
    
    themeToggle.addEventListener('change', function() {
        // 检查样式表是否已加载完成
        if (!window.themeStylesLoaded) {
            console.log('等待样式表加载完成...');
            // 如果样式表未加载完成，延迟执行切换操作
            setTimeout(() => {
                this.click(); // 重新触发切换
            }, 100);
            return;
        }
        
        const newTheme = this.checked ? 'light' : 'dark';
        
        // 1. 更新本地存储
        localStorage.setItem('siteTheme', newTheme);
        
        // 2. 更新html根元素属性
        document.documentElement.setAttribute('theme', newTheme);
        
        // 3. 通过禁用/启用样式表切换主题
        const darkStyle = document.getElementById('dark-theme');
        const lightStyle = document.getElementById('light-theme');
        
        if (darkStyle && lightStyle) {
            // 第一层requestAnimationFrame：确保在浏览器渲染周期内执行
            requestAnimationFrame(() => {
                // 先启用目标主题的样式表
                if (newTheme === 'light') {
                    lightStyle.disabled = false;
                    // 第二层requestAnimationFrame：确保新主题完全渲染后再禁用旧主题
                    requestAnimationFrame(() => {
                        darkStyle.disabled = true;
                    });
                } else {
                    darkStyle.disabled = false;
                    // 第二层requestAnimationFrame：确保新主题完全渲染后再禁用旧主题
                    requestAnimationFrame(() => {
                        lightStyle.disabled = true;
                    });
                }
            });
        }
    });
}

document.addEventListener('DOMContentLoaded', function () {
    // 原有锚点跳转逻辑不变
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            const targetId = this.getAttribute('href');
            const targetElement = document.querySelector(targetId);
            if (targetElement) {
                window.scrollTo({
                    top: targetElement.offsetTop - 20,
                    behavior: 'smooth'
                });
                history.pushState(null, null, targetId);
            }
        });
    });

    const backToTopElement = document.getElementById('backToTop');
    
    // 2. 如果不存在该元素，则创建并插入到body中
    if (!backToTopElement) {
        // 创建div元素
        const backToTopDiv = document.createElement('div');
        // 设置id和class
        backToTopDiv.id = 'backToTop';
        backToTopDiv.className = 'back-to-top';
        
        // 创建a标签元素
        const link = document.createElement('a');
        link.href = '#_jumptitle';
        // 设置点击事件
        link.onclick = function() {
            window.location.hash = '#_jumptitle';
        };
        // 设置字体样式和内容
        link.style.fontFamily = 'Wingdings';
        link.textContent = 'G';
        
        // 将a标签添加到div中
        backToTopDiv.appendChild(link);
        // 将div添加到body末尾
        document.body.appendChild(backToTopDiv);
    }

    // 原有回到顶部逻辑不变
    const backToTopButton = document.querySelector('.back-to-top');
    window.addEventListener('scroll', function () {
        if (window.scrollY > 300) {
            backToTopButton.style.display = 'block';
        } else {
            backToTopButton.style.display = 'none';
        }
    });

    // ===== 优化的主题切换设置 =====
    // 方式1：如果样式表已经加载完成，直接设置切换功能
    if (window.themeStylesLoaded) {
        setupThemeToggle();
    } else {
        // 方式2：监听样式表加载完成事件，然后设置切换功能
        document.addEventListener('themeStylesReady', setupThemeToggle);
        
        // 方式3：添加超时保护，防止样式表加载事件未触发
        setTimeout(() => {
            if (!window.themeStylesLoaded) {
                console.warn('样式表加载超时，强制初始化主题切换功能');
                setupThemeToggle();
            }
        }, 2000);
    }
});