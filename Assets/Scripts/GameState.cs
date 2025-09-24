using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameState : MonoBehaviour
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

    public static GameState Instance { get; private set; }

    public event Action<GamePhase> PhaseChanged;
    public event Action<int> WaveStarted;
    public event Action<int> WaveCompleted;
    [SerializeField, Min(1)] int _startingWave = 1;
    [SerializeField, Min(0f)] float _endSceneDelay = 3f;
    [Header("Fade")]
    [SerializeField] Image _fadeImage;
    [SerializeField, Min(0.01f)] float _fadeDuration = 1.5f;
    [Header("Pause")]
    [SerializeField] GameObject _pausePanel;
    [SerializeField] GameObject _hudPanel;

    readonly List<PlayerController> _players = new();
    readonly Dictionary<PlayerController, PlayerStats> _playerStats = new();

    GamePhase _currentPhase = GamePhase.Intro;
    int _currentWave;
    bool _gameEnded;
    Coroutine _endSceneRoutine;
    Coroutine _fadeRoutine;
    GamePhase _phaseBeforePause = GamePhase.Intro;

    public GamePhase CurrentPhase => _currentPhase;
    public bool IsPaused => _currentPhase == GamePhase.Paused;
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
        Time.timeScale = 1f;
        RefreshPlayers();
        InitializeFadeImage();
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

        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

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

        SetPhase(GamePhase.Intro);
    }

    public void BeginCombatPhase()
    {
        if (_gameEnded || _currentPhase == GamePhase.Combat)
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
        ApplyPhaseSideEffects(previousPhase, _currentPhase);
        Debug.Log($"[GameState] Phase change: {previousPhase} -> {_currentPhase}.", this);
        PhaseChanged?.Invoke(_currentPhase);
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

    void InitializeFadeImage()
    {
        if (_fadeImage == null)
        {
            return;
        }

        Color color = _fadeImage.color;
        color.a = (_currentPhase == GamePhase.GameOver) ? 1f : 0f;
        _fadeImage.color = color;
        _fadeImage.raycastTarget = true;
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

        TriggerFadeToBlack();

        if (!gameObject.activeInHierarchy)
        {
            SceneManager.LoadScene("End Scene");
            return;
        }

        _endSceneRoutine = StartCoroutine(LoadEndSceneAfterDelay());
    }

    void TriggerFadeToBlack()
    {
        if (_fadeImage == null)
        {
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            Color immediateColor = _fadeImage.color;
            immediateColor.a = 1f;
            _fadeImage.color = immediateColor;
            return;
        }

        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
        }

        _fadeRoutine = StartCoroutine(FadeToBlack());
    }

    IEnumerator FadeToBlack()
    {
        if (_fadeImage == null)
        {
            yield break;
        }

        float elapsed = 0f;
        Color startColor = _fadeImage.color;
        Color targetColor = startColor;
        targetColor.a = 1f;

        if (_fadeDuration <= Mathf.Epsilon)
        {
            _fadeImage.color = targetColor;
            _fadeRoutine = null;
            yield break;
        }

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeDuration);
            _fadeImage.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        _fadeImage.color = targetColor;
        _fadeRoutine = null;
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
