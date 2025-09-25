using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    public static GameUIController Instance { get; private set; }

    const string PlayerOneTag = "Player 1";
    const string PlayerTwoTag = "Player 2";

    [Header("Phase Display")]
    [SerializeField] TMP_Text _phaseLabel;

    [Header("Player One UI")]
    [SerializeField] CanvasGroup _playerOneGroup;
    [SerializeField] RectTransform _playerOneFill;
    [SerializeField] TMP_Text _playerOneAmmoText;

    [Header("Player Two UI")]
    [SerializeField] CanvasGroup _playerTwoGroup;
    [SerializeField] RectTransform _playerTwoFill;
    [SerializeField] TMP_Text _playerTwoAmmoText;

    [Header("Root Fade")]
    [SerializeField] CanvasGroup _rootGroup;
    [SerializeField] Image _blackoutImage;
    [SerializeField, Min(0f)] float _introFadeDuration = 0.35f;
    [SerializeField, Min(0f)] float _gameOverFadeDuration = 1.5f;
    [SerializeField, Min(0f)] float _healthBarFadeDuration = 0.35f;

    [Header("Caching")]
    [SerializeField, Min(0f)] float _playerRefreshInterval = 1f;

    GameStateController _gameState;
    PlayerController _playerOne;
    PlayerController _playerTwo;

    Vector3 _playerOneBaseScale = Vector3.one;
    Vector3 _playerTwoBaseScale = Vector3.one;
    float _playerRefreshTimer;

    Coroutine _rootFadeRoutine;
    Coroutine _blackoutFadeRoutine;
    float _rootTargetAlpha;
    float _blackoutTargetAlpha;
    float _healthBarVisibility;
    EnemySpawner _enemySpawner;
    float _enemySpawnerRefreshTimer;
    [SerializeField, Min(0f)] float _enemySpawnerRefreshInterval = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate GameUIController detected. Destroying the newest instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_playerOneFill != null)
        {
            _playerOneBaseScale = _playerOneFill.localScale;
        }

        if (_playerTwoFill != null)
        {
            _playerTwoBaseScale = _playerTwoFill.localScale;
        }

        _rootTargetAlpha = 0f;
        _blackoutTargetAlpha = 0f;
        _healthBarVisibility = 0f;
        SetRootAlpha(0f);
        SetBlackoutAlpha(0f);

        CachePlayers(force: true);
        UpdatePhaseLabel();
        UpdateHealthBarVisibility(0f);
        UpdateHealthBars();
        RefreshEnemySpawner(true);
    }

    void OnEnable()
    {
        UpdateGameStateSubscription();
        RefreshEnemySpawner(true);
        UpdatePhaseLabel();
        UpdateHealthBarVisibility(0f);
        UpdateHealthBars();
        UpdateAmmoTexts();
        EvaluateFade();
    }

    void OnDisable()
    {
        if (_gameState != null)
        {
            _gameState.PhaseChanged -= HandlePhaseChanged;
            _gameState = null;
        }

        if (_rootFadeRoutine != null)
        {
            StopCoroutine(_rootFadeRoutine);
            _rootFadeRoutine = null;
        }

        if (_blackoutFadeRoutine != null)
        {
            StopCoroutine(_blackoutFadeRoutine);
            _blackoutFadeRoutine = null;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        UpdateGameStateSubscription();
        CachePlayers();
        RefreshEnemySpawner();
        UpdatePhaseLabel();
        UpdateHealthBarVisibility(Time.deltaTime);
        UpdateHealthBars();
        UpdateAmmoTexts();
        EvaluateFade();
    }

    void UpdateGameStateSubscription()
    {
        GameStateController instance = GameStateController.Instance;
        if (instance == _gameState)
        {
            return;
        }

        if (_gameState != null)
        {
            _gameState.PhaseChanged -= HandlePhaseChanged;
        }

        _gameState = instance;

        if (_gameState != null && isActiveAndEnabled)
        {
            _gameState.PhaseChanged += HandlePhaseChanged;
        }
    }

    void HandlePhaseChanged(GameStateController.GamePhase phase)
    {
        UpdatePhaseLabel();
        EvaluateFade();
    }

    void CachePlayers(bool force = false)
    {
        if (!force)
        {
            _playerRefreshTimer -= Time.deltaTime;
            if (_playerRefreshTimer > 0f)
            {
                return;
            }
        }

        _playerRefreshTimer = Mathf.Max(0.1f, _playerRefreshInterval);

        if (_gameState != null)
        {
            foreach (PlayerController player in _gameState.Players)
            {
                AssignPlayerReference(player);
            }
        }

        if (_playerOne == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag(PlayerOneTag);
            AssignPlayerReference(found != null ? found.GetComponent<PlayerController>() : null);
        }

        if (_playerTwo == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag(PlayerTwoTag);
            AssignPlayerReference(found != null ? found.GetComponent<PlayerController>() : null);
        }
    }

    void AssignPlayerReference(PlayerController player)
    {
        if (player == null)
        {
            return;
        }

        if (_playerOne == null && player.CompareTag(PlayerOneTag))
        {
            _playerOne = player;
        }
        else if (_playerTwo == null && player.CompareTag(PlayerTwoTag))
        {
            _playerTwo = player;
        }
    }

    void RefreshEnemySpawner(bool force = false)
    {
        if (!force)
        {
            _enemySpawnerRefreshTimer -= Time.deltaTime;
            if (_enemySpawnerRefreshTimer > 0f)
            {
                return;
            }
        }

        _enemySpawnerRefreshTimer = Mathf.Max(0.1f, _enemySpawnerRefreshInterval);

        if (_enemySpawner == null || !_enemySpawner.isActiveAndEnabled)
        {
            _enemySpawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        }
    }

    void UpdatePhaseLabel()
    {
        if (_phaseLabel == null)
        {
            return;
        }

        if (_gameState == null)
        {
            _phaseLabel.text = string.Empty;
            return;
        }

        GameStateController.GamePhase currentPhase = _gameState.CurrentPhase;
        if (currentPhase == GameStateController.GamePhase.Preparation)
        {
            int remaining = Mathf.CeilToInt(_gameState.PreparationTimeRemaining);
            _phaseLabel.text = $"{Mathf.Max(0, remaining)}s until next wave";
        }
        else if (currentPhase == GameStateController.GamePhase.Combat)
        {
            int enemiesRemaining = _enemySpawner != null ? _enemySpawner.EnemiesRemaining : 0;
            _phaseLabel.text = $"Round {_gameState.CurrentWave} - Enemies Remaining {enemiesRemaining}";
        }
        else
        {
            _phaseLabel.text = currentPhase.ToString();
        }
    }

    void UpdateHealthBarVisibility(float deltaTime)
    {
        float target = 0f;

        if (_gameState != null)
        {
            switch (_gameState.CurrentPhase)
            {
                case GameStateController.GamePhase.Intro:
                case GameStateController.GamePhase.GameOver:
                    target = 0f;
                    break;
                case GameStateController.GamePhase.Preparation:
                    float duration = _gameState.PreparationDuration;
                    if (duration <= Mathf.Epsilon)
                    {
                        target = 1f;
                    }
                    else
                    {
                        float remaining = _gameState.PreparationTimeRemaining;
                        float progress = Mathf.Clamp01(1f - (remaining / duration));
                        target = Mathf.Clamp01((progress - 0.5f) * 2f);
                    }
                    break;
                default:
                    target = 1f;
                    break;
            }
        }

        if (_healthBarFadeDuration <= Mathf.Epsilon)
        {
            _healthBarVisibility = target;
            return;
        }

        float step = deltaTime / _healthBarFadeDuration;
        _healthBarVisibility = Mathf.MoveTowards(_healthBarVisibility, target, step);
    }

    void UpdateHealthBars()
    {
        UpdateHealthBar(_playerOneGroup, _playerOneFill, _playerOne, _playerOneBaseScale, _healthBarVisibility);
        UpdateHealthBar(_playerTwoGroup, _playerTwoFill, _playerTwo, _playerTwoBaseScale, _healthBarVisibility);
    }

    void UpdateAmmoTexts()
    {
        if (_playerOneAmmoText != null)
        {
            if (_playerOne != null && _playerOne.isActiveAndEnabled)
            {
                _playerOneAmmoText.text = _playerOne.CurrentAmmo.ToString();
            }
            else
            {
                _playerOneAmmoText.text = string.Empty;
            }
        }

        if (_playerTwoAmmoText != null)
        {
            if (_playerTwo != null && _playerTwo.isActiveAndEnabled)
            {
                _playerTwoAmmoText.text = _playerTwo.CurrentAmmo.ToString();
            }
            else
            {
                _playerTwoAmmoText.text = string.Empty;
            }
        }
    }

    void EvaluateFade()
    {
        float rootTarget = 0f;
        float rootDuration = _introFadeDuration;
        float blackoutTarget = 0f;
        float blackoutDuration = _introFadeDuration;

        if (_gameState != null)
        {
            switch (_gameState.CurrentPhase)
            {
                case GameStateController.GamePhase.Intro:
                    rootTarget = 0f;
                    blackoutTarget = 0f;
                    break;
                case GameStateController.GamePhase.GameOver:
                    rootTarget = 0f;
                    blackoutTarget = 1f;
                    rootDuration = _gameOverFadeDuration;
                    blackoutDuration = _gameOverFadeDuration;
                    break;
                default:
                    rootTarget = 1f;
                    blackoutTarget = 0f;
                    break;
            }
        }

        StartRootFade(rootTarget, rootDuration);
        StartBlackoutFade(blackoutTarget, blackoutDuration);
    }

    void StartRootFade(float targetAlpha, float duration)
    {
        if (_rootGroup == null)
        {
            return;
        }

        bool sameTarget = Mathf.Approximately(_rootTargetAlpha, targetAlpha);
        if (sameTarget)
        {
            if (_rootFadeRoutine == null && Mathf.Approximately(_rootGroup.alpha, targetAlpha))
            {
                return;
            }
        }
        else if (_rootFadeRoutine != null)
        {
            StopCoroutine(_rootFadeRoutine);
            _rootFadeRoutine = null;
        }

        _rootTargetAlpha = targetAlpha;

        if (duration <= Mathf.Epsilon)
        {
            SetRootAlpha(targetAlpha);
            _rootFadeRoutine = null;
            return;
        }

        if (_rootFadeRoutine == null)
        {
            _rootFadeRoutine = StartCoroutine(FadeRoot(targetAlpha, duration));
        }
    }

    void StartBlackoutFade(float targetAlpha, float duration)
    {
        if (_blackoutImage == null)
        {
            return;
        }

        bool sameTarget = Mathf.Approximately(_blackoutTargetAlpha, targetAlpha);
        if (sameTarget)
        {
            if (_blackoutFadeRoutine == null && Mathf.Approximately(_blackoutImage.color.a, targetAlpha))
            {
                return;
            }
        }
        else if (_blackoutFadeRoutine != null)
        {
            StopCoroutine(_blackoutFadeRoutine);
            _blackoutFadeRoutine = null;
        }

        _blackoutTargetAlpha = targetAlpha;

        if (duration <= Mathf.Epsilon)
        {
            SetBlackoutAlpha(targetAlpha);
            _blackoutFadeRoutine = null;
            return;
        }

        if (_blackoutFadeRoutine == null)
        {
            _blackoutFadeRoutine = StartCoroutine(FadeBlackout(targetAlpha, duration));
        }
    }

    void ForceRootFade(float targetAlpha, float duration)
    {
        if (_rootGroup == null)
        {
            return;
        }

        if (_rootFadeRoutine != null)
        {
            StopCoroutine(_rootFadeRoutine);
            _rootFadeRoutine = null;
        }

        _rootTargetAlpha = targetAlpha;

        if (duration <= Mathf.Epsilon)
        {
            SetRootAlpha(targetAlpha);
            return;
        }

        _rootFadeRoutine = StartCoroutine(FadeRoot(targetAlpha, duration));
    }

    void ForceBlackoutFade(float targetAlpha, float duration)
    {
        if (_blackoutImage == null)
        {
            return;
        }

        if (_blackoutFadeRoutine != null)
        {
            StopCoroutine(_blackoutFadeRoutine);
            _blackoutFadeRoutine = null;
        }

        _blackoutTargetAlpha = targetAlpha;

        if (duration <= Mathf.Epsilon)
        {
            SetBlackoutAlpha(targetAlpha);
            return;
        }

        _blackoutFadeRoutine = StartCoroutine(FadeBlackout(targetAlpha, duration));
    }

    IEnumerator FadeRoot(float targetAlpha, float duration)
    {
        float startAlpha = _rootGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetRootAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetRootAlpha(targetAlpha);
        _rootFadeRoutine = null;
    }

    IEnumerator FadeBlackout(float targetAlpha, float duration)
    {
        float startAlpha = _blackoutImage != null ? _blackoutImage.color.a : 0f;
        float elapsed = 0f;

        while (_blackoutImage != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetBlackoutAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetBlackoutAlpha(targetAlpha);
        _blackoutFadeRoutine = null;
    }

    void SetRootAlpha(float alpha)
    {
        if (_rootGroup == null)
        {
            return;
        }

        _rootGroup.alpha = alpha;
        bool interactable = alpha >= 0.999f;
        _rootGroup.interactable = interactable;
        _rootGroup.blocksRaycasts = interactable;
    }

    void SetBlackoutAlpha(float alpha)
    {
        if (_blackoutImage == null)
        {
            return;
        }

        Color color = _blackoutImage.color;
        color.a = alpha;
        _blackoutImage.color = color;
        _blackoutImage.raycastTarget = alpha >= 0.001f;
    }

    public void PlayGameOverFade(bool instant)
    {
        if (_rootGroup == null && _blackoutImage == null)
        {
            return;
        }

        if (instant)
        {
            if (_rootFadeRoutine != null)
            {
                StopCoroutine(_rootFadeRoutine);
                _rootFadeRoutine = null;
            }

            if (_blackoutFadeRoutine != null)
            {
                StopCoroutine(_blackoutFadeRoutine);
                _blackoutFadeRoutine = null;
            }

            _rootTargetAlpha = 0f;
            _blackoutTargetAlpha = 1f;
            SetRootAlpha(0f);
            SetBlackoutAlpha(1f);
            return;
        }

        ForceRootFade(0f, _gameOverFadeDuration);
        ForceBlackoutFade(1f, _gameOverFadeDuration);
    }

    static void UpdateHealthBar(CanvasGroup group, RectTransform fill, PlayerController player, Vector3 baseScale, float visibility)
    {
        if (group == null || fill == null)
        {
            return;
        }

        if (player == null || !player.isActiveAndEnabled)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            Vector3 hiddenScale = baseScale;
            hiddenScale.x = 0f;
            fill.localScale = hiddenScale;
            return;
        }

        group.alpha = visibility;
        bool interactable = visibility >= 0.999f;
        group.interactable = interactable;
        group.blocksRaycasts = interactable;

        float ratio = player.MaxHealth <= Mathf.Epsilon ? 0f : player.CurrentHealth / player.MaxHealth;
        ratio = Mathf.Clamp01(ratio);

        Vector3 scale = baseScale;
        scale.x = baseScale.x * ratio;
        fill.localScale = scale;
    }
}









