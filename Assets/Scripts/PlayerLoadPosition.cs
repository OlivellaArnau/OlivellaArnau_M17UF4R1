using UnityEngine;
using System.IO;  // �A�ade esta l�nea!

public class PlayerLoadPosition : MonoBehaviour
{
    private void Start()
    {
        if (GameManager.Instance != null && File.Exists(Application.persistentDataPath + "/savefile.json"))
        {
            GameManager.Instance.LoadGame();
            transform.position = GameManager.Instance.PlayerPosition;
            transform.rotation = GameManager.Instance.PlayerRotation;
        }
    }
}