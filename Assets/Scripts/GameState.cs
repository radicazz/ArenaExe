using System;
using System.Collections.Generic;
using UnityEngine;

public class GameState : MonoBehaviour
{
    public enum GamePhase
    {
        Intro,
        Preparation,
        Combat
    }

    public sealed class PlayerStats
    {
        public PlayerStats(PlayerController player)
        {
            Player = player;
            PlayerTag = player != null ? player.tag : string.Empty;
        }

        public PlayerController Player { get; }
        public string PlayerTag { get; }
        public int Kills { get; private set; }
        public int Score { get; private set; }
        public int HealthPickupsUsed { get; private set; }
        public float DamageTaken { get; private set; }

        internal void RegisterKill(int scoreAward)
        {
            Kills++;
            Score += Mathf.Max(0, scoreAward);
        }

        internal void AddScore(int amount)
        {
            Score += amount;
        }

        internal void RegisterHealthPickup()
        {
            HealthPickupsUsed++;
        }

        internal void RegisterDamageTaken(float amount)
        {
            DamageTaken += Mathf.Max(0f, amount);
        }
    }

    public static GameState Instance { get; private set; }

    public event Action<GamePhase> PhaseChanged;
    public event Action<int> WaveStarted;
    public event Action<int> WaveCompleted;
    [SerializeField, Min(1)] int _startingWave = 1;

    readonly List<PlayerController> _players = new();
    readonly Dictionary<PlayerController, PlayerStats> _playerStats = new();

    GamePhase _currentPhase = GamePhase.Intro;
    int _currentWave;

    public GamePhase CurrentPhase => _currentPhase;
    public int CurrentWave => _currentWave;
    public IReadOnlyList<PlayerController> Players => _players;
    public IEnumerable<PlayerStats> PlayerStatistics => _playerStats.Values;

    EnemySpawner _enemySpawner = null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate GameState detected. Destroying the newest instance.");
            Destroy(this);
            return;
        }

        Instance = this;

        if (_enemySpawner == null)
        {
            _enemySpawner = GetComponent<EnemySpawner>();
            if (_enemySpawner == null)
            {
                Debug.LogWarning("GameState could not locate an EnemySpawner on the same GameObject.", this);
            }
        }

        _currentWave = Mathf.Max(1, _startingWave);
        RefreshPlayers();
    }

    void Start()
    {
        SetPhase(GamePhase.Intro);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void BeginIntroPhase()
    {
        SetPhase(GamePhase.Intro);
    }

    public void BeginCombatPhase()
    {
        if (_currentPhase == GamePhase.Combat)
        {
            return;
        }

        if (_currentPhase == GamePhase.Preparation)
        {
            _currentWave++;
        }

        SetPhase(GamePhase.Combat);
        Debug.Log($"[GameState] Wave {_currentWave} started.", this);
        WaveStarted?.Invoke(_currentWave);
        _enemySpawner?.SpawnWave(_currentWave);
    }

    public void BeginPreparationPhase()
    {
        if (_currentPhase == GamePhase.Preparation)
        {
            return;
        }

        SetPhase(GamePhase.Preparation);
        Debug.Log($"[GameState] Wave {_currentWave} completed. Entering preparation phase.", this);
        WaveCompleted?.Invoke(_currentWave);
    }

    public void RegisterPlayer(PlayerController player)
    {
        if (player == null || _players.Contains(player))
        {
            return;
        }

        _players.Add(player);
        _playerStats[player] = new PlayerStats(player);
    }

    public void UnregisterPlayer(PlayerController player)
    {
        if (player == null)
        {
            return;
        }

        _players.Remove(player);
        _playerStats.Remove(player);
    }

    public PlayerStats GetStats(PlayerController player)
    {
        if (player != null && _playerStats.TryGetValue(player, out PlayerStats stats))
        {
            return stats;
        }

        return null;
    }

    public void RecordKill(PlayerController player, int scoreAward)
    {
        PlayerStats stats = GetStats(player);
        stats?.RegisterKill(scoreAward);
    }

    public void AddScore(PlayerController player, int amount)
    {
        PlayerStats stats = GetStats(player);
        stats?.AddScore(amount);
    }

    public void RecordHealthPickupUsed(PlayerController player)
    {
        PlayerStats stats = GetStats(player);
        stats?.RegisterHealthPickup();
    }

    public void RecordDamageTaken(PlayerController player, float amount)
    {
        PlayerStats stats = GetStats(player);
        stats?.RegisterDamageTaken(amount);
    }

    void RefreshPlayers()
    {
        _players.Clear();
        _playerStats.Clear();

        PlayerController[] foundPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (PlayerController player in foundPlayers)
        {
            RegisterPlayer(player);
        }
    }

    void SetPhase(GamePhase newPhase)
    {
        if (_currentPhase == newPhase)
        {
            return;
        }

        GamePhase previousPhase = _currentPhase;
        _currentPhase = newPhase;
        Debug.Log($"[GameState] Phase change: {previousPhase} -> {_currentPhase}.", this);
        PhaseChanged?.Invoke(_currentPhase);
    }
}


