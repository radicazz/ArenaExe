using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] GameObject _rangedEnemyPrefab;
    [SerializeField] GameObject _meleeEnemyPrefab;

    [Header("Spawn Points")]
    [SerializeField] Transform[] _spawnPoints = Array.Empty<Transform>();

    [Header("Wave Scaling")]
    [SerializeField, Min(1)] int _baseEnemiesPerWave = 4;
    [SerializeField, Min(0f)] float _enemyGrowthPerWave = 1.5f;
    [SerializeField, Min(0f)] float _spawnSpreadSeconds = 0.35f;

    readonly List<Transform> _activeSpawnPoints = new();

    float _spawnTimer;
    int _queuedSpawns;
    int _currentWave;
    int _spawnIndex;

    void Update()
    {
        if (_queuedSpawns <= 0)
        {
            return;
        }

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f)
        {
            SpawnNextEnemy();
            _spawnTimer = _spawnSpreadSeconds;
        }
    }

    public void SpawnWave(int waveNumber)
    {
        PrepareSpawnPoints();
        if (_activeSpawnPoints.Count == 0)
        {
            Debug.LogWarning("EnemySpawner has no spawn points assigned.", this);
            return;
        }

        if (_rangedEnemyPrefab == null)
        {
            Debug.LogWarning("EnemySpawner has no ranged enemy prefab assigned.", this);
            return;
        }

        _currentWave = Mathf.Max(1, waveNumber);
        _queuedSpawns = CalculateEnemyCount(_currentWave);
        _spawnIndex = 0;
        _spawnTimer = 0f;

        Debug.Log($"[EnemySpawner] Preparing {_queuedSpawns} enemies for wave {_currentWave}.", this);
    }

    void PrepareSpawnPoints()
    {
        _activeSpawnPoints.Clear();

        if (_spawnPoints == null)
        {
            return;
        }

        for (int i = 0; i < _spawnPoints.Length; i++)
        {
            Transform spawnPoint = _spawnPoints[i];
            if (spawnPoint != null)
            {
                _activeSpawnPoints.Add(spawnPoint);
            }
        }
    }

    int CalculateEnemyCount(int waveNumber)
    {
        float scaled = _baseEnemiesPerWave + (waveNumber - 1) * _enemyGrowthPerWave;
        return Mathf.Max(1, Mathf.RoundToInt(scaled));
    }

    void SpawnNextEnemy()
    {
        if (_queuedSpawns <= 0)
        {
            return;
        }

        if (_activeSpawnPoints.Count == 0)
        {
            Debug.LogWarning("EnemySpawner attempted to spawn with no active spawn points.", this);
            _queuedSpawns = 0;
            return;
        }

        Transform spawnPoint = _activeSpawnPoints[_spawnIndex % _activeSpawnPoints.Count];
        _spawnIndex++;

        if (spawnPoint == null)
        {
            Debug.LogWarning("EnemySpawner encountered an empty spawn point entry.", this);
            _queuedSpawns--;
            return;
        }

        Instantiate(_rangedEnemyPrefab, spawnPoint.position, spawnPoint.rotation);
        _queuedSpawns--;

        Debug.Log($"[EnemySpawner] Spawned ranged enemy at {spawnPoint.name} for wave {_currentWave}.", this);
    }
}

