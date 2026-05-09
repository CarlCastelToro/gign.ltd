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
        link.onclick = function () {
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
    document.addEventListener('click', function () {
        // 开始播放视频
        if (!videofirsttimeplay && video) {
            video.play().catch(function (error) {
                console.log('播放失败:', error);
            });
            videofirsttimeplay = true;
        }
    });

    // 为带有class="loadstylesheet"的code元素自动应用IDE风格样式
    function applyIDEStyleToCode() {
        // 查找所有带有class="loadstylesheet"的code元素
        const codeElements = document.querySelectorAll('code.loadstylesheet');

        codeElements.forEach(codeElement => {
            // 检查元素是否已经被处理过
            if (codeElement.classList.contains('highlighted')) {
                return;
            }

            // 标记元素为已处理
            codeElement.classList.add('highlighted');

            // 为code元素添加必要的样式类
            codeElement.classList.add('ide-style-code');

            // 检查父元素是否为pre，如果不是，创建一个pre元素包裹code
            if (!codeElement.parentElement.matches('pre')) {
                const preElement = document.createElement('pre');
                codeElement.parentNode.insertBefore(preElement, codeElement);
                preElement.appendChild(codeElement);
            }

            // 获取语法类型（默认为js）
            const syntaxType = codeElement.getAttribute('name') || 'js';

            // 保存原始纯文本内容
            const originalText = codeElement.textContent;

            // 完全清空code元素
            codeElement.textContent = '';

            // 手动构建语法高亮内容
            buildHighlightedCode(codeElement, originalText, syntaxType);
        });
    }

    // 手动构建语法高亮内容
    function buildHighlightedCode(codeElement, text, syntaxType) {
        // 按行分割文本
        const lines = text.split('\n');
        let minIndent = Infinity;
        lines.forEach(line => {
            if (line.trim().length > 0) { // 忽略空行
                const indent = line.match(/^(\s*)/)[1].length;
                minIndent = Math.min(minIndent, indent);
            }
        });
        if (minIndent === Infinity) minIndent = 0;
        lines.forEach((line, index) => {
            if (line.length >= minIndent) {
                line = line.substring(minIndent);
            }
            // 创建行元素
            const lineElement = document.createElement('div');

            // 根据语法类型处理注释
            let commentIndex = -1;
            if (syntaxType === 'js' && line.includes('//')) {
                commentIndex = line.indexOf('//');
            } else if (syntaxType === 'vb.net' && line.includes("'")) {
                commentIndex = line.indexOf("'");
            }

            if (commentIndex !== -1) {
                const codePart = line.substring(0, commentIndex);
                const commentPart = line.substring(commentIndex);

                // 处理代码部分
                if (codePart.trim()) {
                    processCodePart(lineElement, codePart, syntaxType);
                }

                // 处理注释部分
                const commentElement = document.createElement('span');
                commentElement.className = 'comment';
                commentElement.textContent = commentPart;
                lineElement.appendChild(commentElement);
            } else {
                // 处理没有注释的行
                processCodePart(lineElement, line, syntaxType);
            }

            // 添加行元素到code元素
            codeElement.appendChild(lineElement);

            // 添加换行符（除了最后一行）
            if (index < lines.length - 1) {
                codeElement.appendChild(document.createTextNode('\n'));
            }
        });
    }

    // 处理代码部分
    function processCodePart(parentElement, code, syntaxType) {
        // 定义关键字列表
        const keywords = {
            'js': ['var', 'let', 'const', 'function', 'return', 'if', 'else', 'for', 'while', 'do', 'switch', 'case', 'default', 'break', 'continue', 'class', 'extends', 'import', 'export', 'from', 'async', 'await', 'try', 'catch', 'finally', 'throw', 'new', 'this', 'super', 'static'],
            'vb.net': ['Console', 'Object', 'As' , 'Handles', 'End', 'Module', 'Class', 'Dim', 'Private', 'Public', 'Function', 'Sub', 'Return', 'If', 'Else', 'For', 'While', 'Do', 'Select', 'Case', 'Default', 'Exit', 'Continue', 'Class', 'Inherits', 'Import', 'Export', 'Async', 'Await', 'Try', 'Catch', 'Finally', 'Throw', 'New', 'Me', 'MyBase', 'Shared']
        };

        // 获取当前语法的关键字列表
        const currentKeywords = keywords[syntaxType] || keywords['js'];

        // 创建一个临时容器来处理代码
        const tempContainer = document.createElement('div');

        // 处理代码，使用DOM操作而不是innerHTML
        let remainingCode = code;

        // 处理字符串（最先处理，避免干扰其他替换）
        const stringRegex = /"([^"]*)"|'([^']*)'/g;
        let match;
        let lastIndex = 0;

        while ((match = stringRegex.exec(remainingCode)) !== null) {
            // 添加匹配前的文本
            if (match.index > lastIndex) {
                const textBefore = remainingCode.substring(lastIndex, match.index);
                processText(tempContainer, textBefore, currentKeywords, syntaxType);
            }

            // 添加字符串
            const stringElement = document.createElement('span');
            stringElement.className = 'string';
            stringElement.textContent = match[0];
            tempContainer.appendChild(stringElement);

            lastIndex = match.index + match[0].length;
        }

        // 处理剩余的文本
        if (lastIndex < remainingCode.length) {
            const textAfter = remainingCode.substring(lastIndex);
            processText(tempContainer, textAfter, currentKeywords, syntaxType);
        }

        // 将处理后的内容添加到父元素
        while (tempContainer.firstChild) {
            parentElement.appendChild(tempContainer.firstChild);
        }
    }

    // 处理文本部分
    function processText(parentElement, text, keywords, syntaxType) {
        // 处理数字
        const numberRegex = /\b\d+(\.\d+)?\b/g;
        let match;
        let lastIndex = 0;

        while ((match = numberRegex.exec(text)) !== null) {
            // 添加匹配前的文本
            if (match.index > lastIndex) {
                const textBefore = text.substring(lastIndex, match.index);
                processKeywords(parentElement, textBefore, keywords, syntaxType);
            }

            // 添加数字
            const numberElement = document.createElement('span');
            numberElement.className = 'number';
            numberElement.textContent = match[0];
            parentElement.appendChild(numberElement);

            lastIndex = match.index + match[0].length;
        }

        // 处理剩余的文本
        if (lastIndex < text.length) {
            const textAfter = text.substring(lastIndex);
            processKeywords(parentElement, textAfter, keywords, syntaxType);
        }
    }

    // 处理关键字和函数名
    function processKeywords(parentElement, text, keywords, syntaxType) {
        // 处理函数名
        let processedText = text;

        if (syntaxType === 'js') {
            processedText = processedText.replace(/function\s+(\w+)/g, (match, funcName) => {
                return 'function ' + funcName;
            });
        } else if (syntaxType === 'vb.net') {
            processedText = processedText.replace(/Function\s+(\w+)/g, (match, funcName) => {
                return 'Function ' + funcName;
            });
            processedText = processedText.replace(/Sub\s+(\w+)/g, (match, subName) => {
                return 'Sub ' + subName;
            });
        }

        // 按空格分割文本
        const tokens = processedText.split(/(\s+)/);

        tokens.forEach(token => {
            // 检查是否是关键字
            if (keywords.includes(token)) {
                const keywordElement = document.createElement('span');
                keywordElement.className = 'keyword';
                keywordElement.textContent = token;
                parentElement.appendChild(keywordElement);
            } else if (token.trim() === '') {
                // 处理空格
                parentElement.appendChild(document.createTextNode(token));
            } else {
                // 处理普通文本
                parentElement.appendChild(document.createTextNode(token));
            }
        });
    }

    // 执行自动应用IDE风格样式的函数
    applyIDEStyleToCode();

    // 为带有class="autodecrypt"的元素自动解密内容
    function applyAutoDecrypt() {
        const encryptedElements = document.querySelectorAll('.autodecrypt');

        encryptedElements.forEach(element => {
            // 检查元素是否已经被处理过
            if (element.classList.contains('decrypted')) {
                return;
            }

            // 标记元素为已处理
            element.classList.add('decrypted');

            // 获取加密类型（默认为basecoder）
            const decryptType = element.getAttribute('name') || 'basecoder';

            // 获取原始加密文本（去除首尾空白）
            const encryptedText = element.textContent.trim();

            if (!encryptedText) {
                return;
            }

            // 解密
            let decryptedText = '';
            try {
                switch (decryptType) {
                    case 'basecoder':
                        decryptedText = basecoderDecode(encryptedText);
                        break;
                    case 'safe62':
                        // safe62 返回 Uint8Array，转为 UTF-8 文本
                        const bytes = _safe62.decode(encryptedText);
                        decryptedText = new TextDecoder().decode(bytes);
                        break;
                    case 'base64':
                        // Base64 解码：先转二进制字符串，再转 UTF-8
                        const binaryStr = atob(encryptedText);
                        const uint8arr = Uint8Array.from([...binaryStr].map(c => c.charCodeAt(0)));
                        decryptedText = new TextDecoder().decode(uint8arr);
                        break;
                    default:
                        decryptedText = '[未知加密类型: ' + decryptType + ']';
                }
            } catch (err) {
                decryptedText = '[解密失败: ' + err.message + ']';
            }

            // 清空元素内容
            element.textContent = '';

            // 按换行符分割，使用 DOM 节点正确处理换行
            const lines = decryptedText.split('\n');
            lines.forEach((line, index) => {
                element.appendChild(document.createTextNode(line));
                // 除了最后一行，每行末尾添加 <br>
                if (index < lines.length - 1) {
                    element.appendChild(document.createElement('br'));
                }
            });
        });
    }

    // 执行自动解密函数
    applyAutoDecrypt();

    // 来访者统计功能
    async function loadVisitorStats() {
        const countElement = document.getElementById('visitor-count');
        const statusElement = document.getElementById('visitor-status');
        if (!countElement || !statusElement) return;

        try {
            const response = await fetch('https://kv.gign.ltd/stats/json');
            if (!response.ok) throw new Error('Failed to fetch stats');
            
            const data = await response.json();
            
            // 更新数字
            countElement.textContent = data.totalVisitors;
            countElement.classList.add('loaded');
            
            // 更新状态
            if (data.isNewVisitor) {
                statusElement.textContent = '新访客';
                statusElement.className = 'visitor-stats-status new-visitor';
            } else {
                statusElement.textContent = '欢迎回来';
                statusElement.className = 'visitor-stats-status returning-visitor';
            }
        } catch (error) {
            console.log('加载访客统计失败:', error);
            countElement.textContent = '--';
            statusElement.textContent = '暂不可用';
            statusElement.className = 'visitor-stats-status error';
        }
    }

    // 加载访客统计
    loadVisitorStats();
});