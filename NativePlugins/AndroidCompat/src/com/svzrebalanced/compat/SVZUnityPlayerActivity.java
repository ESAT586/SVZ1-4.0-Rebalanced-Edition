package com.svzrebalanced.compat;

import android.app.Activity;
import android.content.BroadcastReceiver;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.ServiceConnection;
import android.content.res.Configuration;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.Window;

import java.util.concurrent.Executor;

import com.unity3d.player.UnityPlayer;

/**
 * Drop-in replacement launcher Activity for UnityPlayerActivity.
 * Instantiates SVZUnityPlayer (safe on Android 12–16 telephony permission rules)
 * and enforces notch cutout + sticky/transient immersive compatibility on API 21–36+.
 */
public class SVZUnityPlayerActivity extends Activity {
    protected UnityPlayer mUnityPlayer; // Exact field name expected by Unity native layer

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        super.onCreate(savedInstanceState);

        mUnityPlayer = new SVZUnityPlayer(this);
        setContentView(mUnityPlayer);
        mUnityPlayer.requestFocus();

        SVZAndroidBridge.applyWindowCompatibility(this);
    }

    @Override
    protected void onNewIntent(Intent intent) {
        setIntent(intent);
    }

    @Override
    protected void onDestroy() {
        if (mUnityPlayer != null) {
            mUnityPlayer.quit();
        }
        super.onDestroy();
    }

    @Override
    protected void onPause() {
        super.onPause();
        if (mUnityPlayer != null) {
            mUnityPlayer.pause();
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (mUnityPlayer != null) {
            mUnityPlayer.resume();
        }
        SVZAndroidBridge.applyWindowCompatibility(this);
    }

    @Override
    protected void onStart() {
        super.onStart();
        if (mUnityPlayer != null) {
            mUnityPlayer.start();
        }
    }

    @Override
    protected void onStop() {
        super.onStop();
        if (mUnityPlayer != null) {
            mUnityPlayer.stop();
        }
    }

    @Override
    public void onLowMemory() {
        super.onLowMemory();
        if (mUnityPlayer != null) {
            mUnityPlayer.lowMemory();
        }
    }

    @Override
    public void onTrimMemory(int level) {
        super.onTrimMemory(level);
        if (level == TRIM_MEMORY_RUNNING_CRITICAL && mUnityPlayer != null) {
            mUnityPlayer.lowMemory();
        }
    }

    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        super.onConfigurationChanged(newConfig);
        if (mUnityPlayer != null) {
            mUnityPlayer.configurationChanged(newConfig);
        }
        SVZAndroidBridge.applyWindowCompatibility(this);
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (mUnityPlayer != null) {
            mUnityPlayer.windowFocusChanged(hasFocus);
        }
        if (hasFocus) {
            SVZAndroidBridge.applyWindowCompatibility(this);
        }
    }

    @Override
    public boolean dispatchKeyEvent(KeyEvent event) {
        if (mUnityPlayer != null && event != null && event.getAction() == KeyEvent.ACTION_MULTIPLE) {
            return mUnityPlayer.injectEvent(event);
        }
        return super.dispatchKeyEvent(event);
    }

    @Override
    public boolean onKeyUp(int keyCode, KeyEvent event) {
        return mUnityPlayer != null ? mUnityPlayer.injectEvent(event) : super.onKeyUp(keyCode, event);
    }

    @Override
    public boolean onKeyDown(int keyCode, KeyEvent event) {
        return mUnityPlayer != null ? mUnityPlayer.injectEvent(event) : super.onKeyDown(keyCode, event);
    }

    @Override
    public boolean onTouchEvent(MotionEvent event) {
        return mUnityPlayer != null ? mUnityPlayer.injectEvent(event) : super.onTouchEvent(event);
    }

    @Override
    public boolean bindService(final Intent service, ServiceConnection conn, final int flags) {
        return SVZContextCompat.bindWithCompat(conn, new SVZContextCompat.ServiceBinder() {
            @Override
            public boolean bind(ServiceConnection actual) {
                return SVZUnityPlayerActivity.super.bindService(service, actual, flags);
            }
        });
    }

    @Override
    public boolean bindService(final Intent service, final int flags, final Executor executor, ServiceConnection conn) {
        return SVZContextCompat.bindWithCompat(conn, new SVZContextCompat.ServiceBinder() {
            @Override
            public boolean bind(ServiceConnection actual) {
                return SVZUnityPlayerActivity.super.bindService(service, flags, executor, actual);
            }
        });
    }

    @Override
    public void unbindService(ServiceConnection conn) {
        super.unbindService(SVZContextCompat.releaseForUnbind(conn));
    }

    @Override
    public Intent registerReceiver(BroadcastReceiver receiver, IntentFilter filter) {
        if (Build.VERSION.SDK_INT >= 33) {
            return SVZContextCompat.registerReceiverCompat(
                    receiver,
                    filter,
                    new SVZContextCompat.ReceiverRegistrar2() {
                        @Override
                        public Intent register(BroadcastReceiver r, IntentFilter f) {
                            return SVZUnityPlayerActivity.super.registerReceiver(r, f);
                        }
                    },
                    new SVZContextCompat.ReceiverRegistrar3() {
                        @Override
                        public Intent register(BroadcastReceiver r, IntentFilter f, int flags) {
                            return SVZUnityPlayerActivity.super.registerReceiver(r, f, flags);
                        }
                    });
        }
        return super.registerReceiver(receiver, filter);
    }

    @Override
    public Intent registerReceiver(BroadcastReceiver receiver, IntentFilter filter, String broadcastPermission, Handler scheduler) {
        if (Build.VERSION.SDK_INT >= 33) {
            return SVZContextCompat.registerReceiverCompat(
                    receiver,
                    filter,
                    broadcastPermission,
                    scheduler,
                    new SVZContextCompat.ReceiverRegistrar4() {
                        @Override
                        public Intent register(BroadcastReceiver r, IntentFilter f, String p, Handler s) {
                            return SVZUnityPlayerActivity.super.registerReceiver(r, f, p, s);
                        }
                    },
                    new SVZContextCompat.ReceiverRegistrar5() {
                        @Override
                        public Intent register(BroadcastReceiver r, IntentFilter f, String p, Handler s, int flags) {
                            return SVZUnityPlayerActivity.super.registerReceiver(r, f, p, s, flags);
                        }
                    });
        }
        return super.registerReceiver(receiver, filter, broadcastPermission, scheduler);
    }

    @Override
    public boolean onGenericMotionEvent(MotionEvent event) {
        return mUnityPlayer != null ? mUnityPlayer.injectEvent(event) : super.onGenericMotionEvent(event);
    }
}
