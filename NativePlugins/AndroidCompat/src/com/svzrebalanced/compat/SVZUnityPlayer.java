package com.svzrebalanced.compat;

import android.content.Context;
import android.content.pm.PackageManager;
import android.os.Build;

import com.unity3d.player.UnityPlayer;

import java.lang.reflect.Method;

/**
 * Compatibility subclass of UnityPlayer for Android 5.0 (API 21) -> Android 16 (API 36+).
 * Prevents the Android 12+ (API 31+) TelephonyManager.listen SecurityException crash
 * in Unity 2017.4 when targetSdkVersion >= 31 and READ_PHONE_STATE is not granted.
 */
public class SVZUnityPlayer extends UnityPlayer {
    public SVZUnityPlayer(Context context) {
        super(context);
    }

    @Override
    protected void addPhoneCallListener() {
        if (Build.VERSION.SDK_INT >= 31) {
            try {
                Context ctx = getContext();
                if (ctx == null) {
                    return;
                }
                Method checkPerm = Context.class.getMethod("checkSelfPermission", String.class);
                int status = (Integer) checkPerm.invoke(ctx, "android.permission.READ_PHONE_STATE");
                if (status != PackageManager.PERMISSION_GRANTED) {
                    // Android 12–16 throws SecurityException inside TelephonyManager.listen(..., LISTEN_CALL_STATE)
                    // when READ_PHONE_STATE is not granted. Audio focus still handles incoming call muting safely.
                    return;
                }
            } catch (Throwable ignored) {
                return;
            }
        }

        try {
            super.addPhoneCallListener();
        } catch (SecurityException ignored) {
            // Do not crash if OEM / OS denies phone state listener at runtime
        } catch (Throwable ignored) {
        }
    }
}
