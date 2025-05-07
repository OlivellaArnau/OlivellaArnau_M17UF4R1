using UnityEngine;
using System.IO; // Per guardar dades

public class GameManager : MonoBehaviour
{
    // Singleton (accessible des de qualsevol script)
    public static GameManager Instance { get; private set; }

    // Dades a guardar
    [System.Serializable]
    public class SaveData
    {
        public Vector3 playerPosition;
        public Quaternion playerRotation;
        public bool hasCollectable;
        public string currentScene;
    }

    // Variables del joc
    public bool HasCollectable { get; set; }
    public string CurrentScene { get; private set; }
    public Vector3 PlayerPosition { get; private set; }
    public Quaternion PlayerRotation { get; private set; }

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persisteix entre escenes
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Guardar partida
    public void SaveGame(Vector3 playerPos, Quaternion playerRot)
    {
        SaveData data = new SaveData
        {
            playerPosition = playerPos,
            playerRotation = playerRot,
            hasCollectable = HasCollectable,
            currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        };

        string json = JsonUtility.ToJson(data);
        File.WriteAllText(Application.persistentDataPath + "/savefile.json", json);
        Debug.Log("Partida guardada a: " + Application.persistentDataPath);
    }

    // Carregar partida
    public void LoadGame()
    {

        string path = Application.persistentDataPath + "/savefile.json";
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // Actualitza les dades del GameManager
            PlayerPosition = data.playerPosition;
            PlayerRotation = data.playerRotation;
            HasCollectable = data.hasCollectable;
            CurrentScene = data.currentScene;

            // Carrega l'escena guardada (i després posiciona el jugador des del seu script)
            UnityEngine.SceneManagement.SceneManager.LoadScene(data.currentScene);
            Debug.Log("Partida carregada des de: " + path);
        }
        else
        {
            Debug.LogWarning("No s'ha trobat fitxer de guardat.");
        }
    }

    // Reset del joc (opcional)
    public void ResetGame()
    {
        HasCollectable = false;
        CurrentScene = "Exterior"; // Escena per defecte
        if (File.Exists(Application.persistentDataPath + "/savefile.json"))
        {
            File.Delete(Application.persistentDataPath + "/savefile.json");
        }
    }
}
