global using ECommons.DalamudServices;
using Dalamud.Game.Command;
using Dalamud.Interface.Internal.Notifications;
using ECommons.Logging;
using Dalamud.Plugin;
using ECommons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace NotificationMaster
{
    public class NotificationMaster : IDalamudPlugin
    {
        internal bool IsDisposed = false;
        internal ChatMessage chatMessage = null;

        internal long PauseUntil = 0;
        internal static NotificationMaster P;

        public string Name => "NotificationMaster";

        public NotificationMaster(DalamudPluginInterface pluginInterface)
        {
            P = this;
            ECommonsMain.Init(pluginInterface, this);
            ChatMessage.Setup(true, this);

        }

        public void Dispose()
        {
            ChatMessage.Setup(false, this);
            IsDisposed = true;
            ECommonsMain.Dispose();
            P = null;
        }
    }
}
