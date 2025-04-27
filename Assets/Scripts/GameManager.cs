using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    // Singleton para acceso rápido
    public static GameManager Instance;

    // Listas de jugadores
    public List<PlayerController> humans = new List<PlayerController>();
    public List<PlayerController> zombies = new List<PlayerController>();

    // Diccionario para guardar el equipo de cada jugador por su ClientId
    private Dictionary<ulong, Team> playerTeams = new Dictionary<ulong, Team>();

    public enum Team { Human, Zombie }

    // NetworkVariable para contar monedas recogidas (modo monedas)
    public NetworkVariable<int> totalCoinsCollected = new NetworkVariable<int>(0);

    // Tiempo de partida (modo tiempo)
    public NetworkVariable<float> timeRemaining = new NetworkVariable<float>(300f); // Ej: 5 minutos

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Cliente conectado: {clientId}");

        // Esperar a que PlayerController esté listo y asignar equipo
        StartCoroutine(AssignTeamAfterSpawn(clientId));
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Cliente desconectado: {clientId}");

        // Eliminar de equipos si estaba
        if (playerTeams.TryGetValue(clientId, out Team team))
        {
            PlayerController pc = FindPlayerByClientId(clientId);
            if (pc != null)
            {
                if (team == Team.Human) humans.Remove(pc);
                else if (team == Team.Zombie) zombies.Remove(pc);
            }

            playerTeams.Remove(clientId);
        }

        // Opcional: comprobar si queda solo un equipo ? terminar partida
        CheckEndGameConditions();
    }

    private System.Collections.IEnumerator AssignTeamAfterSpawn(ulong clientId)
    {
        // Esperar un frame para asegurar que PlayerController ha spawneado
        yield return new WaitForSeconds(0.1f);

        PlayerController player = FindPlayerByClientId(clientId);

        if (player != null)
        {
            // Asignar equipo equilibradamente
            Team assignedTeam = (humans.Count <= zombies.Count) ? Team.Human : Team.Zombie;
            player.SetTeamServerRpc(assignedTeam); // Pedimos al Player que actualice su equipo en red
            playerTeams.Add(clientId, assignedTeam);

            if (assignedTeam == Team.Human)
                humans.Add(player);
            else
                zombies.Add(player);
        }
    }

    private PlayerController FindPlayerByClientId(ulong clientId)
    {
        foreach (var player in FindObjectsOfType<PlayerController>())
        {
            if (player.OwnerClientId == clientId)
                return player;
        }
        return null;
    }

    public void CollectCoin(PlayerController collector)
    {
        if (!IsServer) return;

        totalCoinsCollected.Value++;

        Debug.Log($"Monedas recogidas: {totalCoinsCollected.Value}");

        // Opcional: comprobar si todas las monedas han sido recogidas
        CheckEndGameConditions();
    }

    public void ConvertToZombie(PlayerController human)
    {
        if (!IsServer) return;

        humans.Remove(human);
        zombies.Add(human);
        playerTeams[human.OwnerClientId] = Team.Zombie;

        human.SetTeamServerRpc(Team.Zombie);

        Debug.Log($"Jugador {human.OwnerClientId} convertido en zombi");

        CheckEndGameConditions();
    }

    private void CheckEndGameConditions()
    {
        // Ejemplos:
        if (humans.Count == 0)
        {
            Debug.Log("¡Los zombis han ganado!");
            EndGameServerRpc("ZombiesWin");
        }
        else if (totalCoinsCollected.Value >= GetTotalCoinsInLevel())
        {
            Debug.Log("¡Los humanos han ganado recogiendo todas las monedas!");
            EndGameServerRpc("HumansWin");
        }
        // También puedes añadir el modo de tiempo aquí
    }

    private int GetTotalCoinsInLevel()
    {
        // Deberías obtener el número real de monedas del escenario
        return FindObjectsOfType<Coin>().Length;
    }

    [ServerRpc(RequireOwnership = false)]
    private void EndGameServerRpc(string result)
    {
        EndGameClientRpc(result);
    }

    [ClientRpc]
    private void EndGameClientRpc(string result)
    {
        // Mostrar mensaje de fin de partida en cada cliente
        Debug.Log($"Fin de partida: {result}");

        // Aquí llamarías al panel de GameOver, mostrarías mensaje, etc.
    }
}
