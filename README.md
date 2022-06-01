# Nanonis .sxm File Helper [Ver 1.3]

### Preview in Windows File Explorer

**蓝色现在已升级为Nanonis Blue！**

![](README.assets/regall.png)

### Right Click Menu Swift Copy Image

**下方信息栏已更新成白色！更有尺寸和文件序号信息！**

![](README.assets/rightAll.png)

##### 启动方法

```Run Setup.exe```

##### 关联.sxm 格式

让您双击启动Scan Inspector. 

- 右键 --> 打开方式 --> 浏览，定位到`C:\Program Files\Nanonis V5e\Scan Inspector\Scan Inspector.exe`，再次打开时勾选始终用这个软件打开即可。

##### 安装新版本

 **更新方法** 

- 先使用Setup.exe, 卸载旧版本。 

- 再在任务管理器中结束“COM Surrogate”的所有进程。【重要，否则旧版本无法删除】（这是文件资源管理器的附属进程，用于渲染sxm文件图标）

- 删除旧版本所有文件，安装新版本。

<u>另外，windows会缓存文件预览图标，所以卸载或者更新后，已经渲染过的图标不会改变。如需要重新渲染，请在搜索框输入“磁盘清理”，选中C盘的“缩略图”并清理即可。</u>

Notice:

> 如果想使用粘贴spm图片功能：
> 	为了正常使用粘贴功能，请不要把这个文件夹放在有空格或者特殊符号的路径上。请不要把.sxm文件放在有空格或者特殊符号的路径上。否则可能出错。

仅在文件资源管理器中使用缩略图预览功能不受此限制。

------------

#### Gallery

**==精于品质，和Nanonis Scan Inspector相比几乎相同！==**

*Current Version: Ver1.4*

<img src="README.assets/compare.png" style="zoom: 80%;" />

**==信息井然排布，整理数据不烦恼！==**

*Current Version: Ver1.4*

<img src="README.assets/Info-16540689002843.png" style="zoom: 80%;" />

---------------------------



###### Ver 1.1

1. 修复了高版本nanonis软件把Frequency_Shift 重命名为 OC M1 Freq. Shift导致无法读取的bug
2. 修复了过小图片（e.g. 64*64）无法显示的bug

###### Ver 1.2 

1. 把颜色图从Blues_r 改成Blue_like_nanonis
2. 优化了右键复制功能的文字提示效果

###### ver 1.3

1. 修复了扫描方向(i.e. up/down) 可能带来的图像翻转问题
2. 右键菜单中，新增加了手动选择Z和Frequency Shift的选项。[^1]

###### ver 1.4

1. 修复了图片宽高比不为1时仍然显示为正方形的问题
2. 修复了过小图片(i.e. width<256 pixel)导致的信息框显示不完整问题





Author: CoccaGuo

ver1.0 @ 2022.05.12

ver1.4 @ 2022.06.01





[^1]: 程序集按照Z-Controller是否打开判断Z还是Frequency Shift通道，但是有时候扫完图马上打开Controller，图片在存储的时候会记录成Z-Controller是打开的，这时需要手动选择通道。

