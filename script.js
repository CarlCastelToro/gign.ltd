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
    
    // 创建回到顶部按钮逻辑不变
    if (!backToTopElement) {
        const backToTopDiv = document.createElement('div');
        backToTopDiv.id = 'backToTop';
        backToTopDiv.className = 'back-to-top';
        
        const link = document.createElement('a');
        link.href = '#_jumptitle';
        link.onclick = function() {
            window.location.hash = '#_jumptitle';
        };
        link.textContent = '↑';
        
        backToTopDiv.appendChild(link);
        document.body.appendChild(backToTopDiv);
    }

    // 回到顶部按钮显示逻辑不变
    const backToTopButton = document.querySelector('.back-to-top');
    window.addEventListener('scroll', function () {
        backToTopButton.style.display = window.scrollY > 300 ? 'block' : 'none';
    });

    // ===== 彻底重构的主题切换逻辑（无闪烁核心）=====
    const themeToggle = document.getElementById('themeToggle');
    if (themeToggle) {
        // 1. 初始化主题（页面加载时直接设置类名，无样式表操作）
        const initTheme = () => {
            const savedTheme = localStorage.getItem('siteTheme') || 'dark';
            // 直接通过类名控制主题，而非禁用样式表
            if (savedTheme === 'light') {
                document.documentElement.classList.add('light-theme');
                themeToggle.checked = true;
            } else {
                document.documentElement.classList.remove('light-theme');
                themeToggle.checked = false;
            }
        };

        // 2. 切换主题（仅修改类名，无任何样式表操作）
        const switchTheme = () => {
            const isLight = themeToggle.checked;
            const newTheme = isLight ? 'light' : 'dark';
            
            // 仅修改根元素类名（CSS 会立即响应，且有过渡效果）
            if (isLight) {
                document.documentElement.classList.add('light-theme');
            } else {
                document.documentElement.classList.remove('light-theme');
            }
            
            // 保存到本地存储
            localStorage.setItem('siteTheme', newTheme);
        };

        // 初始化主题
        initTheme();

        // 绑定切换事件（无延迟、无样式表操作，零闪烁）
        themeToggle.addEventListener('change', switchTheme, { passive: true });
    }

    // ===== 新增：字体切换逻辑（与主题切换逻辑风格一致）=====
    const fontToggle = document.getElementById('fontToggle');
    if (fontToggle) {
        // 1. 初始化字体
        const initFont = () => {
            const savedFont = localStorage.getItem('siteFont') || 'yahei';
            if (savedFont === 'georgia') {
                document.documentElement.classList.add('font-georgia');
                document.documentElement.classList.remove('font-yahei');
                fontToggle.checked = false;
            } else {
                document.documentElement.classList.add('font-yahei');
                document.documentElement.classList.remove('font-georgia');
                fontToggle.checked = true;
            }
        };

        // 2. 切换字体（仅修改类名，无样式表操作）
        const switchFont = () => {
            const isGeorgia = !fontToggle.checked;
            const newFont = isGeorgia ? 'georgia' : 'yahei';
            
            // 切换根元素字体类名
            if (isGeorgia) {
                document.documentElement.classList.add('font-georgia');
                document.documentElement.classList.remove('font-yahei');
            } else {
                document.documentElement.classList.add('font-yahei');
                document.documentElement.classList.remove('font-georgia');
            }
            
            // 保存到本地存储
            localStorage.setItem('siteFont', newFont);
        };

        // 初始化字体
        initFont();

        // 绑定字体切换事件
        fontToggle.addEventListener('change', switchFont, { passive: true });
    }
});