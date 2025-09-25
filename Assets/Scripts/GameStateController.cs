using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStateController : MonoBehaviour
{
    public enum GamePhase
    {
        Intro,
        Preparation,
        Combat,
        Paused,
        GameOver
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

    public static GameStateController Instance { get; private set; }

    public event Action<GamePhase> PhaseChanged;
    public event Action<int> WaveStarted;
    public event Action<int> WaveCompleted;
    [SerializeField, Min(1)] int _startingWave = 1;
    [SerializeField, Min(0f)] float _endSceneDelay = 3f;
    [Header("Preparation")]
    [SerializeField, Min(0f)] float _preparationDuration = 10f;

    [Header("Pause")]
    [SerializeField] GameObject _pausePanel;
    [SerializeField] GameObject _hudPanel;

    [Header("Wave Scaling")]
    [SerializeField, Min(1)] int _baseEnemiesPerWave = 3;
    [SerializeField, Min(0)] int _enemiesPerWaveGrowth = 2;
    [SerializeField, Min(1)] int _maxEnemiesPerWave = 40;
    [SerializeField, Min(1)] int _baseSimultaneousEnemies = 3;
    [SerializeField, Min(0)] int _simultaneousEnemiesGrowth = 1;
    [SerializeField, Min(1)] int _maxSimultaneousEnemies = 10;
    [SerializeField, Min(0f)] float _baseSpawnInterval = 2f;
    [SerializeField, Min(0f)] float _spawnIntervalDecay = 0.1f;
    [SerializeField, Min(0.1f)] float _minSpawnInterval = 0.4f;

    [Header("Enemy Stat Scaling")]
    [SerializeField, Min(0f)] float _enemyHealthMultiplierGrowth = 0.15f;
    [SerializeField, Min(0f)] float _enemySpeedMultiplierGrowth = 0.05f;
    [SerializeField, Min(0f)] float _enemyRangeMultiplierGrowth = 0.1f;
    [SerializeField, Min(0f)] float _enemyDamageMultiplierGrowth = 0.12f;

    readonly List<PlayerController> _players = new();
    readonly Dictionary<PlayerController, PlayerStats> _playerStats = new();

    GamePhase _currentPhase = GamePhase.Intro;
    int _currentWave;
    bool _gameEnded;
    Coroutine _endSceneRoutine;
    Coroutine _preparationTimerRoutine;
    GamePhase _phaseBeforePause = GamePhase.Intro;
    bool _hasStartedCombat;
    float _preparationTimeRemaining;

    public GamePhase CurrentPhase => _currentPhase;
    public bool IsPaused => _currentPhase == GamePhase.Paused;
    public int CurrentWave => _currentWave;
    public IReadOnlyList<PlayerController> Players => _players;
    public IEnumerable<PlayerStats> PlayerStatistics => _playerStats.Values;
    public float PreparationDuration => _preparationDuration;
    public float PreparationTimeRemaining => Mathf.Max(0f, _preparationTimeRemaining);
    public int TotalEnemiesThisWave { get; private set; }
    public int MaxSimultaneousEnemiesThisWave { get; private set; }
    public float SpawnIntervalThisWave { get; private set; }


    public float EnemyHealthMultiplierThisWave { get; private set; } = 1f;
    public float EnemySpeedMultiplierThisWave { get; private set; } = 1f;
    public float EnemyRangeMultiplierThisWave { get; private set; } = 1f;
    public float EnemyDamageMultiplierThisWave { get; private set; } = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate GameState detected. Destroying the newest instance.");
            Destroy(this);
            return;
        }

        Instance = this;

        _currentWave = Mathf.Max(1, _startingWave);
        CalculateWaveMetrics();
        Time.timeScale = 1f;
        RefreshPlayers();
        InitializePausePanel();
    }

    void Start()
    {
        SetPhase(GamePhase.Intro);
    }

    void Update()
    {
        if (!_gameEnded && Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }

        if (_gameEnded || _currentPhase == GamePhase.Paused)
        {
            return;
        }

        if (AreAllPlayersDead())
        {
            EndGame();
        }
    }

    void OnDestroy()
    {
        if (_endSceneRoutine != null)
        {
            StopCoroutine(_endSceneRoutine);
            _endSceneRoutine = null;
        }

        CancelPreparationTimer();

        Time.timeScale = 1f;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void BeginIntroPhase()
    {
        if (_gameEnded)
        {
            return;
        }

        CancelPreparationTimer();
        _hasStartedCombat = false;
        _preparationTimeRemaining = 0f;
        SetPhase(GamePhase.Intro);
    }

    public void BeginCombatPhase()
    {
        if (_gameEnded || _currentPhase == GamePhase.Combat)
        {
            return;
        }

        CancelPreparationTimer();

        bool comingFromPreparation = _currentPhase == GamePhase.Preparation;
        if (_hasStartedCombat)
        {
            if (comingFromPreparation)
            {
                _currentWave++;
            }
        }
        else
        {
            _hasStartedCombat = true;
        }

        CalculateWaveMetrics();
        RefillPlayersAmmoForWave();
        SetPhase(GamePhase.Combat);
        Debug.Log($"[GameState] Wave {_currentWave} started.", this);
        WaveStarted?.Invoke(_currentWave);
    }

    public void TogglePause()
    {
        if (_gameEnded)
        {
            return;
        }

        if (_currentPhase == GamePhase.Paused)
        {
            ResumeFromPause();
        }
        else
        {
            PauseGame();
        }
    }

    void PauseGame()
    {
        if (_currentPhase == GamePhase.GameOver || _currentPhase == GamePhase.Paused)
        {
            return;
        }

        _phaseBeforePause = _currentPhase;
        SetPhase(GamePhase.Paused);
    }

    void ResumeFromPause()
    {
        if (_currentPhase != GamePhase.Paused)
        {
            return;
        }

        SetPhase(_phaseBeforePause);
    }


    public void BeginPreparationPhase()
    {
        if (_gameEnded || _currentPhase == GamePhase.Preparation)
        {
            return;
        }

        CancelPreparationTimer();
        SetPhase(GamePhase.Preparation);
        Debug.Log($"[GameState] Wave {_currentWave} completed. Entering preparation phase.", this);
        WaveCompleted?.Invoke(_currentWave);

        _preparationTimeRemaining = _preparationDuration;
        if (_preparationDuration <= Mathf.Epsilon)
        {
            _preparationTimeRemaining = 0f;
            BeginCombatPhase();
            return;
        }

        _preparationTimerRoutine = StartCoroutine(PreparationCountdown());
    }

    void RefillPlayersAmmoForWave()
    {
        int ammoPerPlayer = Mathf.Max(0, TotalEnemiesThisWave * 5);
        foreach (PlayerController player in _players)
        {
            if (player == null)
            {
                continue;
            }

            player.SetAmmoForWave(ammoPerPlayer);
        }
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

        if (previousPhase == GamePhase.Preparation && newPhase != GamePhase.Preparation && newPhase != GamePhase.Paused)
        {
            CancelPreparationTimer();
        }

        if (newPhase == GamePhase.Intro)
        {
            _hasStartedCombat = false;
        }

        ApplyPhaseSideEffects(previousPhase, _currentPhase);
        Debug.Log($"[GameState] Phase change: {previousPhase} -> {_currentPhase}.", this);
        PhaseChanged?.Invoke(_currentPhase);
    }

    void CalculateWaveMetrics()
    {
        int waveIndex = Mathf.Max(0, _currentWave - 1);
        TotalEnemiesThisWave = Mathf.Clamp(_baseEnemiesPerWave + _enemiesPerWaveGrowth * waveIndex, 1, _maxEnemiesPerWave);
        MaxSimultaneousEnemiesThisWave = Mathf.Clamp(_baseSimultaneousEnemies + _simultaneousEnemiesGrowth * waveIndex, 1, _maxSimultaneousEnemies);
        float interval = _baseSpawnInterval - _spawnIntervalDecay * waveIndex;
        SpawnIntervalThisWave = Mathf.Clamp(interval, _minSpawnInterval, float.MaxValue);
        EnemyHealthMultiplierThisWave = CalculateStatMultiplier(waveIndex, _enemyHealthMultiplierGrowth);
        EnemySpeedMultiplierThisWave = CalculateStatMultiplier(waveIndex, _enemySpeedMultiplierGrowth);
        EnemyRangeMultiplierThisWave = CalculateStatMultiplier(waveIndex, _enemyRangeMultiplierGrowth);
        EnemyDamageMultiplierThisWave = CalculateStatMultiplier(waveIndex, _enemyDamageMultiplierGrowth);
    }

    static float CalculateStatMultiplier(int waveIndex, float growthPerWave)
    {
        if (waveIndex <= 0 || growthPerWave <= 0f)
        {
            return 1f;
        }

        return 1f + growthPerWave * waveIndex;
    }

    void ApplyPhaseSideEffects(GamePhase previousPhase, GamePhase newPhase)
    {
        if (newPhase == GamePhase.Paused)
        {
            Time.timeScale = 0f;
            SetPausePanelVisible(true);
            SetHudVisible(false);
            return;
        }

        if (previousPhase == GamePhase.Paused)
        {
            Time.timeScale = 1f;
            SetPausePanelVisible(false);
            SetHudVisible(true);
        }
    }

    IEnumerator PreparationCountdown()
    {
        float elapsed = 0f;

        while (elapsed < _preparationDuration)
        {
            if (_currentPhase != GamePhase.Preparation && _currentPhase != GamePhase.Paused)
            {
                _preparationTimerRoutine = null;
                _preparationTimeRemaining = 0f;
                yield break;
            }

            yield return null;
            elapsed += Time.deltaTime;
            _preparationTimeRemaining = Mathf.Max(0f, _preparationDuration - elapsed);
        }

        _preparationTimerRoutine = null;
        _preparationTimeRemaining = 0f;
        BeginCombatPhase();
    }

    void CancelPreparationTimer()
    {
        if (_preparationTimerRoutine != null)
        {
            StopCoroutine(_preparationTimerRoutine);
            _preparationTimerRoutine = null;
        }

        _preparationTimeRemaining = 0f;
    }

    void InitializePausePanel()
    {
        SetPausePanelVisible(false);
        SetHudVisible(true);
    }

    void SetPausePanelVisible(bool visible)
    {
        if (_pausePanel == null)
        {
            return;
        }

        _pausePanel.SetActive(visible);
    }

    void SetHudVisible(bool visible)
    {
        if (_hudPanel == null)
        {
            return;
        }

        _hudPanel.SetActive(visible);
    }


    bool AreAllPlayersDead()
    {
        bool anyPlayerFound = false;

        foreach (PlayerController player in _players)
        {
            if (player == null)
            {
                continue;
            }

            anyPlayerFound = true;

            if (player.IsAlive)
            {
                return false;
            }
        }

        return anyPlayerFound;
    }

    void EndGame()
    {
        if (_gameEnded)
        {
            return;
        }

        _gameEnded = true;
        SetPhase(GamePhase.GameOver);
        Debug.Log("[GameState] All players are dead. Game over.", this);

        GameUIController.Instance?.PlayGameOverFade(!gameObject.activeInHierarchy);

        if (!gameObject.activeInHierarchy)
        {
            SceneManager.LoadScene("End Scene");
            return;
        }

        _endSceneRoutine = StartCoroutine(LoadEndSceneAfterDelay());
    }


    IEnumerator LoadEndSceneAfterDelay()
    {
        if (_endSceneDelay > 0f)
        {
            yield return new WaitForSeconds(_endSceneDelay);
        }

        SceneManager.LoadScene("End Scene");
    }
}

