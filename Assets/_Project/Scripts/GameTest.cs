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
    }
}