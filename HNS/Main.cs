using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using HNS;
using HnSApi;
using IksAdminApi;

namespace HnS;

public class Main : AdminModule, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "HNS";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "iks__";

    public PluginConfig Config {get; set;}

    private readonly PluginCapability<IHNSApi> _hnsCapability = new("hns:api");
    public static HNSApi? HnsApi;

    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
    }
    public override void Load(bool hotReload)
    {
        HnsApi = new HNSApi();
        Capabilities.RegisterPluginCapability(_hnsCapability, () => HnsApi);
        AddCommandListener("jointeam", OnJoinTeam, HookMode.Pre);
        AddCommandListener("kill", OnKillCommand, HookMode.Pre);
        foreach (var cmd in Config.RowCommandAliases)
        {
            AddCommand(cmd, "join/exit row", OnRowCommand);
        }
    }

    public override void Ready()
    {
        Api.MenuOpenPre += OnMenuOpen;
    }

    public override void InitializeCommands()
    {
        Api.RegisterPermission("hns.edit_row", "q");
        Api!.AddNewCommand(
            "edit_row",
            "Edit row",
            "hns.edit_row",
            "css_edit_row",
            OnEditRowCommand
        );
    }

    private HookResult OnMenuOpen(CCSPlayerController caller, IDynamicMenu menu, IMenu gameMenu)
    {
        if (menu.Id != Config.EditRowLocation) return HookResult.Continue;
        if (!caller.HasPermissions("hns.edit_row")) return HookResult.Continue;
        menu.AddMenuOption("edit_row", Localizer["MENUOPTION.EditRow"], (_, _) =>
        {
            OpenEditRowMenu(caller);
        });
        return HookResult.Continue;
    }

    private void OnEditRowCommand(CCSPlayerController caller, List<string> args, CommandInfo info)
    {
        OpenEditRowMenu(caller);
    }

    private void OpenEditRowMenu(CCSPlayerController caller)
    {
        var menu = Api!.CreateMenu("hns.edit_row", Localizer["MENUTITLE.EditRow"]);
        menu.AddMenuOption("AddToRow", Localizer["MENUOPTION.AddToRow"], (_, _) => {
            OpenAddToRowMenu(caller, menu);
        });
        menu.AddMenuOption("RemoveFromRow", Localizer["MENUOPTION.RemoveFromRow"], (_, _) => {
            OpenRemoveFromRowMenu(caller, menu);
        });
        menu.Open(caller);
    }

    private void OpenRemoveFromRowMenu(CCSPlayerController caller, IDynamicMenu backMenu)
    {
        var menu = Api!.CreateMenu("hns.remove_from_menu", Localizer["MENUTITLE.AddToRow"], backMenu: backMenu);
        var playersInRow = HnsApi!.RowToManiacs;
        
        foreach (var player in playersInRow.ToArray())
        {
            menu.AddMenuOption(player.GetSteamId(), player.PlayerName, (_, _) => {
                if (!playersInRow.Contains(player)) return;
                playersInRow.Remove(player);
                player.Print(Localizer["NOTIFY.AdminDeleteYouFromRow"].Value.Replace("{name}", caller.PlayerName), Localizer["tag"]);
                caller.Print(Localizer["NOTIFY.PlayerRemovedFromRow"], Localizer["tag"]);
                OpenEditRowMenu(caller);
            });
        }
        menu.Open(caller);
    }

    private void OpenAddToRowMenu(CCSPlayerController caller, IDynamicMenu backMenu)
    {
        var menu = Api!.CreateMenu("hns.add_to_row", Localizer["MENUTITLE.AddToRow"], backMenu: backMenu);
        var activePlayers = HnsApi!.GetActivePlayers();
        
        foreach (var player in activePlayers)
        {
            if (HnsApi.RowToManiacs.Contains(player)) continue;
            menu.AddMenuOption(player.GetSteamId(), player.PlayerName, (_, _) => {
                if (HnsApi.RowToManiacs.Contains(player)) return;
                HnsApi.RowToManiacs.Add(player);
                player.Print(Localizer["NOTIFY.AdminAddYouToRow"].Value.Replace("{name}", caller.PlayerName), Localizer["tag"]);
                caller.Print(Localizer["NOTIFY.PlayerAddedToRow"], Localizer["tag"]);
                OpenEditRowMenu(caller);
            });
        }
        menu.Open(caller);
    }

    private HookResult OnKillCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        return HookResult.Stop;
    }

    private HookResult OnJoinTeam(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null) return HookResult.Continue;
        var team = commandInfo.GetArg(1);
        if (player.TeamNum == 2)
            return HookResult.Stop;
        if (player.TeamNum == 3 && team == "2")
            return HookResult.Stop;
        if (team == "2")
            return HookResult.Stop;
        if (player.TeamNum == 3 && team == "1")
        {
            HnsApi!.RowToManiacs.Remove(player);
            player.Print(Localizer["NOTIFY.DeleteFromRowByChangeTeam"], Localizer["tag"]);
        }
        return HookResult.Continue;
    }

    public override void Unload(bool hotReload)
    {
        RemoveCommandListener("jointeam", OnJoinTeam, HookMode.Pre);
        RemoveCommandListener("kill", OnKillCommand, HookMode.Pre);
        Api!.MenuOpenPre -= OnMenuOpen;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        var winner = @event.Winner;
        if (HnsApi!.Maniacs.Count > 0)
        {
            switch (winner)
            {
                case 3:
                    HnsApi!.EOnSurvivorsWin();
                    break;
                case 2:
                    HnsApi!.EOnManiacsWin();
                    break;
            }
        }
        var needManiacsCount = 1;
        var activePlayers = HnsApi!.GetActivePlayers();
        var lastManiacs = HnsApi!.Maniacs.ToArray();
        HnsApi.Maniacs.Clear();
        foreach (var setting in Config.ManiacsOnPlayers)
        {
            if (activePlayers.Count >= setting.Value)
            {
                needManiacsCount = setting.Key;
            }
        }
        var playersInRow = HnsApi.RowToManiacs;
        var nextManiacs = new List<CCSPlayerController>();

        foreach (var player in playersInRow.ToArray())
        {
            if (nextManiacs.Count == needManiacsCount) break;
            nextManiacs.Add(player);
            playersInRow.Remove(player);
        }
        foreach (var player in lastManiacs)
        {
            activePlayers.Remove(player);
        }
        Random.Shared.Shuffle(CollectionsMarshal.AsSpan(activePlayers));
        foreach (var player in activePlayers)
        {
            if (nextManiacs.Count == needManiacsCount) break;
            nextManiacs.Add(player);
        }
        Random.Shared.Shuffle(CollectionsMarshal.AsSpan(lastManiacs.ToList()));
        foreach (var player in lastManiacs)
        {
            if (nextManiacs.Count == needManiacsCount) break;
            if (player == null) continue;
            nextManiacs.Add(player);
        }

        HnsApi.Maniacs = nextManiacs;
        SetManiacsAndSurvivors();

        var itemsString = "";
        for (int i = 0; i < HnsApi.Maniacs.Count; i++)
        {
            var player = HnsApi.Maniacs[i];
            itemsString += Localizer["rowItem"].Value
            .Replace("{i}", (i+1).ToString())
            .Replace("{name}", player.PlayerName)
            .Replace("{steamId}", player.SteamID.ToString())
            .Replace("{uid}", player.UserId.ToString());
        }
        var nextManiacsMessage = Localizer["nextManiacs"].Value.Replace("{items}", itemsString);
        AdminUtils.PrintToServer(nextManiacsMessage, Localizer["tag"]);

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (HnsApi!.Maniacs.Count == 0 && HnsApi.GetActivePlayers().Count > 0)
        {
            XHelper.RulesProxy()!.GameRules!.TerminateRound(1f, RoundEndReason.RoundDraw);
        }
        return HookResult.Continue;
    }
    

    public void OnRowCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null) return;
        if ((caller.TeamNum == 2 && !Config.ManiacsCanTakeRow) || caller.TeamNum == 1)
        {
            caller.Print(Localizer["ERROR.YouCantTakeRowFromThisTeam"], Localizer["tag"]);
            return;
        }

        if (!HnsApi!.RowToManiacs.Contains(caller))
        {
            HnsApi.RowToManiacs.Add(caller);
            caller.Print(Localizer["NOTIFY.EnterRow"].Value.Replace("{num}", HnsApi.RowToManiacs.Count.ToString()), Localizer["tag"]);
            if (Config.RowAnnounce)
                AdminUtils.PrintToServer(Localizer["SERVER.EnterRow"].Value.Replace("{name}", caller.PlayerName), Localizer["tag"]);
        } else {
            HnsApi.RowToManiacs.Remove(caller);
            caller.Print(Localizer["NOTIFY.ExitRow"], Localizer["tag"]);
            if (Config.RowAnnounce)
                AdminUtils.PrintToServer(Localizer["SERVER.ExitRow"].Value.Replace("{name}", caller.PlayerName), Localizer["tag"]);
        } 
    }

    [ConsoleCommand("css_row_list")]
    public void OnRowListCommand(CCSPlayerController caller, CommandInfo info)
    {
        var itemsString = "";
        for (int i = 0; i < HnsApi!.RowToManiacs.Count; i++)
        {
            var player = HnsApi.RowToManiacs[i];
            itemsString += Localizer["rowItem"].Value
            .Replace("{i}", (i+1).ToString())
            .Replace("{name}", player.PlayerName)
            .Replace("{steamId}", player.SteamID.ToString())
            .Replace("{uid}", player.UserId.ToString());
        }
        caller.Print(Localizer["rowList"].Value.Replace("{items}", itemsString), Localizer["tag"]);
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null) return HookResult.Continue;
        if (!player.PawnIsAlive) return HookResult.Continue;

        if (player.TeamNum == 2 && !HnsApi!.Maniacs.Contains(player))
        {
            player.SwitchTeam(CsTeam.CounterTerrorist);
            Server.NextFrame(player.Respawn);
            return HookResult.Continue;
        }

        if (player.TeamNum != 2) return HookResult.Continue;
        Server.NextFrame(() => {
            var pawn = player.PlayerPawn;
            pawn.Value!.MaxHealth = Config.ManiacsHp;
            pawn.Value!.Health = Config.ManiacsHp;
            Utilities.SetStateChanged(pawn.Value!, "CBaseEntity", "m_iHealth");
        });
        return HookResult.Continue;
    }
    

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null) return HookResult.Continue;
        HnsApi!.Maniacs.Remove(player);
        HnsApi!.RowToManiacs.Remove(player);
        if (HnsApi!.Maniacs.Count == 0)
        {
            XHelper.RulesProxy()!.GameRules!.TerminateRound(1f, RoundEndReason.CTsWin);
        }
        return HookResult.Continue;
    }
    
    [GameEventHandler]
    public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null) return HookResult.Continue;
        if (!HnsApi!.Maniacs.Contains(player)) return HookResult.Continue;
        var playerPawn = player.PlayerPawn;
        var damage = @event.DmgHealth;

        playerPawn.Value!.Health += damage;
        playerPawn.Value!.ArmorValue += @event.DmgArmor;
        
        Utilities.SetStateChanged(playerPawn.Value, "CBaseEntity", "m_iHealth");
        Utilities.SetStateChanged(playerPawn.Value, "CBasePlayerPawn", "m_pItemServices");
        Utilities.SetStateChanged(playerPawn.Value, "CCSPlayerPawn", "m_ArmorValue");
        return HookResult.Continue;
    }
    

    private void SetManiacsAndSurvivors()
    {
        foreach (var player in HnsApi!.Maniacs)
        {
            player.SwitchTeam(CsTeam.Terrorist);
            if (!player.PawnIsAlive) continue;
            var pawn = player.PlayerPawn;
            pawn.Value!.MaxHealth = Config.ManiacsHp;
            pawn.Value!.Health = Config.ManiacsHp;
            Utilities.SetStateChanged(pawn.Value!, "CBaseEntity", "m_iHealth");
        }
        foreach (var player in HnsApi.GetActivePlayers())
        {
            if (HnsApi.Maniacs.Contains(player)) continue;
            player.SwitchTeam(CsTeam.CounterTerrorist);
        }
        HnsApi.EOnNextManiacsSetted();
    }
}

public class HNSApi : IHNSApi
{
    public List<CCSPlayerController> Maniacs {get; set;} = new();
    public List<CCSPlayerController> RowToManiacs {get; set;} = new();

    public event Action? OnNextManiacsSetted;
    public event Action? OnManiacsWin;
    public event Action? OnSurvivorsWin;

    public List<CCSPlayerController> GetActivePlayers()
    {
        return Utilities.GetPlayers().Where(x => x.TeamNum is 3 or 2 && x is {IsBot: false, IsValid: true, Connected: PlayerConnectedState.PlayerConnected}).ToList();
    }

    public List<CCSPlayerController> GetSurvivors()
    {
        var players = GetActivePlayers();
        foreach (var player in players.ToArray())
        {
            if (Maniacs.Any(x => x.SteamID == player.SteamID)) players.Remove(player);
            if (player.TeamNum == 2) players.Remove(player);
        }
        return players;
    }

    public void EOnNextManiacsSetted()
    {
        OnNextManiacsSetted?.Invoke();
    }
    public void EOnManiacsWin()
    {
        OnManiacsWin?.Invoke();
    }
    public void EOnSurvivorsWin()
    {
        OnSurvivorsWin?.Invoke();
    }
}
