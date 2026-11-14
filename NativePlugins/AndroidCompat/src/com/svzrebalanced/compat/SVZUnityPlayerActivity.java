package com.svzrebalanced.compat;

import android.app.Activity;
import android.content.Intent;
import android.content.res.Configuration;
import android.os.Bundle;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.Window;

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
    public boolean onGenericMotionEvent(MotionEvent event) {
        return mUnityPlayer != null ? mUnityPlayer.injectEvent(event) : super.onGenericMotionEvent(event);
    }
}
