using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class FoodSpawn_2_14 : MonoBehaviour
{
    public GameObject foodPrefab;
    public Sprite[] foodSprites;

    public Transform player;
    public float shieldRadius = 2f;

    Camera cam;

    void Awake()
    {
        cam = Camera.main;
    }

    public void SpawnOneFood(float moveTime)
    {
        Vector3 spawnPos = GetOutsidePosition();

        GameObject food = Instantiate(foodPrefab, spawnPos, Quaternion.identity, transform);

        SpriteRenderer sr = food.GetComponent<SpriteRenderer>();

        if (sr != null && foodSprites.Length > 0)
        {
            sr.sprite = foodSprites[Random.Range(0, foodSprites.Length)];
        }

        food.GetComponent<FoodMove_2_14>()
            ?.Init(player, shieldRadius, moveTime);
    }

    Vector3 GetOutsidePosition()
    {
        Vector3 min = cam.ViewportToWorldPoint(new Vector3(0, 0, 10));
        Vector3 max = cam.ViewportToWorldPoint(new Vector3(1, 1, 10));

        float margin = 1.5f;

        int side = Random.Range(0, 4);

        switch (side)
        {
            case 0:
                return new Vector3(
                    min.x - margin,
                    Random.Range(min.y, max.y),
                    0);

            case 1:
                return new Vector3(
                    max.x + margin,
                    Random.Range(min.y, max.y),
                    0);

            case 2:
                return new Vector3(
                    Random.Range(min.x, max.x),
                    max.y + margin,
                    0);

            default:
                return new Vector3(
                    Random.Range(min.x, max.x),
                    min.y - margin,
                    0);
        }
    }
}
