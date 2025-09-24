using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StartSceneController : MonoBehaviour
{
    [Header("Title")]
    [SerializeField] TMP_Text _titleText;
    [SerializeField] string _fullTitle = "ARENA.EXE";
    [SerializeField, Min(0f)] float _charRevealDelay = 0.08f;
    [SerializeField, Min(0f)] float _extensionHoldDelay = 1.25f;
    [SerializeField, Min(0f)] float _extensionPauseDelay = 0.35f;
    [SerializeField] string[] _alternateExtensions;

    [Header("Buttons")]
    [SerializeField] CanvasGroup[] _buttonCanvasGroups;
    [SerializeField, Min(0f)] float _buttonFadeDuration = 0.6f;
    [SerializeField, Min(0f)] float _buttonFadeStagger = 0.15f;

    readonly List<string> _extensionCycle = new();
    Coroutine _extensionLoopRoutine;
    string _baseTitle = string.Empty;
    string _defaultExtension = string.Empty;
    string _currentExtension = string.Empty;
    int _currentExtensionIndex;

    void Awake()
    {
        ParseTitle();
        BuildExtensionCycle();
        PrepareUI();
    }

    void OnEnable()
    {
        ParseTitle();
        BuildExtensionCycle();
        PrepareUI();
        StartCoroutine(PlayIntroSequence());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        _extensionLoopRoutine = null;
    }

    void ParseTitle()
    {
        if (string.IsNullOrEmpty(_fullTitle))
        {
            _baseTitle = string.Empty;
            _defaultExtension = string.Empty;
            return;
        }

        int dotIndex = _fullTitle.IndexOf('.');
        if (dotIndex < 0 || dotIndex == _fullTitle.Length - 1)
        {
            _baseTitle = _fullTitle;
            _defaultExtension = string.Empty;
            return;
        }

        _baseTitle = _fullTitle.Substring(0, dotIndex);
        _defaultExtension = _fullTitle.Substring(dotIndex);
    }

    void BuildExtensionCycle()
    {
        _extensionCycle.Clear();

        string formattedDefault = FormatExtension(_defaultExtension);
        if (!string.IsNullOrEmpty(formattedDefault))
        {
            _extensionCycle.Add(formattedDefault);
        }

        if (_alternateExtensions != null)
        {
            foreach (string raw in _alternateExtensions)
            {
                string formatted = FormatExtension(raw);
                if (string.IsNullOrEmpty(formatted))
                {
                    continue;
                }

                _extensionCycle.Add(formatted);
            }
        }

        if (_extensionCycle.Count == 0)
        {
            _extensionCycle.Add(string.Empty);
        }

        _currentExtensionIndex = 0;
        _currentExtension = _extensionCycle[0];
    }

    static string FormatExtension(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string trimmed = raw.Trim();
        return trimmed.StartsWith(".") ? trimmed : "." + trimmed;
    }

    void PrepareUI()
    {
        if (_titleText != null)
        {
            _titleText.text = string.Empty;
        }

        if (_buttonCanvasGroups == null)
        {
            return;
        }

        foreach (CanvasGroup canvasGroup in _buttonCanvasGroups)
        {
            if (canvasGroup == null)
            {
                continue;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    IEnumerator PlayIntroSequence()
    {
        if (_titleText != null)
        {
            yield return StartCoroutine(TypeTitleOnce());
        }

        yield return StartCoroutine(FadeButtonsIn());

        if (_titleText != null && _extensionCycle.Count > 0)
        {
            _extensionLoopRoutine = StartCoroutine(AnimateExtensionLoop());
        }
    }

    IEnumerator TypeTitleOnce()
    {
        if (_titleText == null)
        {
            yield break;
        }

        string target = _baseTitle + _currentExtension;
        _titleText.text = string.Empty;

        for (int i = 0; i < target.Length; i++)
        {
            _titleText.text = target.Substring(0, i + 1);
            yield return new WaitForSeconds(_charRevealDelay);
        }

        _titleText.text = target;
    }

    IEnumerator FadeButtonsIn()
    {
        if (_buttonCanvasGroups == null || _buttonCanvasGroups.Length == 0)
        {
            yield break;
        }

        for (int i = 0; i < _buttonCanvasGroups.Length; i++)
        {
            CanvasGroup canvasGroup = _buttonCanvasGroups[i];
            if (canvasGroup == null)
            {
                continue;
            }

            yield return StartCoroutine(FadeCanvasGroup(canvasGroup, 1f));

            if (i < _buttonCanvasGroups.Length - 1)
            {
                yield return new WaitForSeconds(_buttonFadeStagger);
            }
        }
    }

    IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < _buttonFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _buttonFadeDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        bool isVisible = Mathf.Approximately(targetAlpha, 1f);
        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
    }

    IEnumerator AnimateExtensionLoop()
    {
        if (_titleText == null || _extensionCycle.Count == 0)
        {
            yield break;
        }

        yield return new WaitForSeconds(_extensionHoldDelay);

        while (true)
        {
            yield return StartCoroutine(RemoveExtension(_currentExtension));
            yield return new WaitForSeconds(_extensionPauseDelay);

            if (_extensionCycle.Count > 1)
            {
                _currentExtensionIndex = (_currentExtensionIndex + 1) % _extensionCycle.Count;
                _currentExtension = _extensionCycle[_currentExtensionIndex];
            }

            yield return StartCoroutine(RevealExtension(_currentExtension));
            yield return new WaitForSeconds(_extensionHoldDelay);
        }
    }

    IEnumerator RemoveExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            _titleText.text = _baseTitle;
            yield return null;
            yield break;
        }

        for (int i = extension.Length; i >= 0; i--)
        {
            _titleText.text = _baseTitle + extension.Substring(0, i);
            yield return new WaitForSeconds(_charRevealDelay);
        }
    }

    IEnumerator RevealExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            _titleText.text = _baseTitle;
            yield break;
        }

        for (int i = 1; i <= extension.Length; i++)
        {
            _titleText.text = _baseTitle + extension.Substring(0, i);
            yield return new WaitForSeconds(_charRevealDelay);
        }
    }
}
