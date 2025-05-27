using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum GameMode
{
    Tiempo,
    Monedas
}

public class GameManager : NetworkBehaviour
{
    //private NetworkManager _NetworkManager;

    [SerializeField] NetworkManager _NetworkManager;

    [Header("Prefabs")]
    [SerializeField] GameObject playerPrefab;
    [SerializeField] GameObject zombiePrefab;

    [Header("Team Settings")]
    [Tooltip("Número máximo de jugadores humanos")]
    [SerializeField] private int maxHumans = 2;
    [Tooltip("Número máximo de zombis")]
    [SerializeField] private int maxZombies = 2;

    [Header("Game Mode Settings")]
    [SerializeField] private GameMode gameMode;
    [SerializeField] private int minutes = 5;

    private List<Vector3> humanSpawnPoints = new List<Vector3>();
    private List<Vector3> zombieSpawnPoints = new List<Vector3>();

    private int coinsGenerated = 0;
    private bool isGameOver = false;
    private float remainingSeconds;

    public LevelBuilder levelBuilder; // Declarar la variable levelBuilder
    private PlayerController playerController;
    public string PlayerPrefabName => playerPrefab.name;
    public string ZombiePrefabName => zombiePrefab.name;

    int serverPlayerCount;


    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        if (!_NetworkManager.IsClient && !_NetworkManager.IsServer)
        {
            StartButtons();
        }
        else
        {
            StatusLabels();
        }

        GUILayout.EndArea();
    }

    void StartButtons()
    {
        if (GUILayout.Button("Host"))
        {
            _NetworkManager.StartHost();

            //bloquea el cursor en el centro de la pantalla
            Cursor.lockState = CursorLockMode.Locked;
            //hsce el cursor invisible
            Cursor.visible = false;

        }
        if (GUILayout.Button("Client")) _NetworkManager.StartClient();
        if (GUILayout.Button("Server")) _NetworkManager.StartServer();
    }

    void StatusLabels()
    {
        var mode = _NetworkManager.IsHost ?
            "Host" : _NetworkManager.IsServer ? "Server" : "Client";

        GUILayout.Label("Transport: " +
            _NetworkManager.NetworkConfig.NetworkTransport.GetType().Name);
        GUILayout.Label("Mode: " + mode);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Debug.Log("[LevelManager] OnNetworkSpawn() ejecutado en el servidor.");
            levelBuilder.Build();
            humanSpawnPoints = levelBuilder.GetHumanSpawnPoints();
            zombieSpawnPoints = levelBuilder.GetZombieSpawnPoints();
            coinsGenerated = levelBuilder.GetCoinsGenerated();
            // Después de que el servidor genera el nivel, informa a los clientes
            Debug.Log("[LevelManager] Llamando a InformClientsToBuildLevelClientRpc().");
            InformClientsToBuildLevelClientRpc();
            PrintClientCountClientRpc(NetworkManager.Singleton.LocalClientId, NetworkManager.Singleton.ConnectedClients.Count);
        }
        else
        {
            Debug.Log("[LevelManager] OnNetworkSpawn() ejecutado en el cliente.");
            OnServerRpc();

        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void OnServerRpc()
    {
        Debug.Log("el server ha recibido el mensaje");
        InformClientsToBuildLevelClientRpc();
        PrintClientCountClientRpc(NetworkManager.Singleton.LocalClientId, NetworkManager.Singleton.ConnectedClients.Count);
    }
    [ClientRpc]
    private void InformClientsToBuildLevelClientRpc()
    {
        if (!IsServer) // Los clientes (no el servidor que ya lo hizo) construyen el nivel
        {
            Debug.Log("[LevelManager] ClientRpc recibido. Construyendo nivel.");
            levelBuilder.Build();
        }
    }

    private void Start()
    {
        if (!IsServer)
        {
            Debug.Log("[LevelManager] Start() ejecutado en el cliente (solo cliente).");
        }

        if (IsServer)
        {
            Debug.Log("[LevelManager] Start() ejecutado en el server.");
            //NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }

        if (IsClient)
        {
            //NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }

        remainingSeconds = minutes * 60;
    }
    [ServerRpc(RequireOwnership = false)]
    public void getConectedPlayersServerRpc()
    {
        updatePlayerCountClientRpc(NetworkManager.Singleton.ConnectedClients.Count);
    }
    [ClientRpc]
    void updatePlayerCountClientRpc(int players)
    {
        Debug.Log($"player cout: {players}");
        serverPlayerCount = players;
    }
    [ClientRpc]
    public void PrintClientCountClientRpc(ulong clientId, int playerCount)
    {
        Debug.Log($"player cout: {playerCount}");
        GameObject playerInstance;
        Vector3 spawnPosition;

        // Asignar aleatoriamente a los jugadores como humano o zombi
        bool isHuman = (playerCount % 2 == 0); // Alternar entre humano y zombi

        Debug.Log($"isHuman: {isHuman}");
        if (isHuman)
        {
            spawnPosition = humanSpawnPoints[playerCount % humanSpawnPoints.Count];
            Debug.Log($"[LevelManager] Spawning humano en: {spawnPosition}");
            playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            spawnPosition = zombieSpawnPoints[playerCount % zombieSpawnPoints.Count];
            Debug.Log($"[LevelManager] Spawning zombie en: {spawnPosition}");
            playerInstance = Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
        }

        // Asociar el player con el NetworkObject para que sea gestionado por el servidor
        var netObj = playerInstance.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);

        // Asignar el rol correspondiente (Humano o Zombi)
        playerController = playerInstance.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.isZombie = !isHuman; // Si es humano, el rol será false (no zombi), si es zombi será true
        }
    }
    private void HandleClientConnected(ulong clientId)
    {
        PrintClientCountClientRpc(clientId, NetworkManager.Singleton.ConnectedClients.Count);
        int playerCount = NetworkManager.Singleton.ConnectedClients.Count;
        Debug.Log($"player cout: {playerCount}");
        GameObject playerInstance;
        Vector3 spawnPosition;

        // Asignar aleatoriamente a los jugadores como humano o zombi
        bool isHuman = (playerCount % 2 == 0); // Alternar entre humano y zombi

        if (isHuman)
        {
            spawnPosition = humanSpawnPoints[playerCount % humanSpawnPoints.Count];
            Debug.Log($"[LevelManager] Spawning humano en: {spawnPosition}");
            playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            spawnPosition = zombieSpawnPoints[playerCount % zombieSpawnPoints.Count];
            Debug.Log($"[LevelManager] Spawning zombie en: {spawnPosition}");
            playerInstance = Instantiate(zombiePrefab, spawnPosition, Quaternion.identity);
        }

        // Asociar el player con el NetworkObject para que sea gestionado por el servidor
        var netObj = playerInstance.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);

        // Asignar el rol correspondiente (Humano o Zombi)
        playerController = playerInstance.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.isZombie = !isHuman; // Si es humano, el rol será false (no zombi), si es zombi será true
        }
    }


}
