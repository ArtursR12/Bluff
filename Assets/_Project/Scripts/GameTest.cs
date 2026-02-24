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

        UIManager.Instance.RefreshUI(
            GameManager.Instance.GetState(),
            "0"  // "0" is Arturs player Id
        );
    }
}