using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using System;
using System.IO;

namespace PhiInfo.Android
{
    public class AndroidHttpServer(Context context, string apkPath, Stream cldbStream) : HttpServer(apkPath, cldbStream)
    {
        private const string TAG = "PhiInfoHttpServer";
        private const string TARGET_PKG = "com.PigeonGames.Phigros";
        private readonly Context _context = context;

        protected override void Log(string msg)
        {
            global::Android.Util.Log.Info(TAG, msg);
        }

        protected override string LoadVersionCode()
        {
            var pkgInfo = _context.PackageManager.GetPackageInfo(TARGET_PKG, 0);
            return pkgInfo.LongVersionCode.ToString();
        }
    }

    [Service(Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
    public class HttpServerService : Service
    {
        private const string TAG = "PhiInfoHttpService";
        private const int NOTIFICATION_ID = 41669;
        private const string CHANNEL_ID = "phiinfo_channel";
        private const string TARGET_PKG = "com.PigeonGames.Phigros";

        private AndroidHttpServer _server;

        public override void OnCreate()
        {
            base.OnCreate();
            CreateNotificationChannel();
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            var notification = new Notification.Builder(this, CHANNEL_ID)
                .SetContentTitle("PhiInfo Server")
                .SetContentText("服务正在监听 http://127.0.0.1:41669/")
                .SetSmallIcon(global::Android.Resource.Drawable.SymDefAppIcon)
                .SetOngoing(true)
                .Build();

            StartForeground(NOTIFICATION_ID, notification, ForegroundService.TypeDataSync);

            StartServer();

            return StartCommandResult.Sticky;
        }

        private void StartServer()
        {
            try
            {
                if (_server != null) return;

                var appInfo = PackageManager.GetApplicationInfo(TARGET_PKG, 0);
                string apkPath = appInfo.SourceDir;

                Stream cldbStream = Assets.Open("classdata.tpk");

                _server = new AndroidHttpServer(this, apkPath, cldbStream);

                _ = _server.Start(41669, "127.0.0.1");

                Log.Info(TAG, "HTTP Server started successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Failed to start HTTP server: {ex.Message}");
            }
        }

        public override void OnDestroy()
        {
            _server?.Dispose();
            _server = null;
            Log.Info(TAG, "HTTP Server stopped and disposed.");
            base.OnDestroy();
        }

        public override IBinder OnBind(Intent intent) => null;

        private void CreateNotificationChannel()
        {
            var channel = new NotificationChannel(CHANNEL_ID, "HTTP Server 状态", NotificationImportance.Low)
            {
                Description = "显示 PhiInfo 本地 HTTP 服务器的运行状态"
            };
            var manager = (NotificationManager)GetSystemService(NotificationService);
            manager.CreateNotificationChannel(channel);
        }
    }
}