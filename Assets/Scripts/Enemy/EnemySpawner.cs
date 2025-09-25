using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    const string LogPrefix = "[EnemySpawner]";

    [Header("Prefabs & Spawn Points")]
    [SerializeField] List<GameObject> _enemyPrefabs = new();
    [SerializeField] List<Transform> _spawnPoints = new();

    [Header("Runtime State")]
    [SerializeField] int _pendingSpawns;
    [SerializeField] int _activeEnemies;
    [SerializeField] int _maxConcurrentEnemies;

    readonly List<GameObject> _activeEnemiesList = new();
    public int PendingEnemies => Mathf.Max(0, _pendingSpawns);
    public int ActiveEnemies => Mathf.Max(0, _activeEnemies);
    public int EnemiesRemaining => Mathf.Max(0, _pendingSpawns + _activeEnemies);

    GameStateController _gameState;
    Coroutine _bindRoutine;
    Coroutine _spawnRoutine;
    WaitForSeconds _spawnDelay;

    int _waveNumber;
    float _enemyHealthMultiplier = 1f;
    float _enemySpeedMultiplier = 1f;
    float _enemyRangeMultiplier = 1f;
    float _enemyDamageMultiplier = 1f;

    void OnEnable()
    {
        _bindRoutine = StartCoroutine(BindGameStateWhenReady());
    }

    void OnDisable()
    {
        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }

        StopSpawning(true);

        if (_gameState != null)
        {
            _gameState.PhaseChanged -= HandlePhaseChanged;
        }

        _gameState = null;
        _activeEnemiesList.Clear();
        Log("Spawner disabled.");
    }

    IEnumerator BindGameStateWhenReady()
    {
        while (GameStateController.Instance == null)
        {
            yield return null;
        }

        BindToGameState(GameStateController.Instance);
    }

    void BindToGameState(GameStateController controller)
    {
        if (controller == null)
        {
            return;
        }

        if (_gameState == controller)
        {
            return;
        }

        if (_gameState != null)
        {
            _gameState.PhaseChanged -= HandlePhaseChanged;
        }

        _gameState = controller;
        _gameState.PhaseChanged += HandlePhaseChanged;

        Log($"Bound to GameStateController. Current phase: {_gameState.CurrentPhase}.");

        ResetState();

        if (_gameState.CurrentPhase == GameStateController.GamePhase.Combat)
        {
            ConfigureWave();
            BeginSpawning();
        }
    }

    void HandlePhaseChanged(GameStateController.GamePhase phase)
    {
        Log($"Phase changed to {phase}.");

        if (phase == GameStateController.GamePhase.Combat)
        {
            ConfigureWave();
            BeginSpawning();
        }
        else
        {
            StopSpawning(phase == GameStateController.GamePhase.Preparation);
        }
    }

    void ConfigureWave()
    {
        if (_gameState == null)
        {
            return;
        }

        ResetState();

        _pendingSpawns = Mathf.Max(0, _gameState.TotalEnemiesThisWave);
        _maxConcurrentEnemies = Mathf.Max(1, _gameState.MaxSimultaneousEnemiesThisWave);
        float interval = Mathf.Max(0.05f, _gameState.SpawnIntervalThisWave);
        _spawnDelay = new WaitForSeconds(interval);
        _waveNumber = Mathf.Max(1, _gameState.CurrentWave);
        _enemyHealthMultiplier = Mathf.Max(0.01f, _gameState.EnemyHealthMultiplierThisWave);
        _enemySpeedMultiplier = Mathf.Max(0.01f, _gameState.EnemySpeedMultiplierThisWave);
        _enemyRangeMultiplier = Mathf.Max(0.01f, _gameState.EnemyRangeMultiplierThisWave);
        _enemyDamageMultiplier = Mathf.Max(0.01f, _gameState.EnemyDamageMultiplierThisWave);

        Log($"Configured wave | pending={_pendingSpawns}, maxActive={_maxConcurrentEnemies}, interval={interval:F2}");
    }

    void ResetState()
    {
        StopSpawning(false);
        _pendingSpawns = 0;
        _activeEnemies = 0;
        _maxConcurrentEnemies = 0;
        _waveNumber = 0;
        _enemyHealthMultiplier = 1f;
        _enemySpeedMultiplier = 1f;
        _enemyRangeMultiplier = 1f;
        _enemyDamageMultiplier = 1f;
        CleanupDestroyedEnemies(true);
    }

    void BeginSpawning()
    {
        if (_spawnRoutine != null)
        {
            return;
        }

        if (_enemyPrefabs.Count == 0 || _spawnPoints.Count == 0)
        {
            Log("Cannot spawn: prefabs or spawn points missing.");
            return;
        }

        if (_pendingSpawns <= 0)
        {
            Log("No pending enemies for this wave.");
            if (_activeEnemies == 0)
            {
                _gameState?.BeginPreparationPhase();
            }
            return;
        }

        _spawnRoutine = StartCoroutine(SpawnLoop());
        Log("Spawn loop started.");
    }

    void StopSpawning(bool clearActive)
    {
        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
            Log("Spawn loop stopped.");
        }

        if (clearActive)
        {
            CleanupDestroyedEnemies(true);
        }
    }

    IEnumerator SpawnLoop()
    {
        while (_gameState != null && _gameState.CurrentPhase == GameStateController.GamePhase.Combat)
        {
            CleanupDestroyedEnemies();

            if (_pendingSpawns <= 0)
            {
                if (_activeEnemies <= 0)
                {
                    Log("Wave complete, signalling preparation phase.");
                    _gameState.BeginPreparationPhase();
                    break;
                }

                yield return null;
                continue;
            }

            if (_maxConcurrentEnemies > 0 && _activeEnemies >= _maxConcurrentEnemies)
            {
                yield return null;
                continue;
            }

            SpawnEnemy();

            if (_spawnDelay != null)
            {
                yield return _spawnDelay;
            }
            else
            {
                yield return null;
            }
        }

        _spawnRoutine = null;
    }

    void SpawnEnemy()
    {
        GameObject prefab = _enemyPrefabs[Random.Range(0, _enemyPrefabs.Count)];
        Transform spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Count)];

        if (prefab == null || spawnPoint == null)
        {
            Log("Spawn cancelled: invalid prefab or spawn point.");
            return;
        }

        GameObject enemy = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        _activeEnemiesList.Add(enemy);

        _activeEnemies++;
        _pendingSpawns = Mathf.Max(0, _pendingSpawns - 1);
        Log($"Spawned '{prefab.name}' at {spawnPoint.position}. Active={_activeEnemies} Pending={_pendingSpawns}");

        if (enemy != null)
        {
            IWaveScalableEnemy scalableEnemy = enemy.GetComponent<IWaveScalableEnemy>();
            if (scalableEnemy != null)
            {
                int wave = _waveNumber > 0 ? _waveNumber : (_gameState != null ? _gameState.CurrentWave : 1);
                scalableEnemy.ApplyWaveScaling(wave, _enemyHealthMultiplier, _enemySpeedMultiplier, _enemyRangeMultiplier, _enemyDamageMultiplier);
            }
        }

        EnemyWatcher watcher = enemy.AddComponent<EnemyWatcher>();
        watcher.Initialize(this, enemy);
    }

    void CleanupDestroyedEnemies(bool clearAll = false)
    {
        for (int i = _activeEnemiesList.Count - 1; i >= 0; i--)
        {
            if (_activeEnemiesList[i] == null)
            {
                _activeEnemiesList.RemoveAt(i);
                _activeEnemies = Mathf.Max(0, _activeEnemies - 1);
                Log($"Removed null enemy reference. Active={_activeEnemies}");
            }
        }

        if (clearAll)
        {
            Log("Clearing active enemy list.");
            _activeEnemiesList.Clear();
            _activeEnemies = 0;
        }
    }

    public void NotifyEnemyDestroyed(EnemyWatcher watcher)
    {
        if (watcher == null)
        {
            return;
        }

        if (watcher.TrackedEnemy != null)
        {
            _activeEnemiesList.Remove(watcher.TrackedEnemy);
        }

        _activeEnemies = Mathf.Max(0, _activeEnemies - 1);
        Log($"Enemy destroyed. Active={_activeEnemies}, Pending={_pendingSpawns}");

        if (_gameState != null && _gameState.CurrentPhase == GameStateController.GamePhase.Combat)
        {
            if (_pendingSpawns <= 0 && _activeEnemies == 0)
            {
                Log("Wave complete, signalling preparation phase.");
                _gameState.BeginPreparationPhase();
            }
        }
    }

    void Log(string message)
    {
        Debug.Log($"{LogPrefix} {message}", this);
    }

    public class EnemyWatcher : MonoBehaviour
    {
        EnemySpawner _spawner;
        GameObject _enemy;

        public GameObject TrackedEnemy => _enemy;

        public void Initialize(EnemySpawner spawner, GameObject enemy)
        {
            _spawner = spawner;
            _enemy = enemy;
        }

        void OnDestroy()
        {
            _spawner?.NotifyEnemyDestroyed(this);
        }
    }
}

