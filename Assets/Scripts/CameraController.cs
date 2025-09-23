using UnityEngine;

public class CameraController : MonoBehaviour
{
    const string DEFAULT_PLAYER_ONE_TAG = "Player 1";
    const string DEFAULT_PLAYER_TWO_TAG = "Player 2";

    [Header("Position")]
    [SerializeField] Vector3 _followOffset = new Vector3(0f, 15f, -15f);
    [SerializeField, Min(0f)] float _forwardOffsetWhenClose = 5f;

    [Header("Rotation")]
    [SerializeField, Range(-89f, 89f)] float _minPitchDegrees = 25f;
    [SerializeField, Range(-89f, 89f)] float _maxPitchDegrees = 45f;
    [SerializeField, Min(0.01f)] float _pitchSmoothTime = 0.35f;
    [SerializeField, Min(0.01f)] float _yawSmoothTime = 0.35f;

    [Header("Zoom")]
    [SerializeField, Min(0.01f)] float _minOrthographicSize = 6f;
    [SerializeField, Min(0.01f)] float _maxOrthographicSize = 20f;
    [SerializeField, Min(0.01f)] float _distanceForMaxZoom = 25f;
    [SerializeField, Min(0.01f)] float _zoomSmoothTime = 0.35f;

    [Header("Fly In")]
    [SerializeField, Min(0f)] float _flyInDuration = 1.5f;
    [SerializeField] AnimationCurve _flyInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    Camera _camera;
    Transform _playerOne;
    Transform _playerTwo;

    Vector3 _flyInStartPosition;
    float _flyInStartSize;
    Quaternion _flyInStartRotation;
    Vector3 _staticPosition;

    float _flyInTimer;
    bool _flyInComplete;

    float _pitchVelocity;
    float _yawVelocity;
    float _zoomVelocity;

    float _initialYaw;
    float _initialRoll;

    void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    void Start()
    {
        CacheTargets();
        _flyInStartPosition = transform.position;
        _flyInStartSize = _camera.orthographicSize;
        _flyInStartRotation = transform.rotation;
        _staticPosition = transform.position;

        Vector3 startEuler = transform.eulerAngles;
        _initialYaw = startEuler.y;
        _initialRoll = startEuler.z;
    }

    void LateUpdate()
    {
        if (!EnsureTargets())
        {
            return;
        }

        Vector3 focusPoint = CalculateFocusPoint(out float planarSeparation);
        float desiredSize = CalculateDesiredSize(planarSeparation, out float distanceFactor);
        float desiredPitch = CalculateDesiredPitch(distanceFactor);
        Quaternion initialTargetRotation = Quaternion.Euler(desiredPitch, _initialYaw, _initialRoll);

        if (!_flyInComplete)
        {
            Vector3 targetPosition = CalculateDesiredPosition(focusPoint, initialTargetRotation, distanceFactor);
            ProcessFlyIn(targetPosition, desiredSize, initialTargetRotation);
        }
        else
        {
            FollowTargets(focusPoint, desiredSize, desiredPitch);
        }
    }

    void ProcessFlyIn(Vector3 desiredPosition, float desiredSize, Quaternion targetRotation)
    {
        if (_flyInDuration <= Mathf.Epsilon)
        {
            transform.SetPositionAndRotation(desiredPosition, targetRotation);
            _camera.orthographicSize = desiredSize;
            _staticPosition = desiredPosition;
            _flyInComplete = true;
            return;
        }

        _flyInTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_flyInTimer / _flyInDuration);
        float eased = _flyInCurve != null ? _flyInCurve.Evaluate(t) : t;

        transform.position = Vector3.LerpUnclamped(_flyInStartPosition, desiredPosition, eased);
        transform.rotation = Quaternion.SlerpUnclamped(_flyInStartRotation, targetRotation, eased);
        _camera.orthographicSize = Mathf.LerpUnclamped(_flyInStartSize, desiredSize, eased);

        if (t >= 1f)
        {
            _flyInComplete = true;
            _pitchVelocity = 0f;
            _yawVelocity = 0f;
            _zoomVelocity = 0f;
            _staticPosition = desiredPosition;
            transform.SetPositionAndRotation(desiredPosition, targetRotation);
        }
    }

    void FollowTargets(Vector3 focusPoint, float desiredSize, float desiredPitch)
    {
        transform.position = _staticPosition;

        Vector3 planarToFocus = new Vector3(focusPoint.x - _staticPosition.x, 0f, focusPoint.z - _staticPosition.z);
        float targetYaw = planarToFocus.sqrMagnitude <= Mathf.Epsilon
            ? transform.eulerAngles.y
            : Mathf.Atan2(planarToFocus.x, planarToFocus.z) * Mathf.Rad2Deg;

        float currentYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref _yawVelocity, _yawSmoothTime);
        float currentPitch = Mathf.SmoothDampAngle(transform.eulerAngles.x, desiredPitch, ref _pitchVelocity, _pitchSmoothTime);
        transform.rotation = Quaternion.Euler(currentPitch, currentYaw, _initialRoll);

        _camera.orthographicSize = Mathf.SmoothDamp(_camera.orthographicSize, desiredSize, ref _zoomVelocity, _zoomSmoothTime);
    }

    Vector3 CalculateDesiredPosition(Vector3 focusPoint, Quaternion rotation, float distanceFactor)
    {
        float forwardOffset = Mathf.Lerp(_forwardOffsetWhenClose, 0f, distanceFactor);
        Vector3 localOffset = _followOffset + Vector3.forward * forwardOffset;
        return focusPoint + rotation * localOffset;
    }

    float CalculateDesiredPitch(float distanceFactor)
    {
        float minPitch = Mathf.Min(_minPitchDegrees, _maxPitchDegrees);
        float maxPitch = Mathf.Max(_minPitchDegrees, _maxPitchDegrees);
        return Mathf.Lerp(maxPitch, minPitch, distanceFactor);
    }

    Vector3 CalculateFocusPoint(out float planarSeparation)
    {
        if (_playerOne != null && _playerTwo != null)
        {
            Vector3 p1 = _playerOne.position;
            Vector3 p2 = _playerTwo.position;
            planarSeparation = Vector2.Distance(new Vector2(p1.x, p1.z), new Vector2(p2.x, p2.z));
            return (p1 + p2) * 0.5f;
        }

        Transform soloTarget = _playerOne != null ? _playerOne : _playerTwo;
        planarSeparation = 0f;
        return soloTarget != null ? soloTarget.position : transform.position - transform.rotation * _followOffset;
    }

    float CalculateDesiredSize(float planarSeparation, out float distanceFactor)
    {
        if (_distanceForMaxZoom <= Mathf.Epsilon)
        {
            distanceFactor = 0f;
            return _minOrthographicSize;
        }

        distanceFactor = Mathf.Clamp01(planarSeparation / _distanceForMaxZoom);
        return Mathf.Lerp(_minOrthographicSize, _maxOrthographicSize, distanceFactor);
    }

    bool EnsureTargets()
    {
        if (_playerOne == null || _playerTwo == null)
        {
            CacheTargets();
        }

        return _playerOne != null || _playerTwo != null;
    }

    void CacheTargets()
    {
        if ((_playerOne == null || !_playerOne.gameObject.activeInHierarchy) && !string.IsNullOrEmpty(DEFAULT_PLAYER_ONE_TAG))
        {
            GameObject p1 = GameObject.FindGameObjectWithTag(DEFAULT_PLAYER_ONE_TAG);
            _playerOne = p1 != null ? p1.transform : null;
        }

        if ((_playerTwo == null || !_playerTwo.gameObject.activeInHierarchy) && !string.IsNullOrEmpty(DEFAULT_PLAYER_TWO_TAG))
        {
            GameObject p2 = GameObject.FindGameObjectWithTag(DEFAULT_PLAYER_TWO_TAG);
            _playerTwo = p2 != null ? p2.transform : null;
        }
    }

    public Vector3 GetForward()
    {
        Vector3 forward = _camera.transform.forward;
        forward.y = 0f;
        forward.Normalize();
        return forward;
    }

    public Vector3 GetRight()
    {
        Vector3 right = _camera.transform.right;
        right.y = 0f;
        right.Normalize();
        return right;
    }
}
