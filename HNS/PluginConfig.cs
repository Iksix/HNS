using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CounterStrikeSharp.API.Core;

namespace HnS;

public class PluginConfig : BasePluginConfig
{
    public Dictionary<int, int> ManiacsOnPlayers { get; set; } = new Dictionary<int, int>
    {
        // До 6 игроков - 1 маньяк [4 кт, 1т]
        {2, 6}, // От 6 игроков - 2 маньяка [4 кт, 2т]
        {3, 10} // От 10 игроков - 3 маньяка [7 кт, 3т]
    };

    public List<string> RowCommandAliases { get; set; } = new () // Команды для входа в очередь
    {
        "row", "m"
    };

    public int ManiacsHp { get; set; } = 777;
    public bool RowAnnounce {get; set;} = true; // Отображать ли сообщение ВСЕМ о входе/выходе игрока в очередь
    public bool ManiacsCanTakeRow {get; set;} = false; // Могут ли маньяки вступать в очередь
}