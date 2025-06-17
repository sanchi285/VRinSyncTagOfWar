using UnityEngine;
using Unity.Netcode;

public class MusicManagerSpawner : MonoBehaviour
{
    [Tooltip("Drag in your MusicManager prefab here")]
    public GameObject musicManagerPrefab;

    private void Start()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            var go = Instantiate(musicManagerPrefab);
            go.GetComponent<NetworkObject>().Spawn();
            Debug.Log("[Spawner] MusicManager spawned on Host");
        }
    }
}
