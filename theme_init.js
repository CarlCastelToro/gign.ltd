// 立即执行函数，在脚本加载时立刻执行（无需等待DOMContentLoaded）
(function() {
    // 关键：不依赖DOMContentLoaded，直接操作document.head（浏览器解析<head>时就存在）
    // 1. 获取本地存储的主题偏好，默认深色
    var firstvisit = false;
    if(localStorage.getItem('siteTheme')===null){
        firstvisit = true;
    }
    const savedTheme = localStorage.getItem('siteTheme') || 'dark';
    const cssHref = savedTheme === 'dark' ? '/style.css' : '/style_light.css';

    // 2. 标记html根元素的主题属性（document.documentElement在脚本执行时已存在）
    document.documentElement.setAttribute('theme', savedTheme);

    // 3. 优先检查是否已有样式表，避免重复创建；无则创建并插入到<head>最顶部
    let styleLink = document.querySelector('link[rel="stylesheet"]');
    if (!styleLink) {
        styleLink = document.createElement('link');
        styleLink.rel = 'stylesheet';
        // 关键：设置disable=false确保样式立即生效，且插入到<head>第一个位置
        styleLink.disabled = false;
        // 插入到<head>最顶部，优先级最高，覆盖任何后续可能的样式
        document.head.insertBefore(styleLink, document.head.firstChild);
    }

    // 4. 同步设置样式表href（不使用异步逻辑，确保立即加载）
    styleLink.href = cssHref;

    // 5. 预初始化开关状态（仅DOM加载后执行，不影响样式加载）
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