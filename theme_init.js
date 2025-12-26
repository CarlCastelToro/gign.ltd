// 立即执行函数，在脚本加载时立刻执行（无需等待DOMContentLoaded）
(function() {
    // 1. 获取本地存储的主题偏好，默认深色
    var firstvisit = false;
    if(localStorage.getItem('siteTheme')===null){
        firstvisit = true;
    }
    const savedTheme = localStorage.getItem('siteTheme') || 'dark';
    // ===== 新增：初始化字体偏好 =====
    const savedFont = localStorage.getItem('siteFont') || 'yahei'; // 默认雅黑
    
    // 2. 核心修改：不再设置theme属性，改为直接添加/移除类名（匹配新方案）
    if (savedTheme === 'light') {
        document.documentElement.classList.add('light-theme');
    } else {
        document.documentElement.classList.remove('light-theme');
    }

    // ===== 新增：初始化字体类名 =====
    if (savedFont === 'georgia') {
        document.documentElement.classList.add('font-georgia');
        document.documentElement.classList.remove('font-yahei');
    } else {
        document.documentElement.classList.add('font-yahei');
        document.documentElement.classList.remove('font-georgia');
    }

    // 3. 移除旧逻辑：不再删除所有样式表（仅清理旧的主题样式表，保留其他样式）
    const oldThemeStyles = document.querySelectorAll('link[id="dark-theme"], link[id="light-theme"]');
    oldThemeStyles.forEach(link => link.remove());
    
    // 4. 核心修改：只加载一个包含CSS变量的统一样式表（替代原来的两个样式表）
    const mainStyle = document.createElement('link');
    mainStyle.rel = 'stylesheet';
    mainStyle.href = '/style.css'; // 这个文件包含所有CSS变量和主题样式
    mainStyle.id = 'main-theme';
    
    // 5. 优先加载样式表，确保页面渲染前样式已就绪（消除初始闪烁）
    mainStyle.onload = function() {
        // 样式表加载完成后标记，避免重复加载
        document.documentElement.classList.add('theme-loaded');
    };
    
    // 6. 将统一样式表添加到文档头部（放在最前面，确保优先级）
    document.head.insertBefore(mainStyle, document.head.firstChild);

    // 7. 预初始化开关状态（仅DOM加载后执行，不影响样式加载）
    function initToggleState() {
        const themeToggle = document.getElementById('themeToggle');
        const fontToggle = document.getElementById('fontToggle'); // 新增字体开关
        
        if (themeToggle) {
            themeToggle.checked = savedTheme === 'light';
        }
        // ===== 新增：初始化字体开关状态 =====
        if (fontToggle) {
            fontToggle.checked = savedFont === 'georgia'; // 勾选=Georgia，未勾选=雅黑
        }
    }

    function initFirstVisit(){
        if(firstvisit){
            showCenterAlert('欢迎来到gign.ltd\n我们推荐你关闭例如Dark Reader等浏览器插件, 使用我们内置的主题样式切换开关\n它位于目录标题旁');
            showCenterAlert('默认为深色样式, 你可以通过目录底部的开关随时切换它\n当目录过长时, 你可以通过滚动目录来查看所有内容', {icon: '?'});
            localStorage.setItem('siteTheme', 'dark');
            localStorage.setItem('siteFont', 'yahei'); // 新增：默认保存雅黑字体
        }
    }

    // 兼容两种场景：DOM已解析完成（脚本加载晚）/未解析完成（脚本加载早）
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initToggleState);
        document.addEventListener('DOMContentLoaded', initFirstVisit);
    } else {
        initToggleState();
        initFirstVisit();
    }
})();