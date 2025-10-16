// Astrvo 极简WebGL模板 - Unity设置脚本

// 显示临时消息横幅，用于警告和错误信息
function unityShowBanner(msg, type) {
    function updateBannerVisibility() {
        warningBanner.style.display = warningBanner.children.length ? "block" : "none";
    }
    var div = document.createElement("div");
    div.innerHTML = msg;
    warningBanner.appendChild(div);
    if (type == "error") {
        div.style = "background: rgba(255, 0, 0, 0.9); color: white; padding: 15px; border-radius: 5px; margin: 10px;";
    } else {
        if (type == "warning") {
            div.style = "background: rgba(255, 165, 0, 0.9); color: white; padding: 15px; border-radius: 5px; margin: 10px;";
        }
        setTimeout(function () {
            warningBanner.removeChild(div);
            updateBannerVisibility();
        }, 5000);
    }
    updateBannerVisibility();
}

// Unity WebGL配置
var buildUrl = "Build";
var loaderUrl = buildUrl + "/{{{ LOADER_FILENAME }}}";
var config = {
    dataUrl: buildUrl + "/{{{ DATA_FILENAME }}}",
    frameworkUrl: buildUrl + "/{{{ FRAMEWORK_FILENAME }}}",
    codeUrl: buildUrl + "/{{{ CODE_FILENAME }}}",
    #if MEMORY_FILENAME
    memoryUrl: buildUrl + "/{{{ MEMORY_FILENAME }}}",
    #endif
    #if SYMBOLS_FILENAME
    symbolsUrl: buildUrl + "/{{{ SYMBOLS_FILENAME }}}",
    #endif
    streamingAssetsUrl: "StreamingAssets",
    companyName: "{{{ COMPANY_NAME }}}",
    productName: "{{{ PRODUCT_NAME }}}",
    productVersion: "{{{ PRODUCT_VERSION }}}",
    showBanner: unityShowBanner,
};

// 获取DOM元素
var container = document.querySelector("#unity-container");
var canvas = document.querySelector("#unity-canvas");
var loadingBar = document.querySelector("#unity-loading-bar");
var progressBarFull = document.querySelector("#unity-progress-bar-full");
var warningBanner = document.querySelector("#unity-warning");
var logoElement = document.querySelector("#unity-logo");

// 强制logo居中的函数
function forceLogoCenter() {
    if (logoElement) {
        logoElement.style.display = "flex";
        logoElement.style.alignItems = "center";
        logoElement.style.justifyContent = "center";
        logoElement.style.marginLeft = "auto";
        logoElement.style.marginRight = "auto";
        logoElement.style.textAlign = "center";
        logoElement.style.backgroundPosition = "center center";
        logoElement.style.backgroundRepeat = "no-repeat";
        logoElement.style.backgroundSize = "contain";
    }
}

// 移动设备检测和配置
if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {
    container.className = "unity-mobile";
    // 移动设备优化
    config.devicePixelRatio = 1;
    canvas.style.width = "100%";
    canvas.style.height = "100%";
} else {
    // 桌面设备 - 全屏显示
    canvas.style.width = "100%";
    canvas.style.height = "100%";
}

// 背景图片设置
#if BACKGROUND_FILENAME
canvas.style.background = "url('" + buildUrl + "/{{{ BACKGROUND_FILENAME.replace(/'/g, '%27') }}}') center / cover";
#endif

// 显示加载界面
loadingBar.style.display = "block";
// 立即强制logo居中
forceLogoCenter();

// 设置定时器，定期检查并修复logo居中状态
var logoCenterInterval = setInterval(function() {
    if (loadingBar.style.display !== "none") {
        forceLogoCenter();
    } else {
        clearInterval(logoCenterInterval);
    }
}, 100); // 每100ms检查一次

// 创建Unity实例
var script = document.createElement("script");
script.src = loaderUrl;
script.onload = () => {
    createUnityInstance(canvas, config, (progress) => {
        progressBarFull.style.width = 100 * progress + "%";
        // 在每次进度更新时也强制logo居中
        forceLogoCenter();
    })
    .then((unityInstance) => {
        unityGame = unityInstance;
        // 清理定时器
        clearInterval(logoCenterInterval);
        // 加载完成后隐藏加载界面
        loadingBar.style.display = "none";
        container.classList.add("loaded");
        
        // 全屏功能（如果需要）
        document.addEventListener("keydown", function(e) {
            if (e.key === "F11") {
                e.preventDefault();
                if (document.fullscreenElement) {
                    document.exitFullscreen();
                } else {
                    document.documentElement.requestFullscreen();
                }
            }
        });
    })
    .catch((message) => {
        console.error("Unity加载失败:", message);
        unityShowBanner("游戏加载失败: " + message, "error");
    });
};
document.body.appendChild(script);
