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

    // ===== 优化后的主题切换逻辑 =====
    const themeToggle = document.getElementById('themeToggle');
    if (themeToggle) {
        themeToggle.addEventListener('change', function() {
            const newTheme = this.checked ? 'light' : 'dark';
            
            // 1. 更新本地存储
            localStorage.setItem('siteTheme', newTheme);
            
            // 2. 更新html根元素属性
            document.documentElement.setAttribute('theme', newTheme);
            
            // 3. 通过禁用/启用样式表切换主题（优化关键：使用requestAnimationFrame确保渲染顺序）
            const darkStyle = document.getElementById('dark-theme');
            const lightStyle = document.getElementById('light-theme');
            
            if (darkStyle && lightStyle) {
                // 使用requestAnimationFrame确保在浏览器下一次渲染前执行
                requestAnimationFrame(() => {
                    // 先启用目标主题的样式表（不立即禁用当前主题）
                    if (newTheme === 'light') {
                        lightStyle.disabled = false;
                        // 再在下一个渲染帧禁用原主题，确保新主题完全加载
                        requestAnimationFrame(() => {
                            darkStyle.disabled = true;
                        });
                    } else {
                        darkStyle.disabled = false;
                        // 再在下一个渲染帧禁用原主题，确保新主题完全加载
                        requestAnimationFrame(() => {
                            lightStyle.disabled = true;
                        });
                    }
                });
            }
        });
    }
});