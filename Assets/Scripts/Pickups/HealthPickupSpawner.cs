using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class HealthPickupSpawner : MonoBehaviour
{
    const string LogPrefix = "[HealthPickupSpawner]";

    static readonly string[] _blockedTags =
    {
        "Player 1",
        "Player 2",
        "Enemy",
        "pickup Health"
    };

    [Header("Pickup")]
    [SerializeField] HealthPickupController _pickupPrefab;
    [SerializeField, Min(1)] int _maxActivePickups = 3;
    [SerializeField, Min(0f)] float _spawnInterval = 10f;
    [SerializeField, Min(0f)] float _exclusionRadius = 3f;
    [SerializeField, Range(1, 64)] int _maxSpawnAttempts = 20;

    [Header("Spawn Area")]
    [SerializeField] Transform _spawnVolume;
    [SerializeField] bool _parentSpawnedPickupsToVolume = true;

    readonly List<HealthPickupController> _activePickups = new();
    WaitForSeconds _spawnDelay;
    Coroutine _spawnRoutine;

    void Awake()
    {
        if (_spawnVolume == null)
        {
            _spawnVolume = transform;
        }

        _spawnDelay = new WaitForSeconds(Mathf.Max(0.01f, _spawnInterval));
    }

    void OnEnable()
    {
        HealthPickupController.HealthGranted += HandlePickupGranted;
        SubscribeToGameState();
        RemoveDestroyedPickups();

        GameStateController state = GameStateController.Instance;
        Log($"Enabled. Current phase: {state?.CurrentPhase}.");
        if (state != null && state.CurrentPhase == GameStateController.GamePhase.Combat)
        {
            BeginSpawning();
        }
    }

    void OnDisable()
    {
        HealthPickupController.HealthGranted -= HandlePickupGranted;
        UnsubscribeFromGameState();
        StopSpawning(true);
        Log("Disabled.");
    }

    void SubscribeToGameState()
    {
        GameStateController state = GameStateController.Instance;
        if (state != null)
        {
            state.PhaseChanged += HandlePhaseChanged;
        }
    }

    void UnsubscribeFromGameState()
    {
        GameStateController state = GameStateController.Instance;
        if (state != null)
        {
            state.PhaseChanged -= HandlePhaseChanged;
        }
    }

    void HandlePhaseChanged(GameStateController.GamePhase phase)
    {
        Log($"Phase changed to {phase}.");

        if (phase == GameStateController.GamePhase.Combat)
        {
            BeginSpawning();
        }
        else
        {
            StopSpawning(true);
        }
    }

    void BeginSpawning()
    {
        if (_spawnRoutine != null || _pickupPrefab == null)
        {
            Log("Cannot begin spawning: routine already running or prefab missing.");
            return;
        }

        RemoveDestroyedPickups();
        Log($"Starting spawn loop. Active pickups: {_activePickups.Count}.");
        if (_spawnInterval <= 0f)
        {
            _spawnRoutine = StartCoroutine(SpawnWhileCombatImmediate());
        }
        else
        {
            _spawnDelay = new WaitForSeconds(_spawnInterval);
            _spawnRoutine = StartCoroutine(SpawnWhileCombat());
        }
    }

    IEnumerator SpawnWhileCombatImmediate()
    {
        GameStateController state = GameStateController.Instance;
        while (state != null && state.CurrentPhase == GameStateController.GamePhase.Combat)
        {
            TrySpawnPickup();
            yield return null;
            state = GameStateController.Instance;
        }
        _spawnRoutine = null;
    }

    IEnumerator SpawnWhileCombat()
    {
        // wait once before the first spawn so pickups arrive over time
        yield return _spawnDelay;

        GameStateController state = GameStateController.Instance;
        while (state != null && state.CurrentPhase == GameStateController.GamePhase.Combat)
        {
            TrySpawnPickup();
            yield return _spawnDelay;
            state = GameStateController.Instance;
        }

        _spawnRoutine = null;
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
            ClearActivePickups();
        }
    }

    void ClearActivePickups()
    {
        int removed = 0;
        for (int i = _activePickups.Count - 1; i >= 0; i--)
        {
            HealthPickupController pickup = _activePickups[i];
            if (pickup == null)
            {
                _activePickups.RemoveAt(i);
                continue;
            }

            Destroy(pickup.gameObject);
            _activePickups.RemoveAt(i);
            removed++;
        }

        if (removed > 0)
        {
            Log($"Removed {removed} pickups when clearing.");
        }
    }

    void HandlePickupGranted(object sender, HealthPickupController.HealthGrantedEventArgs e)
    {
        if (sender is HealthPickupController pickup)
        {
            if (_activePickups.Remove(pickup))
            {
                Log($"Pickup consumed by {e.Player?.name ?? "unknown"}. Active remaining: {_activePickups.Count}.");
            }
        }
    }

    void TrySpawnPickup()
    {
        RemoveDestroyedPickups();

        if (_activePickups.Count >= _maxActivePickups)
        {
            Log("Max active pickups reached; skipping spawn.");
            return;
        }

        if (!TryFindSpawnPosition(out Vector3 spawnPosition))
        {
            Log("Failed to locate a valid spawn position this interval.");
            return;
        }

        Quaternion rotation = _spawnVolume.rotation;
        Transform parent = _parentSpawnedPickupsToVolume ? _spawnVolume : transform;
        HealthPickupController pickup = Instantiate(_pickupPrefab, spawnPosition, rotation, parent);
        _activePickups.Add(pickup);
        Log($"Spawned pickup at {spawnPosition}. Active count: {_activePickups.Count}.");
    }

    void RemoveDestroyedPickups()
    {
        for (int i = _activePickups.Count - 1; i >= 0; i--)
        {
            if (_activePickups[i] == null)
            {
                _activePickups.RemoveAt(i);
            }
        }
    }

    bool TryFindSpawnPosition(out Vector3 spawnPosition)
    {
        if (_spawnVolume == null)
        {
            spawnPosition = transform.position;
            return true;
        }

        for (int attempt = 0; attempt < _maxSpawnAttempts; attempt++)
        {
            Vector3 candidate = SamplePointWithinVolume();
            if (IsCandidateValid(candidate))
            {
                spawnPosition = candidate;
                return true;
            }
        }

        spawnPosition = default;
        Log("Exceeded spawn attempts without finding a valid position.");
        return false;
    }

    Vector3 SamplePointWithinVolume()
    {
        Vector3 center = _spawnVolume.position;
        Vector3 right = _spawnVolume.right;
        Vector3 forward = _spawnVolume.forward;
        Vector3 lossy = _spawnVolume.lossyScale;

        float radiusX = lossy.x * 0.5f;
        float radiusZ = lossy.z * 0.5f;
        Vector2 sample = Random.insideUnitCircle;

        Vector3 offset = right * sample.x * radiusX + forward * sample.y * radiusZ;
        float height = lossy.y * 0.5f;
        float vertical = Mathf.Approximately(height, 0f) ? 0f : Random.Range(-height, height);

        Vector3 up = _spawnVolume.up * vertical;
        return center + offset + up;
    }

    void Log(string message)
    {
        Debug.Log($"{LogPrefix} {message}", this);
    }

    bool IsCandidateValid(Vector3 candidate)
    {
        // ensure not too close to existing pickups allocated in list
        float minSqr = _exclusionRadius * _exclusionRadius;
        for (int i = 0; i < _activePickups.Count; i++)
        {
            HealthPickupController active = _activePickups[i];
            if (active == null)
            {
                continue;
            }

            float sqr = (active.transform.position - candidate).sqrMagnitude;
            if (sqr < minSqr)
            {
                return false;
            }
        }

        Collider[] overlaps = Physics.OverlapSphere(candidate, _exclusionRadius);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider collider = overlaps[i];
            if (collider == null)
            {
                continue;
            }

            if (!collider.enabled)
            {
                continue;
            }

            Transform colliderTransform = collider.transform;
            if (colliderTransform == null)
            {
                continue;
            }

            string tag = colliderTransform.tag;
            for (int j = 0; j < _blockedTags.Length; j++)
            {
                if (colliderTransform.CompareTag(_blockedTags[j]))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
