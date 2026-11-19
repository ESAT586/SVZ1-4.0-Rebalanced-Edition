package com.svzrebalanced.compat;

import android.content.BroadcastReceiver;
import android.content.ComponentName;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.ServiceConnection;
import android.os.Handler;
import android.os.IBinder;
import android.util.Log;

import java.lang.reflect.Proxy;
import java.util.IdentityHashMap;
import java.util.Map;

/**
 * Compatibility helpers for newer Android runtimes (API 33-37+):
 * 1) Wraps dynamic Proxy ServiceConnection instances (such as Unity's JNIBridge proxy)
 *    in a concrete ServiceConnection class so Android 17's default 3-argument
 *    onServiceConnected(ComponentName, IBinder, IBinderSession) and API 26+/28+
 *    onBindingDied/onNullBinding callbacks route safely to classic 2-arg callbacks
 *    without hitting NoSuchMethodError inside native JNIBridge.invoke.
 * 2) Retries BroadcastReceiver registration with RECEIVER_NOT_EXPORTED (4) on API 33+.
 */
public final class SVZContextCompat {
    private static final String TAG = "SVZCompat";
    private static final int RECEIVER_NOT_EXPORTED_FLAG = 4;

    private static final Object LOCK = new Object();
    // IdentityHashMap avoids invoking hashCode()/equals() on dynamic JNIBridge proxies.
    private static final Map<ServiceConnection, WrapperEntry> ACTIVE_WRAPPERS =
            new IdentityHashMap<ServiceConnection, WrapperEntry>();

    public interface ServiceBinder {
        boolean bind(ServiceConnection connection);
    }

    public interface ReceiverRegistrar2 {
        Intent register(BroadcastReceiver receiver, IntentFilter filter);
    }

    public interface ReceiverRegistrar3 {
        Intent register(BroadcastReceiver receiver, IntentFilter filter, int flags);
    }

    public interface ReceiverRegistrar4 {
        Intent register(BroadcastReceiver receiver, IntentFilter filter, String permission, Handler scheduler);
    }

    public interface ReceiverRegistrar5 {
        Intent register(BroadcastReceiver receiver, IntentFilter filter, String permission, Handler scheduler, int flags);
    }

    private SVZContextCompat() {
    }

    public static boolean bindWithCompat(ServiceConnection raw, ServiceBinder binder) {
        final ServiceConnection actual = retain(raw);
        boolean bound = false;
        try {
            bound = binder.bind(actual);
            return bound;
        } finally {
            if (!bound && actual != raw) {
                release(raw);
            }
        }
    }

    public static ServiceConnection releaseForUnbind(ServiceConnection raw) {
        return release(raw);
    }

    public static Intent registerReceiverCompat(
            BroadcastReceiver receiver,
            IntentFilter filter,
            ReceiverRegistrar2 directRegistrar,
            ReceiverRegistrar3 flaggedRegistrar) {
        try {
            return directRegistrar.register(receiver, filter);
        } catch (SecurityException se) {
            return flaggedRegistrar.register(receiver, filter, RECEIVER_NOT_EXPORTED_FLAG);
        }
    }

    public static Intent registerReceiverCompat(
            BroadcastReceiver receiver,
            IntentFilter filter,
            String permission,
            Handler scheduler,
            ReceiverRegistrar4 directRegistrar,
            ReceiverRegistrar5 flaggedRegistrar) {
        try {
            return directRegistrar.register(receiver, filter, permission, scheduler);
        } catch (SecurityException se) {
            return flaggedRegistrar.register(receiver, filter, permission, scheduler, RECEIVER_NOT_EXPORTED_FLAG);
        }
    }

    private static ServiceConnection retain(ServiceConnection raw) {
        if (raw == null || !Proxy.isProxyClass(raw.getClass())) {
            return raw;
        }
        synchronized (LOCK) {
            WrapperEntry entry = ACTIVE_WRAPPERS.get(raw);
            if (entry == null) {
                entry = new WrapperEntry(new SafeServiceConnectionWrapper(raw));
                ACTIVE_WRAPPERS.put(raw, entry);
            }
            entry.refCount++;
            return entry.wrapper;
        }
    }

    private static ServiceConnection release(ServiceConnection raw) {
        if (raw == null || !Proxy.isProxyClass(raw.getClass())) {
            return raw;
        }
        synchronized (LOCK) {
            WrapperEntry entry = ACTIVE_WRAPPERS.get(raw);
            if (entry == null) {
                return raw;
            }
            entry.refCount--;
            if (entry.refCount <= 0) {
                ACTIVE_WRAPPERS.remove(raw);
            }
            return entry.wrapper;
        }
    }

    private static final class WrapperEntry {
        final SafeServiceConnectionWrapper wrapper;
        int refCount;

        WrapperEntry(SafeServiceConnectionWrapper wrapper) {
            this.wrapper = wrapper;
        }
    }

    private static final class SafeServiceConnectionWrapper implements ServiceConnection {
        private final ServiceConnection delegate;

        SafeServiceConnectionWrapper(ServiceConnection delegate) {
            this.delegate = delegate;
        }

        @Override
        public void onServiceConnected(ComponentName name, IBinder service) {
            try {
                delegate.onServiceConnected(name, service);
            } catch (Throwable t) {
                Log.w(TAG, "Suppressed proxy exception in onServiceConnected: " + t.getClass().getSimpleName());
            }
        }

        @Override
        public void onServiceDisconnected(ComponentName name) {
            try {
                delegate.onServiceDisconnected(name);
            } catch (Throwable t) {
                Log.w(TAG, "Suppressed proxy exception in onServiceDisconnected: " + t.getClass().getSimpleName());
            }
        }

        @Override
        public void onBindingDied(ComponentName name) {
            // Do not forward API 26+ callback into legacy Unity JNIBridge proxies.
        }

        @Override
        public void onNullBinding(ComponentName name) {
            // Do not forward API 28+ callback into legacy Unity JNIBridge proxies.
        }
    }
}
