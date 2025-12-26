// 立即执行函数，在脚本加载时立刻执行（无需等待DOMContentLoaded）
(function() {
    // 1. 获取本地存储的主题偏好，默认深色
    var firstvisit = false;
    if(localStorage.getItem('siteTheme')===null){
        firstvisit = true;
    }
    const savedTheme = localStorage.getItem('siteTheme') || 'dark';
    
    // 2. 标记html根元素的主题属性
    document.documentElement.setAttribute('theme', savedTheme);

    // 3. 移除可能存在的旧样式表（为了兼容性）
    const oldStyleLinks = document.querySelectorAll('link[rel="stylesheet"]');
    oldStyleLinks.forEach(link => link.remove());
    
    // 4. 创建并预加载深色样式表
    const darkStyle = document.createElement('link');
    darkStyle.rel = 'stylesheet';
    darkStyle.href = '/style.css';
    darkStyle.id = 'dark-theme';
    darkStyle.disabled = savedTheme !== 'dark'; // 根据保存的主题设置启用状态
    
    // 5. 创建并预加载浅色样式表
    const lightStyle = document.createElement('link');
    lightStyle.rel = 'stylesheet';
    lightStyle.href = '/style_light.css';
    lightStyle.id = 'light-theme';
    lightStyle.disabled = savedTheme !== 'light'; // 根据保存的主题设置启用状态
    
    // 6. 将两个样式表添加到文档头部
    document.head.appendChild(darkStyle);
    document.head.appendChild(lightStyle);

    // 7. 预初始化开关状态（仅DOM加载后执行，不影响样式加载）
    function initToggleState() {
        const themeToggle = document.getElementById('themeToggle');
        if (themeToggle) {
            themeToggle.checked = savedTheme === 'light';
        }
    }

    function initFirstVisit(){
        if(firstvisit){
            showCenterAlert('欢迎来到gign.ltd\n我们推荐你关闭例如Dark Reader等浏览器插件, 使用我们内置的主题样式切换开关\n它位于目录标题旁');
            showCenterAlert('默认为深色样式, 你可以通过开关随时切换它', {icon: '?'});
            localStorage.setItem('siteTheme', 'dark');
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