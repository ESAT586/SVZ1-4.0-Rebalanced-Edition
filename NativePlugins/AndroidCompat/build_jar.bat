@echo off
setlocal
set JDK_BIN=C:\Users\esatg\Downloads\OpenJDK8U-jdk_x64_windows_hotspot_8u492b09\jdk8u492-b09\bin
set ANDROID_JAR=C:\Users\esatg\AppData\Local\Android\Sdk\platforms\android-34\android.jar
set UNITY_JAR=C:\Program Files\Unity\Hub\Editor\2017.4.40f1\Editor\Data\PlaybackEngines\AndroidPlayer\Variations\il2cpp\Release\Classes\classes.jar
set SRC_DIR=%~dp0src
set OUT_CLS=%~dp0build\classes
set OUT_JAR=%~dp0..\..\Assets\Plugins\Android\svz-android-compat.jar

if exist "%OUT_CLS%" rmdir /s /q "%OUT_CLS%"
mkdir "%OUT_CLS%"

"%JDK_BIN%\javac.exe" -source 1.8 -target 1.8 -Xlint:unchecked -bootclasspath "%ANDROID_JAR%" -classpath "%UNITY_JAR%" -d "%OUT_CLS%" "%SRC_DIR%\com\svzrebalanced\compat\SVZAndroidBridge.java" "%SRC_DIR%\com\svzrebalanced\compat\SVZUnityPlayer.java" "%SRC_DIR%\com\svzrebalanced\compat\SVZUnityPlayerActivity.java"
if errorlevel 1 exit /b 1

"%JDK_BIN%\jar.exe" cf "%OUT_JAR%" -C "%OUT_CLS%" .
if errorlevel 1 exit /b 1

echo Built %OUT_JAR% successfully.
