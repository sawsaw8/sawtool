using Dalamud.Common;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using sawtool.Gathering;
using Fate = Dalamud.Game.ClientState.Fates.Fate;

namespace sawtool;

class RepoMigrateWindow : Window
{
    public static string OldURL = "https://raw.githubusercontent.com/awgil/ffxiv_plugin_distribution/master/pluginmaster.json";
    public static string NewURL = "https://puni.sh/api/repository/veyn";

    public RepoMigrateWindow() : base("Warning! Plugin home repository was changed")
    {
        IsOpen = true;
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("...");
    }
}

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "sawtool";

    public DalamudPluginInterface Dalamud { get; init; }

    public WindowSystem WindowSystem = new("sawtool");
    private GatherWindow _wndGather;

    [PluginService] internal static IFateTable fates { get; private set; } = null!;

    public unsafe Plugin(DalamudPluginInterface dalamud)
    {
        var dir = dalamud.ConfigDirectory;
        if (!dir.Exists)
            dir.Create();
        var dalamudRoot = dalamud.GetType().Assembly.
                GetType("Dalamud.Service`1", true)!.MakeGenericType(dalamud.GetType().Assembly.GetType("Dalamud.Dalamud", true)!).
                GetMethod("Get")!.Invoke(null, BindingFlags.Default, null, Array.Empty<object>(), null);
        var dalamudStartInfo = dalamudRoot.GetFoP<DalamudStartInfo>("StartInfo");
        FFXIVClientStructs.Interop.Resolver.GetInstance.SetupSearchSpace(0, new(Path.Combine(dalamud.ConfigDirectory.FullName, $"{dalamudStartInfo.GameVersion}_cs.json")));
        FFXIVClientStructs.Interop.Resolver.GetInstance.Resolve();

        ECommonsMain.Init(dalamud, this);

        dalamud.Create<Service>();
        dalamud.UiBuilder.Draw += WindowSystem.Draw;

        Service.Config.Initialize();
        if (dalamud.ConfigFile.Exists)
            Service.Config.LoadFromFile(dalamud.ConfigFile);
        Service.Config.Modified += (_, _) => Service.Config.SaveToFile(dalamud.ConfigFile);

        Dalamud = dalamud;

        _wndGather = new GatherWindow();

        if (dalamud.SourceRepository == RepoMigrateWindow.OldURL)
        {
            WindowSystem.AddWindow(new RepoMigrateWindow());
        }
        else
        {
            WindowSystem.AddWindow(_wndGather);
            Service.CommandManager.AddHandler("/sawtool", new CommandInfo(OnCommand) { HelpMessage = "Show plugin gathering UI" });
            Dalamud.UiBuilder.OpenConfigUi += () => _wndGather.IsOpen = true;
        }
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        Service.CommandManager.RemoveHandler("/sawtool");
        _wndGather.Dispose();
    }

    private void OnCommand(string command, string arguments)
    {
        Service.Log.Debug($"cmd: '{command}', args: '{arguments}'");
        if (arguments.Length == 0)
        {
            _wndGather.IsOpen ^= true;
        }
        else
        {
            var args = arguments.Split(' ');
            switch (args[0])
            {
                case "moveto":
                    if (args.Length > 3)
                        MoveToCommand(args, false);
                    break;
                case "movedir":
                    if (args.Length > 3)
                        MoveToCommand(args, true);
                    break;
                case "moveflag":
                    if (args.Length > 3)
                        MoveToFlagCommand(args, false);
                    break;
                 case "moveflagnav":
                    MoveToFlagByNavmeshCommand(args, false);
                    break;               
                case "movefate":
                    if (args.Length > 3)
                        MoveToFateCommand(args, false);
                    break;
                case "movequest":
                    if (args.Length > 3)
                        MoveToQuestCommand(args, false);
                    break;
                case "stop":
                    _wndGather.Exec.Finish();
                    {
                        Chat chat = new();
                        chat.SendMessage("/vnavmesh stop");

                    }
                    break;
                case "pause":
                    _wndGather.Exec.Paused = true;
                    break;
                case "resume":
                    _wndGather.Exec.Paused = false;
                    break;
                case "exec":
                    ExecuteCommand(string.Join(" ", args.Skip(1)), false);
                    break;
                case "execonce":
                    ExecuteCommand(string.Join(" ", args.Skip(1)), true);
                    break;
            }
        }
    }

    private void MoveToCommand(string[] args, bool relativeToPlayer)
    {
        var originActor = relativeToPlayer ? Service.ClientState.LocalPlayer : null;
        var origin = originActor?.Position ?? new();
        var offset = new Vector3(float.Parse(args[1], new CultureInfo("en-US")), float.Parse(args[2], new CultureInfo("en-US")), float.Parse(args[3], new CultureInfo("en-US")));
        var route = new GatherRouteDB.Route { Name = "Temporary", Waypoints = new() };
        route.Waypoints.Add(new() { Position = origin + offset, Radius = 0.5f, InteractWithName = "", InteractWithOID = 0 });
        _wndGather.Exec.Start(route, 0, false, false);
    }

    private void ExecuteCommand(string name, bool once)
    {
        var route = _wndGather.RouteDB.Routes.Find(r => r.Name == name);
        if (route != null)
            _wndGather.Exec.Start(route, 0, true, !once);
    }
    private unsafe void MoveToFlagCommand(string[] args, bool relativeToPlayer)
    {
        var instance = AgentMap.Instance();
        if (instance == null)
        {
            Service.Log.Debug($"instance == null");
            return;
        }
        if (instance->IsFlagMarkerSet == 0)
        {
            Service.Log.Debug($"instance->IsFlagMarkerSet == 0");
            return;
        }
        
        var marker = instance->FlagMapMarker;
        var offset = new Vector3(marker.XFloat, Service.ClientState.LocalPlayer.Position.Y + 30, marker.YFloat);
        Service.Log.Debug($"offset = {marker.XFloat}, {Service.ClientState.LocalPlayer.Position.Y + 30}, {marker.YFloat}");
        var originActor = relativeToPlayer ? Service.ClientState.LocalPlayer : null;
        var origin = originActor?.Position ?? new();
        //var offset = new Vector3(float.Parse(args[1]), float.Parse(args[2]), float.Parse(args[3]));
        var route = new GatherRouteDB.Route { Name = "Temporary", Waypoints = new() };
        route.Waypoints.Add(new() { Position = origin + offset, Radius = 0.5f, InteractWithName = "", Movement = GatherRouteDB.Movement.MountFly , InteractWithOID = 0 }) ;
        _wndGather.Exec.Start(route, 0, false, false);
    }

    private unsafe void MoveToFlagByNavmeshCommand(string[] args, bool relativeToPlayer)
    {
        var instance = AgentMap.Instance();
        if (instance == null)
        {
            Service.Log.Debug($"instance == null");
            return;
        }
        if (instance->IsFlagMarkerSet == 0)
        {
            Service.Log.Debug($"instance->IsFlagMarkerSet == 0");
            return;
        }

        var marker = instance->FlagMapMarker;
        Service.Log.Debug($"offset = {marker.XFloat}, {Service.ClientState.LocalPlayer.Position.Y}, {marker.YFloat}");
        var command = $"/vnavmesh flyto {marker.XFloat} {Service.ClientState.LocalPlayer.Position.Y}, {marker.YFloat}";
        Chat chat = new();
        chat.SendMessage(command);
    }
    private unsafe void MoveToFateCommand(string[] args, bool relativeToPlayer)
    {
        var instance = AgentMap.Instance();
        if (instance == null)
        {
            Service.Log.Debug($"instance == null");
            return;
        }

        if (Plugin.fates.Length == 0)
        {
            Service.Log.Debug($"Plugin.fates.Length == 0");
            return;
        }

        List<Fate> fates = new(Plugin.fates.Length);
        
        foreach (Fate? fate in Plugin.fates)
        {
            if (fate is not null)
                fates.Add(fate);
        }

        var originActor = Service.ClientState.LocalPlayer;
        var origin = originActor?.Position ?? new();
        Vector3 targetPos = origin;
        float mindist = float.MaxValue;
        foreach (Fate fate in fates)
        {
            var toWaypoint = fate.Position - origin;
            float dist = toWaypoint.LengthSquared();
            if(dist < mindist)
            {
                mindist = dist;
                targetPos = fate.Position;
            }
        }

        var route = new GatherRouteDB.Route { Name = "Temporary", Waypoints = new() };
        route.Waypoints.Add(new() { Position = targetPos, Radius = 0.5f, InteractWithName = "", Movement = GatherRouteDB.Movement.MountFly, InteractWithOID = 0 });
        _wndGather.Exec.Start(route, 0, false, false);
    }
    private unsafe void MoveToQuestCommand(string[] args, bool relativeToPlayer)
    {
        var instance = AgentMap.Instance();
        if (instance == null)
        {
            Service.Log.Debug($"instance == null");
            return;
        }

        if (Plugin.fates.Length == 0)
        {
            Service.Log.Debug($"Plugin.fates.Length == 0");
            return;
        }

        List<Fate> fates = new(Plugin.fates.Length);

        foreach (Fate? fate in Plugin.fates)
        {
            if (fate is not null)
                fates.Add(fate);
        }

        var originActor = Service.ClientState.LocalPlayer;
        var origin = originActor?.Position ?? new();
        Vector3 targetPos = origin;
        float mindist = float.MaxValue;
        foreach (Fate fate in fates)
        {
            var toWaypoint = fate.Position - origin;
            float dist = toWaypoint.LengthSquared();
            if (dist < mindist)
            {
                mindist = dist;
                targetPos = fate.Position;
            }
        }

        var route = new GatherRouteDB.Route { Name = "Temporary", Waypoints = new() };
        route.Waypoints.Add(new() { Position = targetPos, Radius = 0.5f, InteractWithName = "", Movement = GatherRouteDB.Movement.MountFly, InteractWithOID = 0 });
        _wndGather.Exec.Start(route, 0, false, false);
    }
}