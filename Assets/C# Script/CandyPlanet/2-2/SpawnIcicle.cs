using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class SpawnIcicle : MonoBehaviour
{
    public GameObject iciclePrefab;

    [Header("X Position")]
    [SerializeField] private float startX = -7f;
    [SerializeField] private float step = 1f;

    [Header("Spawn Delays")]
    public float[] spawnDelays;

    public int index = 0;
    private float currentX;
    private bool waiting = false;

    private MiniGame2_2 minigame2_2;
    [SerializeField] private CapsuleCollider2D player;
    

    void Awake()
    {
        currentX = startX;
        minigame2_2 = GetComponentInParent<MiniGame2_2>();
        player = GetComponent<CapsuleCollider2D>();
    }
    private void HandleIcicleFalling()
    {
        // 이미 모든 고드름을 다 생성했으면 종료
        if (index >= spawnDelays.Length) return;

        SpawnNext();
    }
    public void SpawnNext()
    {
        Vector3 pos = new Vector3(currentX, transform.position.y, transform.position.z);
        GameObject icicle = Instantiate(iciclePrefab, pos, Quaternion.identity, minigame2_2.transform);

        Icicle icicleScript = icicle.GetComponent<Icicle>();
        icicleScript.OnIcicleFalling += HandleIcicleFalling;   // 이 고드름 하나에만 구독

        float delay = spawnDelays[index];
        icicleScript.StartIcicle(delay);

        currentX += step;
        index++;
    }

    private IEnumerator DelayAndSpawn()
    {
        waiting = true;

        float delay = spawnDelays[index];
        yield return new WaitForSeconds(delay);

        Spawn();
        index++;
        waiting = false;
    }
    private void Spawn()
    {
        Vector3 pos = new Vector3(currentX, transform.position.y, transform.position.z);
        Instantiate(iciclePrefab, pos, Quaternion.identity);
        currentX += step;
    }
    private IEnumerator SpawnLoop()
    {
        float currentX = startX;

        for (int i = 0; i < spawnDelays.Length; i++)
        {
            yield return new WaitForSeconds(spawnDelays[i]);

            Vector3 spawnPos = new Vector3(
                currentX,
                transform.position.y,
                transform.position.z
            );

            Instantiate(iciclePrefab, spawnPos, Quaternion.identity);

            currentX += step;
        }
    }
}
