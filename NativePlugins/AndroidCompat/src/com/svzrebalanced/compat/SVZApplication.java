package com.svzrebalanced.compat;

import android.app.Application;
import android.content.BroadcastReceiver;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.ServiceConnection;
import android.os.Build;
import android.os.Handler;

import java.util.concurrent.Executor;

/**
 * Application subclass ensuring getApplicationContext() also routes bindService/unbindService
 * and API 33+ BroadcastReceiver registrations through SVZContextCompat.
 */
public class SVZApplication extends Application {
    @Override
    public boolean bindService(final Intent service, ServiceConnection conn, final int flags) {
        return SVZContextCompat.bindWithCompat(conn, new SVZContextCompat.ServiceBinder() {
            @Override
            public boolean bind(ServiceConnection actual) {
                return SVZApplication.super.bindService(service, actual, flags);
            }
        });
    }

    @Override
    public boolean bindService(final Intent service, final int flags, final Executor executor, ServiceConnection conn) {
        return SVZContextCompat.bindWithCompat(conn, new SVZContextCompat.ServiceBinder() {
            @Override
            public boolean bind(ServiceConnection actual) {
                return SVZApplication.super.bindService(service, flags, executor, actual);
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
                            return SVZApplication.super.registerReceiver(r, f);
                        }
                    },
                    new SVZContextCompat.ReceiverRegistrar3() {
                        @Override
                        public Intent register(BroadcastReceiver r, IntentFilter f, int flags) {
                            return SVZApplication.super.registerReceiver(r, f, flags);
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
                            return SVZApplication.super.registerReceiver(r, f, p, s);
                        }
                    },
                    new SVZContextCompat.ReceiverRegistrar5() {
                        @Override
                        public Intent register(BroadcastReceiver r, IntentFilter f, String p, Handler s, int flags) {
                            return SVZApplication.super.registerReceiver(r, f, p, s, flags);
                        }
                    });
        }
        return super.registerReceiver(receiver, filter, broadcastPermission, scheduler);
    }
}
