using UnityEngine;
using System.Collections.Generic;

public class GameTest : MonoBehaviour
{
    void Start()
    {
        GameManager.Instance.StartGame(new List<string>
        {
            "Arturs",
            "Bot1",
            "Bot2"
        });

        // Force Arturs turn for testing
        while (GameManager.Instance.GetState().CurrentPlayer.Id != "0")
            GameManager.Instance.GetState().NextTurn();

        UIManager.Instance.RefreshUI(
            GameManager.Instance.GetState(),
            "0"
        );
    }
}