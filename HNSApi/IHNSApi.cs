using CounterStrikeSharp.API.Core;
namespace HnSApi;

public interface IHNSApi
{
    public List<CCSPlayerController> RowToManiacs {get; set;}
    public List<CCSPlayerController> Maniacs {get; set;} // Текущие маньяки, используем в OnRoundEnd
    public List<CCSPlayerController> GetActivePlayers(); // CT OR T players
    public List<CCSPlayerController> GetSurvivors(); 

    event Action OnNextManiacsSetted; // OnRoundEnd - маньяки на следующий раунд установленны
    event Action OnManiacsWin; // OnRoundEnd - маньяки победили
    event Action OnSurvivorsWin; // OnRoundEnd - выжившие победили
}
