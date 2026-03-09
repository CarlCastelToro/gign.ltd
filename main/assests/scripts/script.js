document.addEventListener('DOMContentLoaded', function () {
    // 原有锚点跳转逻辑
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
    
    // 创建回到顶部按钮
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

    // 回到顶部按钮 - 绑定代码
    const backToTopButton = document.querySelector('.back-to-top');
    window.addEventListener('scroll', function () {
        backToTopButton.style.display = window.scrollY > 300 ? 'block' : 'none';
    });

    //主题切换逻辑
    const themeToggle = document.getElementById('themeToggle');
    if (themeToggle) {
        //初始化主题
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

        //切换主题
        const switchTheme = () => {
            const isLight = themeToggle.checked;
            const newTheme = isLight ? 'light' : 'dark';
            
            if (isLight) {
                document.documentElement.classList.add('light-theme');
            } else {
                document.documentElement.classList.remove('light-theme');
            }
            
            localStorage.setItem('siteTheme', newTheme);
        };

        // 初始化主题
        initTheme();

        themeToggle.addEventListener('change', switchTheme, { passive: true });
    }

    //字体切换逻辑
    const fontToggle = document.getElementById('fontToggle');
    if (fontToggle) {
        //初始化字体
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

        //切换字体
        const switchFont = () => {
            const isGeorgia = !fontToggle.checked;
            const newFont = isGeorgia ? 'georgia' : 'yahei';
            
            if (isGeorgia) {
                document.documentElement.classList.add('font-georgia');
                document.documentElement.classList.remove('font-yahei');
            } else {
                document.documentElement.classList.add('font-yahei');
                document.documentElement.classList.remove('font-georgia');
            }
            
            localStorage.setItem('siteFont', newFont);
        };

        // 初始化字体
        initFont();

        fontToggle.addEventListener('change', switchFont, { passive: true });
    }

    const video = document.getElementById('autoplayvideo');
    

    //autoplayvideo视频元素的自动播放
    var videofirsttimeplay = false;
    // 监听用户交互事件（如点击、触摸等）
    document.addEventListener('click', function() {
        // 开始播放视频
        if(!videofirsttimeplay && video){
            video.play().catch(function(error) {
                console.log('播放失败:', error);
            });
            videofirsttimeplay = true;
        }
    });


});