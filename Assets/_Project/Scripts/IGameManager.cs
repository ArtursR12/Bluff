using Bluff.Core;

/// <summary>
/// Common interface for both offline (GameManager) and online (NetworkedGameManager).
/// UIManager depends only on this — never on concrete manager types.
/// </summary>
public interface IGameManager
{
    GameState GetState();
    void PlaceBet(int[] cardIndices, int declaredRankInt);
    void Believe(int cardIndex);
    void Bluff(int cardIndex);
    bool IsShortDeck { get; }
}
