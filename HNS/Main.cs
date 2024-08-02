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

public class Main : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "HNS";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "iks__";

    public PluginConfig Config {get; set;}

    private readonly PluginCapability<IHNSApi> _hnsCapability = new("hns:api");
    public static HNSApi? Api;

    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
    }
    public override void Load(bool hotReload)
    {
        Api = new HNSApi();
        Capabilities.RegisterPluginCapability(_hnsCapability, () => Api);
        AddCommandListener("jointeam", OnJoinTeam, HookMode.Pre);
        AddCommandListener("kill", OnKillCommand, HookMode.Pre);
        foreach (var cmd in Config.RowCommandAliases)
        {
            AddCommand(cmd, "join/exit row", OnRowCommand);
        }
    }

    private readonly PluginCapability<IIksAdminApi> _adminCapability = new("iksadmin:core");
    public static IIksAdminApi? AdminApi;
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        AdminApi = _adminCapability.Get();
        AdminApi!.AddNewCommand(
            "edit_row",
            "Edit row",
            "css_edit_row",
            0,
            "edit_row",
            "q",
            CommandUsage.CLIENT_ONLY,
            OnEditRowCommand
        );
        AdminApi.OnMenuOpen += OnMenuOpen;
    }

    private void OnMenuOpen(string key, IMenu menu, CCSPlayerController caller)
    {
        if (key != Config.EditRowLocation) return;
        if (!AdminApi!.HasPermissions(caller.GetSteamId(), "edit_row", "q")) return;
        menu.AddMenuOption(Localizer["MENUOPTION.EditRow"], (_, _) => {
            OpenEditRowMenu(caller);
        });
    }

    private void OnEditRowCommand(CCSPlayerController caller, Admin? admin, List<string> args, CommandInfo info)
    {
        OpenEditRowMenu(caller);
    }

    private void OpenEditRowMenu(CCSPlayerController caller)
    {
        var menu = AdminApi!.CreateMenu(EditRowMenu);
        menu.Open(caller, Localizer["MENUTITLE.EditRow"]);
    }

    private void EditRowMenu(CCSPlayerController caller, Admin? admin, IMenu menu)
    {
        menu.AddMenuOption(Localizer["MENUOPTION.AddToRow"], (_, _) => {
            OpenAddToRowMenu(caller, menu);
        });
        menu.AddMenuOption(Localizer["MENUOPTION.RemoveFromRow"], (_, _) => {
            OpenRemoveFromRowMenu(caller, menu);
        });
    }

    private void OpenRemoveFromRowMenu(CCSPlayerController caller, IMenu backMenu)
    {
        var menu = AdminApi!.CreateMenu(RemoveFromRowMenu);
        menu.Open(caller, Localizer["MENUTITLE.AddToRow"], backMenu);
    }

    private void RemoveFromRowMenu(CCSPlayerController caller, Admin? admin, IMenu menu)
    {
        var playersInRow = Api!.RowToManiacs;
        
        foreach (var player in playersInRow.ToArray())
        {
            menu.AddMenuOption(player.PlayerName, (_, _) => {
                if (!playersInRow.Contains(player)) return;
                playersInRow.Remove(player);
                AdminApi!.SendMessageToPlayer(player, Localizer["NOTIFY.AdminDeleteYouFromRow"].Value.Replace("{name}", caller.PlayerName), Localizer["tag"]);
                AdminApi!.SendMessageToPlayer(caller, Localizer["NOTIFY.PlayerRemovedFromRow"], Localizer["tag"]);
                OpenEditRowMenu(caller);
            });
        }
    }

    private void OpenAddToRowMenu(CCSPlayerController caller, IMenu backMenu)
    {
        var menu = AdminApi!.CreateMenu(AddToRowMenu);
        menu.Open(caller, Localizer["MENUTITLE.AddToRow"], backMenu);
    }

    private void AddToRowMenu(CCSPlayerController caller, Admin? admin, IMenu menu)
    {
        var activePlayers = Api!.GetActivePlayers();
        
        foreach (var player in activePlayers)
        {
            if (Api.RowToManiacs.Contains(player)) continue;
            menu.AddMenuOption(player.PlayerName, (_, _) => {
                if (activePlayers.Contains(player)) return;
                activePlayers.Add(player);
                AdminApi!.SendMessageToPlayer(player, Localizer["NOTIFY.AdminAddYouToRow"].Value.Replace("{name}", caller.PlayerName), Localizer["tag"]);
                AdminApi!.SendMessageToPlayer(caller, Localizer["NOTIFY.PlayerAddedToRow"], Localizer["tag"]);
                OpenEditRowMenu(caller);
            });
        }
    }
    private HookResult OnKillCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        return HookResult.Stop;
    }

    private HookResult OnJoinTeam(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null) return HookResult.Continue;
        var team = commandInfo.GetArg(1);
        if (player.TeamNum == 3 && team == "2")
            return HookResult.Stop;
        if (team == "2")
            return HookResult.Stop;
        if (player.TeamNum == 3 && team == "1")
        {
            Api!.RowToManiacs.Remove(player);
            AdminApi!.SendMessageToPlayer(player, Localizer["NOTIFY.DeleteFromRowByChangeTeam"], Localizer["tag"]);
        }
        return HookResult.Continue;
    }

    public override void Unload(bool hotReload)
    {
        RemoveCommandListener("jointeam", OnJoinTeam, HookMode.Pre);
        RemoveCommandListener("kill", OnKillCommand, HookMode.Pre);
        AdminApi!.OnMenuOpen -= OnMenuOpen;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        var winner = @event.Winner;
        if (Api!.Maniacs.Count > 0)
        {
            switch (winner)
            {
                case 3:
                    Api!.EOnSurvivorsWin();
                    break;
                case 2:
                    Api!.EOnManiacsWin();
                    break;
            }
        }
        var needManiacsCount = 1;
        var activePlayers = Api!.GetActivePlayers();
        var lastManiacs = Api!.Maniacs.ToArray();
        Api.Maniacs.Clear();
        foreach (var setting in Config.ManiacsOnPlayers)
        {
            if (activePlayers.Count >= setting.Value)
            {
                needManiacsCount = setting.Key;
            }
        }
        var playersInRow = Api.RowToManiacs;
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

        Api.Maniacs = nextManiacs;
        SetManiacsAndSurvivors();

        var itemsString = "";
        for (int i = 0; i < Api.Maniacs.Count; i++)
        {
            var player = Api.Maniacs[i];
            itemsString += Localizer["rowItem"].Value
            .Replace("{i}", (i+1).ToString())
            .Replace("{name}", player.PlayerName)
            .Replace("{steamId}", player.SteamID.ToString())
            .Replace("{uid}", player.UserId.ToString());
        }
        var nextManiacsMessage = Localizer["nextManiacs"].Value.Replace("{items}", itemsString);
        AdminApi!.SendMessageToAll(nextManiacsMessage, Localizer["tag"]);

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (Api!.Maniacs.Count == 0)
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
            AdminApi!.SendMessageToPlayer(caller, Localizer["ERROR.YouCantTakeRowFromThisTeam"], Localizer["tag"]);
            return;
        }

        if (!Api!.RowToManiacs.Contains(caller))
        {
            Api.RowToManiacs.Add(caller);
            AdminApi!.SendMessageToPlayer(caller, Localizer["NOTIFY.EnterRow"].Value.Replace("{num}", Api.RowToManiacs.Count.ToString()), Localizer["tag"]);
            if (Config.RowAnnounce)
                AdminApi!.SendMessageToAll(Localizer["SERVER.EnterRow"].Value.Replace("{name}", caller.PlayerName), Localizer["tag"]);
        } else {
            Api.RowToManiacs.Remove(caller);
            AdminApi!.SendMessageToPlayer(caller, Localizer["NOTIFY.ExitRow"], Localizer["tag"]);
            if (Config.RowAnnounce)
                AdminApi!.SendMessageToAll(Localizer["SERVER.ExitRow"].Value.Replace("{name}", caller.PlayerName), Localizer["tag"]);
        } 
    }

    [ConsoleCommand("css_row_list")]
    public void OnRowListCommand(CCSPlayerController caller, CommandInfo info)
    {
        var itemsString = "";
        for (int i = 0; i < Api!.Maniacs.Count; i++)
        {
            var player = Api.Maniacs[i];
            itemsString += Localizer["rowItem"].Value
            .Replace("{i}", (i+1).ToString())
            .Replace("{name}", player.PlayerName)
            .Replace("{steamId}", player.SteamID.ToString())
            .Replace("{uid}", player.UserId.ToString());
        }
        AdminApi!.SendMessageToPlayer(caller, Localizer["rowList"].Value.Replace("{items}", itemsString), Localizer["tag"]);
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null) return HookResult.Continue;
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
        Api!.Maniacs.Remove(player);
        Api!.RowToManiacs.Remove(player);
        if (Api!.Maniacs.Count == 0)
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
        if (!Api!.Maniacs.Contains(player)) return HookResult.Continue;
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
        foreach (var player in Api!.Maniacs)
        {
            player.SwitchTeam(CsTeam.Terrorist);
            if (!player.PawnIsAlive) return;
            var pawn = player.PlayerPawn;
            pawn.Value!.MaxHealth = Config.ManiacsHp;
            pawn.Value!.Health = Config.ManiacsHp;
            Utilities.SetStateChanged(pawn.Value!, "CBaseEntity", "m_iHealth");
        }
        foreach (var player in Api.GetActivePlayers())
        {
            if (Api.Maniacs.Contains(player)) continue;
            player.SwitchTeam(CsTeam.CounterTerrorist);
        }
        Api.EOnNextManiacsSetted();
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
