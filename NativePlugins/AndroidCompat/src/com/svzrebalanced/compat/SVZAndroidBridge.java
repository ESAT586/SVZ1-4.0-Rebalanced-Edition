package com.svzrebalanced.compat;

import android.app.Activity;
import android.app.UiModeManager;
import android.content.Context;
import android.content.pm.PackageManager;
import android.content.res.Configuration;
import android.net.ConnectivityManager;
import android.net.LinkAddress;
import android.net.LinkProperties;
import android.net.wifi.WifiInfo;
import android.net.wifi.WifiManager;
import android.os.Build;
import android.view.Display;
import android.view.View;
import android.view.Window;
import android.view.WindowInsets;
import android.view.WindowManager;

import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.net.Inet4Address;
import java.net.InetAddress;
import java.net.NetworkInterface;
import java.util.Collections;
import java.util.Enumeration;
import java.util.List;

/**
 * Android 5.0 (API 21) -> Android 16 (API 36+) runtime compatibility helper.
 * Uses reflection for newer platform APIs so one JAR works safely across all API levels.
 */
public final class SVZAndroidBridge {
    private static final int CUTOUT_MODE_SHORT_EDGES = 1; // WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES (API 28+)
    private static final int BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE = 2; // WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE (API 30+)
    private static final int LEGACY_IMMERSIVE_FLAGS =
            View.SYSTEM_UI_FLAG_LAYOUT_STABLE
            | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
            | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
            | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
            | View.SYSTEM_UI_FLAG_FULLSCREEN
            | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY;

    private SVZAndroidBridge() {
    }

    public static int getApiLevel() {
        return Build.VERSION.SDK_INT;
    }

    public static String getAndroidVersionLabel() {
        String rel = Build.VERSION.RELEASE != null ? Build.VERSION.RELEASE : "?";
        return "Android " + rel + " (API " + Build.VERSION.SDK_INT + ")";
    }

    public static String getSupportedAbisLabel() {
        try {
            if (Build.VERSION.SDK_INT >= 21 && Build.SUPPORTED_ABIS != null && Build.SUPPORTED_ABIS.length > 0) {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < Build.SUPPORTED_ABIS.length; i++) {
                    if (i > 0) sb.append(", ");
                    sb.append(Build.SUPPORTED_ABIS[i]);
                }
                return sb.toString();
            }
        } catch (Throwable ignored) {
        }
        return Build.CPU_ABI != null ? Build.CPU_ABI : "unknown";
    }

    public static boolean isAndroidTvDevice(Context context) {
        if (context == null) return false;
        try {
            UiModeManager uiMode = (UiModeManager) context.getSystemService(Context.UI_MODE_SERVICE);
            if (uiMode != null && uiMode.getCurrentModeType() == Configuration.UI_MODE_TYPE_TELEVISION) {
                return true;
            }
        } catch (Throwable ignored) {
        }

        try {
            PackageManager pm = context.getPackageManager();
            if (pm != null) {
                if (pm.hasSystemFeature("android.software.leanback")
                        || pm.hasSystemFeature("android.hardware.type.television")) {
                    return true;
                }
            }
        } catch (Throwable ignored) {
        }
        return false;
    }

    public static boolean isChromebookOrDesktop(Context context) {
        if (context == null) return false;
        try {
            PackageManager pm = context.getPackageManager();
            if (pm != null) {
                if (pm.hasSystemFeature("org.chromium.arc")
                        || pm.hasSystemFeature("org.chromium.arc.device_management")
                        || pm.hasSystemFeature("android.hardware.type.pc")) {
                    return true;
                }
            }
        } catch (Throwable ignored) {
        }
        return false;
    }

    public static void applyWindowCompatibility(final Activity activity) {
        if (activity == null) return;
        try {
            final Window window = activity.getWindow();
            if (window == null) return;

            window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);

            // API 28+ (Android 9 -> 16): Render into display cutout / punch-hole edges in landscape
            if (Build.VERSION.SDK_INT >= 28) {
                try {
                    WindowManager.LayoutParams lp = window.getAttributes();
                    Field cutoutField = WindowManager.LayoutParams.class.getField("layoutInDisplayCutoutMode");
                    if (cutoutField.getInt(lp) != CUTOUT_MODE_SHORT_EDGES) {
                        cutoutField.setInt(lp, CUTOUT_MODE_SHORT_EDGES);
                        window.setAttributes(lp);
                    }
                } catch (Throwable ignored) {
                }
            }

            // API 30+ (Android 11 -> 16): Modern WindowInsetsController transient fullscreen
            boolean appliedModernInsets = false;
            if (Build.VERSION.SDK_INT >= 30) {
                try {
                    Method setDecorFits = Window.class.getMethod("setDecorFitsSystemWindows", boolean.class);
                    setDecorFits.invoke(window, false);

                    Method getController = Window.class.getMethod("getInsetsController");
                    Object controller = getController.invoke(window);
                    if (controller != null) {
                        Class<?> typeCls = Class.forName("android.view.WindowInsets$Type");
                        int sysBars = (Integer) typeCls.getMethod("systemBars").invoke(null);
                        Class<?> ctrlCls = Class.forName("android.view.WindowInsetsController");
                        ctrlCls.getMethod("hide", int.class).invoke(controller, sysBars);
                        ctrlCls.getMethod("setSystemBarsBehavior", int.class).invoke(controller, BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
                        appliedModernInsets = true;
                    }
                } catch (Throwable ignored) {
                }
            }

            // Fallback & API 21-29 (Android 5.0 -> 10): Sticky immersive fullscreen
            if (!appliedModernInsets) {
                View decor = window.getDecorView();
                if (decor != null) {
                    decor.setSystemUiVisibility(LEGACY_IMMERSIVE_FLAGS);
                }
            }
        } catch (Throwable ignored) {
        }
    }

    /**
     * Returns display cutout / unavoidable inset padding in pixels: [left, top, right, bottom].
     * Works across Android 9–16 even when Unity 2017's native Screen.safeArea ignores punch-holes.
     */
    public static int[] getSafeInsetsPx(Activity activity) {
        int[] result = new int[] { 0, 0, 0, 0 };
        if (activity == null || Build.VERSION.SDK_INT < 28) {
            return result;
        }

        try {
            Window window = activity.getWindow();
            if (window == null) return result;
            View decor = window.getDecorView();
            if (decor == null) return result;
            Method getRootInsets = View.class.getMethod("getRootWindowInsets");
            Object rootInsetsObj = getRootInsets.invoke(decor);
            if (!(rootInsetsObj instanceof WindowInsets)) return result;
            WindowInsets rootInsets = (WindowInsets) rootInsetsObj;

            // 1. DisplayCutout safe insets (API 28+)
            try {
                Method getCutout = WindowInsets.class.getMethod("getDisplayCutout");
                Object cutout = getCutout.invoke(rootInsets);
                if (cutout != null) {
                    Class<?> cutoutCls = cutout.getClass();
                    int l = (Integer) cutoutCls.getMethod("getSafeInsetLeft").invoke(cutout);
                    int t = (Integer) cutoutCls.getMethod("getSafeInsetTop").invoke(cutout);
                    int r = (Integer) cutoutCls.getMethod("getSafeInsetRight").invoke(cutout);
                    int b = (Integer) cutoutCls.getMethod("getSafeInsetBottom").invoke(cutout);
                    result[0] = Math.max(result[0], l);
                    result[1] = Math.max(result[1], t);
                    result[2] = Math.max(result[2], r);
                    result[3] = Math.max(result[3], b);
                }
            } catch (Throwable ignored) {
            }

            // 2. API 30+: displayCutout + any visible non-hidden system/caption bars (multi-window / Android 15-16 desktop)
            if (Build.VERSION.SDK_INT >= 30) {
                try {
                    Class<?> typeCls = Class.forName("android.view.WindowInsets$Type");
                    int cutoutType = (Integer) typeCls.getMethod("displayCutout").invoke(null);
                    Method getIgnoring = WindowInsets.class.getMethod("getInsetsIgnoringVisibility", int.class);
                    Object cutoutInsets = getIgnoring.invoke(rootInsets, cutoutType);
                    if (cutoutInsets != null) {
                        Class<?> insetsCls = cutoutInsets.getClass();
                        result[0] = Math.max(result[0], insetsCls.getField("left").getInt(cutoutInsets));
                        result[1] = Math.max(result[1], insetsCls.getField("top").getInt(cutoutInsets));
                        result[2] = Math.max(result[2], insetsCls.getField("right").getInt(cutoutInsets));
                        result[3] = Math.max(result[3], insetsCls.getField("bottom").getInt(cutoutInsets));
                    }

                    // If system bars or caption bar are currently visible (e.g. split screen / freeform / edge-to-edge fallback), pad them too
                    int captionType = 0;
                    try {
                        captionType = (Integer) typeCls.getMethod("captionBar").invoke(null);
                    } catch (Throwable ignored) {
                    }
                    int sysBarsType = (Integer) typeCls.getMethod("systemBars").invoke(null);
                    Method getVisible = WindowInsets.class.getMethod("getInsets", int.class);
                    Object visibleSys = getVisible.invoke(rootInsets, sysBarsType | captionType);
                    if (visibleSys != null) {
                        Class<?> insetsCls = visibleSys.getClass();
                        result[0] = Math.max(result[0], insetsCls.getField("left").getInt(visibleSys));
                        result[1] = Math.max(result[1], insetsCls.getField("top").getInt(visibleSys));
                        result[2] = Math.max(result[2], insetsCls.getField("right").getInt(visibleSys));
                        result[3] = Math.max(result[3], insetsCls.getField("bottom").getInt(visibleSys));
                    }
                } catch (Throwable ignored) {
                }
            }
        } catch (Throwable ignored) {
        }
        return result;
    }

    /**
     * Resolves local LAN IPv4 reliably on Android 5.0–16 without depending on DNS hostname lookup.
     */
    public static String getLanIpv4Address(Context context) {
        // 1. Scan active non-loopback network interfaces (prioritize wlan / eth / ap / swlan over cellular)
        try {
            String fallbackIp = null;
            Enumeration<NetworkInterface> interfaces = NetworkInterface.getNetworkInterfaces();
            if (interfaces != null) {
                for (NetworkInterface nif : Collections.list(interfaces)) {
                    if (nif == null || !nif.isUp() || nif.isLoopback()) continue;
                    String name = nif.getName() != null ? nif.getName().toLowerCase() : "";
                    boolean isCellular = name.startsWith("rmnet") || name.startsWith("ccmni") || name.startsWith("pdp");
                    boolean isLanPreferred = name.startsWith("wlan") || name.startsWith("eth") || name.startsWith("swlan") || name.startsWith("ap");

                    Enumeration<InetAddress> addrs = nif.getInetAddresses();
                    while (addrs.hasMoreElements()) {
                        InetAddress addr = addrs.nextElement();
                        if (addr instanceof Inet4Address && !addr.isLoopbackAddress() && !addr.isLinkLocalAddress()) {
                            String ip = addr.getHostAddress();
                            if (ip != null && ip.length() > 0) {
                                if (isLanPreferred) {
                                    return ip;
                                }
                                if (fallbackIp == null || (!isCellular && fallbackIp.startsWith("10."))) {
                                    fallbackIp = ip;
                                }
                            }
                        }
                    }
                }
            }
            if (fallbackIp != null) {
                return fallbackIp;
            }
        } catch (Throwable ignored) {
        }

        // 2. ConnectivityManager LinkProperties (Android 6.0+ / API 23+, invoked via reflection so API 21-22 stays clean)
        if (context != null && Build.VERSION.SDK_INT >= 23) {
            try {
                ConnectivityManager cm = (ConnectivityManager) context.getSystemService(Context.CONNECTIVITY_SERVICE);
                if (cm != null) {
                    Object active = ConnectivityManager.class.getMethod("getActiveNetwork").invoke(cm);
                    if (active != null) {
                        Class<?> netCls = Class.forName("android.net.Network");
                        LinkProperties lp = (LinkProperties) ConnectivityManager.class.getMethod("getLinkProperties", netCls).invoke(cm, active);
                        if (lp != null) {
                            List<LinkAddress> list = lp.getLinkAddresses();
                            for (LinkAddress la : list) {
                                InetAddress addr = la.getAddress();
                                if (addr instanceof Inet4Address && !addr.isLoopbackAddress() && !addr.isLinkLocalAddress()) {
                                    return addr.getHostAddress();
                                }
                            }
                        }
                    }
                }
            } catch (Throwable ignored) {
            }
        }

        // 3. WifiManager fallback
        if (context != null) {
            try {
                WifiManager wm = (WifiManager) context.getApplicationContext().getSystemService(Context.WIFI_SERVICE);
                if (wm != null) {
                    WifiInfo info = wm.getConnectionInfo();
                    if (info != null) {
                        int ipInt = info.getIpAddress();
                        if (ipInt != 0) {
                            return String.format("%d.%d.%d.%d",
                                    (ipInt & 0xff),
                                    (ipInt >> 8 & 0xff),
                                    (ipInt >> 16 & 0xff),
                                    (ipInt >> 24 & 0xff));
                        }
                    }
                }
            } catch (Throwable ignored) {
            }
        }

        return "";
    }

    public static String getDeviceDiagnosticSummary(Activity activity) {
        StringBuilder sb = new StringBuilder();
        sb.append(getAndroidVersionLabel());
        sb.append(" | ABI: ").append(getSupportedAbisLabel());
        if (activity != null) {
            sb.append(" | TV=").append(isAndroidTvDevice(activity));
            sb.append(" | Desktop=").append(isChromebookOrDesktop(activity));
            try {
                Display disp = activity.getWindowManager() != null ? activity.getWindowManager().getDefaultDisplay() : null;
                if (disp != null) {
                    sb.append(" | Hz=").append(Math.round(disp.getRefreshRate()));
                }
            } catch (Throwable ignored) {
            }
            int[] insets = getSafeInsetsPx(activity);
            sb.append(" | CutoutInsets=[").append(insets[0]).append(',').append(insets[1]).append(',').append(insets[2]).append(',').append(insets[3]).append(']');
        }
        return sb.toString();
    }
}
