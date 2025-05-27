using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class UIManager : NetworkBehaviour
{
    //private NetworkManager _NetworkManager;

    [SerializeField] NetworkManager _NetworkManager;
    [SerializeField] private TextMeshProUGUI humanCountText;
    
    private NetworkVariable<int> humansNum = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone);

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

    private void Update()
    {
        humanCountText.text = humansNum.Value.ToString();

        if (!IsServer) return;
        humansNum.Value = NetworkManager.Singleton.ConnectedClients.Count;
    }

}
