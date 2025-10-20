# PlayFab用户系统使用说明

## 概述
这是一个完整的PlayFab用户管理系统，支持多平台登录、用户名管理和UI显示。

## 功能特性
- ✅ 自动用户注册和登录
- ✅ 随机用户名生成
- ✅ 用户名显示UI
- ✅ 设置面板（修改用户名、绑定登录方式）
- ✅ 多平台登录支持（Google、Facebook、Steam）
- ✅ 本地数据存储
- ✅ 完整的错误处理

## 文件结构
```
Assets/Scripts/PlayFab/
├── PlayFabManager.cs          # PlayFab核心管理器
├── UserAuthentication.cs       # 用户认证系统
├── UsernameManager.cs         # 用户名管理系统
├── PlayFabInitializer.cs      # 系统初始化器
├── UI/
│   ├── UsernameDisplayUI.cs   # 用户名显示UI
│   └── SettingsUI.cs          # 设置面板UI
└── README.md                  # 使用说明
```

## 快速开始

### 1. 设置PlayFab
1. 在PlayFab控制台创建新项目
2. 获取Title ID
3. 在`PlayFabInitializer`中设置Title ID

### 2. 创建UI
1. 在场景中创建Canvas
2. 添加用户名显示UI组件（**必须使用TextMeshPro - Text (UI)**）
3. 添加设置按钮和面板
4. 将UI组件拖拽到对应的脚本字段

#### TextMeshPro设置说明
- **用户名显示**: 必须使用 `TextMeshPro - Text (UI)` 组件
- **富文本支持**: 可以启用富文本格式（颜色、样式等）
- **前缀/后缀**: 可以为用户名添加前缀和后缀
- **颜色设置**: 支持自定义用户名颜色

### 3. 初始化系统
```csharp
// 方法1: 自动初始化（推荐）
// 将PlayFabInitializer脚本挂载到场景中的GameObject上

// 方法2: 手动初始化
PlayFabInitializer initializer = FindObjectOfType<PlayFabInitializer>();
initializer.InitializePlayFabSystem();
```

## 详细使用说明

### PlayFabManager
核心管理器，负责：
- PlayFab初始化和配置
- 用户登录和注册
- 用户名设置和获取
- 用户数据存储

### UserAuthentication
用户认证系统，支持：
- 自动登录（基于设备ID）
- Google登录
- Facebook登录
- Steam登录
- 账户链接

### UsernameManager
用户名管理系统，功能：
- 随机用户名生成（格式：snek + 4-5位随机数，如：snek12345）
- 用户名验证
- 用户名可用性检查
- 本地用户名存储

### UsernameDisplayUI
用户名显示UI组件（使用TextMeshPro）：
- 自动显示当前用户名
- 支持富文本格式
- 支持用户名前缀/后缀
- 支持自定义颜色
- 加载动画
- 点击事件处理
- 错误状态显示

### SettingsUI
设置面板UI：
- 用户名修改
- 随机用户名生成
- 登录方式绑定
- 系统设置

## 配置说明

### PlayFab设置
```csharp
[SerializeField] private string playFabTitleId = "YOUR_TITLE_ID";
```

### 用户名设置
```csharp
[SerializeField] private int maxUsernameLength = 20;
[SerializeField] private int minUsernameLength = 3;
[SerializeField] private bool allowSpecialCharacters = false;
```

### UI设置
```csharp
[SerializeField] private TextMeshProUGUI usernameText;
[SerializeField] private Button settingsButton;
[SerializeField] private GameObject settingsPanel;
```

## 事件系统

### PlayFabManager事件
- `OnLoginResult(bool success)` - 登录结果
- `OnUsernameChanged(string username)` - 用户名变化
- `OnError(string error)` - 错误信息

### UsernameManager事件
- `OnUsernameChanged(string username)` - 用户名变化
- `OnUsernameValidationResult(bool isValid)` - 用户名验证结果
- `OnError(string error)` - 错误信息

### UserAuthentication事件
- `OnAuthenticationResult(bool success, string message)` - 认证结果
- `OnError(string error)` - 错误信息

## 常见问题

### Q: 如何设置PlayFab Title ID？
A: 在PlayFab控制台获取Title ID，然后在`PlayFabInitializer`脚本中设置`playFabTitleId`字段。

### Q: 用户名显示在哪里？
A: 用户名会显示在`UsernameDisplayUI`组件绑定的Text组件中。确保将UI组件正确拖拽到脚本字段。

### Q: 如何自定义用户名生成规则？
A: 修改`UsernameManager.cs`中的`GenerateRandomUsername()`方法。

### Q: 如何添加新的登录方式？
A: 在`UserAuthentication.cs`中添加新的登录方法，并在`SettingsUI.cs`中添加对应的UI按钮。

## 注意事项

1. **PlayFab SDK**: 确保已正确安装PlayFab SDK
2. **网络权限**: 确保应用有网络访问权限
3. **平台支持**: 不同平台的登录方式需要相应的SDK支持
4. **错误处理**: 所有网络操作都有完整的错误处理
5. **数据持久化**: 用户数据会自动保存到PlayFab云端

## 扩展功能

### TextMeshPro高级用法
```csharp
// 设置用户名前缀和后缀
usernameDisplayUI.SetUsernamePrefix("玩家: ");
usernameDisplayUI.SetUsernameSuffix(" [在线]");

// 设置用户名颜色
usernameDisplayUI.SetUsernameColor(Color.green);

// 启用富文本支持
usernameDisplayUI.SetRichTextEnabled(true);

// 设置文本大小
usernameDisplayUI.SetUsernameSize(24f);
```

### 添加新的用户数据
```csharp
// 设置用户数据
PlayFabManager.Instance.SetUserData("level", "10", 
    () => Debug.Log("等级保存成功"),
    error => Debug.LogError($"保存失败: {error}"));

// 获取用户数据
PlayFabManager.Instance.GetUserData("level", 
    level => Debug.Log($"当前等级: {level}"),
    error => Debug.LogError($"获取失败: {error}"));
```

### 自定义用户名验证
```csharp
// 在UsernameManager中添加自定义验证规则
private bool CustomUsernameValidation(string username)
{
    // 添加你的验证逻辑
    return true;
}
```

## 技术支持

如有问题，请检查：
1. PlayFab控制台设置
2. 网络连接状态
3. 控制台错误信息
4. 脚本字段绑定

## 错误修复记录

### v1.1 修复内容
- ✅ 修复了 `PlayFabManager.IsLoggedIn` 属性访问问题
- ✅ 修复了 `PlayFabManager.PlayFabId` 属性访问问题
- ✅ 修复了 `PlayFabManager.CurrentUsername` 属性访问问题
- ✅ 为 `UsernameManager` 和 `UserAuthentication` 添加了单例模式
- ✅ 修复了 `SettingsUI` 中的 `UsernameManager.Instance` 访问问题
- ✅ 修复了 `UsernameDisplayUI` 中重复的 `SetUsernameColor` 方法
- ✅ 增强了 `UsernameDisplayUI` 的TextMeshPro支持
- ✅ 修复了PlayFab Title ID设置逻辑错误
- ✅ 添加了 `SetTitleId` 方法用于动态设置Title ID
- ✅ 简化了用户名生成规则为 "snek" + 随机数格式
- ✅ 添加了智能重试机制和错误处理
- ✅ 修复了 `UserAccountInfo` 属性访问错误（`LastLoginTime`、`AccountType`、`Verified`）
- ✅ 添加了玩家后端验证功能
- ✅ 添加了 `PlayFabTest.cs` 测试脚本

### 常见编译错误解决方案
1. **CS0272错误**: 所有PlayFabManager属性现在都可以正确设置
   - `IsLoggedIn` 属性: `{ get; set; }`
   - `PlayFabId` 属性: `{ get; set; }`
   - `CurrentUsername` 属性: `{ get; set; }`
2. **CS0117错误**: 所有管理器现在都有 `Instance` 属性
3. **CS0111错误**: 修复了重复的方法定义（`SetUsernameColor`）
4. **CS1061错误**: 修复了 `UserAccountInfo` 属性访问错误（`LastLoginTime`、`AccountType`、`Verified`）
5. **单例模式**: 所有管理器都使用单例模式，确保全局访问

## 版本信息
- 版本: 1.1
- 兼容Unity版本: 2020.3+
- PlayFab SDK版本: 最新
