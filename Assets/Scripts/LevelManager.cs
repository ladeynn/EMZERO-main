using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;

public enum GameMode
{
    Tiempo,
    Monedas
}
public class LevelManager : NetworkBehaviour
{
    #region Properties

    [Header("Prefabs")]
    [SerializeField] GameObject playerPrefab;
    [SerializeField] GameObject zombiePrefab;

    [Header("Team Settings")]
    [Tooltip("N�mero m�ximo de jugadores humanos")]
    [SerializeField] private int maxHumans = 2;
    [Tooltip("N�mero m�ximo de zombis")]
    [SerializeField] private int maxZombies = 2;

    [Header("Game Mode Settings")]
    [SerializeField] private GameMode gameMode;
    [SerializeField] private int minutes = 5;

    private List<Vector3> humanSpawnPoints = new List<Vector3>();
    private List<Vector3> zombieSpawnPoints = new List<Vector3>();

    private TextMeshProUGUI humansText;
    private TextMeshProUGUI zombiesText;
    private TextMeshProUGUI gameModeText;

    private int coinsGenerated = 0;
    private bool isGameOver = false;
    private float remainingSeconds;

    private LevelBuilder levelBuilder; // Declarar la variable levelBuilder
    private PlayerController playerController;
    public string PlayerPrefabName => playerPrefab.name;
    public string ZombiePrefabName => zombiePrefab.name;


    public GameObject gameOverPanel; // Asigna el panel desde el inspector
    [SerializeField] NetworkManager _NetworkManager;
    #endregion

    private void Start()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }

        // Buscar puntos de spawn y generar monedas
        levelBuilder.Build();
        humanSpawnPoints = levelBuilder.GetHumanSpawnPoints();
        zombieSpawnPoints = levelBuilder.GetZombieSpawnPoints();
        coinsGenerated = levelBuilder.GetCoinsGenerated();

        remainingSeconds = minutes * 60;
    }

    private void HandleClientConnected(ulong clientId)
    {
        int playerCount = NetworkManager.Singleton.ConnectedClients.Count;
        GameObject playerInstance;
        Vector3 spawnPosition;

        // Asignar aleatoriamente a los jugadores como humano o zombi
        bool isHuman = (playerCount % 2 == 0); // Alternar entre humano y zombi

        if (isHuman)
        {
            // Spawn humano
            spawnPosition = humanSpawnPoints[playerCount % humanSpawnPoints.Count];
            playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            // Spawn zombi
            spawnPosition = zombieSpawnPoints[playerCount % zombieSpawnPoints.Count];
            playerInstance = Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
        }

        // Asociar el player con el NetworkObject para que sea gestionado por el servidor
        var netObj = playerInstance.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);

        // Asignar el rol correspondiente (Humano o Zombi)
        playerController = playerInstance.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.isZombie = !isHuman; // Si es humano, el rol ser� false (no zombi), si es zombi ser� true
        }
    }

    private void Update()
    {
        if (gameMode == GameMode.Tiempo)
        {
            HandleTimeLimitedGameMode();
        }
        else if (gameMode == GameMode.Monedas)
        {
            HandleCoinBasedGameMode();
        }

        if (isGameOver)
        {
            ShowGameOverPanel();
        }

        UpdateTeamUI();
    }

    private void UpdateTeamUI()
    {
        if (humansText != null)
        {
            humansText.text = $"{maxHumans}";
        }

        if (zombiesText != null)
        {
            zombiesText.text = $"{maxZombies}";
        }
    }

    private void HandleTimeLimitedGameMode()
    {
        if (isGameOver) return;

        remainingSeconds -= Time.deltaTime;

        if (remainingSeconds <= 0)
        {
            isGameOver = true;
            remainingSeconds = 0;
        }

        int minutesRemaining = Mathf.FloorToInt(remainingSeconds / 60);
        int secondsRemaining = Mathf.FloorToInt(remainingSeconds % 60);

        if (gameModeText != null)
        {
            gameModeText.text = $"{minutesRemaining:D2}:{secondsRemaining:D2}";
        }
    }

    private void HandleCoinBasedGameMode()
    {
        if (isGameOver) return;

        if (gameModeText != null && playerController != null)
        {
            gameModeText.text = $"{playerController.CoinsCollected}/{coinsGenerated}";
            if (playerController.CoinsCollected == coinsGenerated)
            {
                isGameOver = true;
            }
        }
    }

    private void ShowGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            Time.timeScale = 0f;
            gameOverPanel.SetActive(true); // Muestra el panel de pausa
            Cursor.lockState = CursorLockMode.None; // Desbloquea el cursor
            Cursor.visible = true; // Hace visible el cursor
        }
    }
    public void ChangeToZombie(GameObject player)
    {
        // L�gica para cambiar el estado del jugador a zombi
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.isZombie = true;
            // Otros cambios relacionados con el jugador
        }
    }
    public void ChangeToZombie()
    {
        GameObject currentPlayer = GameObject.FindGameObjectWithTag("Player");
        ChangeToZombie(currentPlayer, true);
    }
    
    public void ChangeToZombie(GameObject human, bool enabled)
    {
        Debug.Log("Cambiando a Zombie");

        if (human != null)
        {
            // Guardar la posici�n, rotaci�n y uniqueID del humano actual
            Vector3 playerPosition = human.transform.position;
            Quaternion playerRotation = human.transform.rotation;
            string uniqueID = human.GetComponent<PlayerController>().uniqueID;

            // Destruir el humano actual
            Destroy(human);

            // Instanciar el prefab del zombie en la misma posici�n y rotaci�n
            GameObject zombie = Instantiate(zombiePrefab, playerPosition, playerRotation);
            if (enabled) { zombie.tag = "Player"; }

            // Obtener el componente PlayerController del zombie instanciado
            PlayerController playerController = zombie.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.enabled = enabled;
                playerController.isZombie = true; // Cambiar el estado a zombie
                playerController.uniqueID = uniqueID; // Mantener el identificador �nico

                UpdateTeamUI();

                if (enabled)
                {
                    // Obtener la referencia a la c�mara principal
                    Camera mainCamera = Camera.main;

                    if (mainCamera != null)
                    {
                        // Obtener el script CameraController de la c�mara principal
                        CameraController cameraController = mainCamera.GetComponent<CameraController>();

                        if (cameraController != null)
                        {
                            // Asignar el zombie al script CameraController
                            cameraController.player = zombie.transform;
                        }

                        // Asignar el transform de la c�mara al PlayerController
                        playerController.cameraTransform = mainCamera.transform;
                    }
                    else
                    {
                        Debug.LogError("No se encontr� la c�mara principal.");
                    }
                }
            }
            else
            {
                Debug.LogError("PlayerController no encontrado en el zombie instanciado.");
            }
        }
        else
        {
            Debug.LogError("No se encontr� el humano actual.");
        }
    }
}
