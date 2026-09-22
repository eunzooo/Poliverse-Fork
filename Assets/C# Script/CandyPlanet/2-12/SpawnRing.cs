using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnRing : MonoBehaviour
{
    [SerializeField] private GameObject ringPrefab;
    public Transform spawnPoint;

    public float minOffsetX = -2f;
    public float maxOffsetX = 2f;

    public void SpawnRings(int count)
    {
        for (int i = 0; i < count; i++)
        {
            float offsetX = Random.Range(minOffsetX, maxOffsetX);
            Vector3 pos = spawnPoint.position + new Vector3(offsetX, 0);
            Instantiate(ringPrefab, pos, Quaternion.identity, transform);
        }
    }
}
