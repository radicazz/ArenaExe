using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyRangedController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform _leftBarrel;
    [SerializeField] Transform _rightBarrel;
    [SerializeField] GameObject _projectilePrefab;

    [Header("Combat")]
    [SerializeField, Min(0.05f)] float _fireInterval = 1.5f;
    [SerializeField, Min(0f)] float _fireRange = 20f;
    [SerializeField, Min(0f)] float _rangeMargin = 1.25f;
    [SerializeField] LayerMask _lineOfSightMask = ~0;

    [Header("Health")]
    [SerializeField, Min(1f)] float _maxHealth = 200f;
    [SerializeField, Min(0f)] float _currentHealth = 200f;

    [Header("Movement")]
    [SerializeField, Min(0f)] float _moveSpeed = 5f;
    [SerializeField, Min(0f)] float _acceleration = 12f;
    [SerializeField, Min(0f)] float _stopTolerance = 0.35f;
    [SerializeField, Min(0f)] float _avoidanceRadius = 1.5f;
    [SerializeField, Min(0f)] float _avoidanceStrength = 2.5f;

    [Header("Hover")]
    [SerializeField, Min(0f)] float _baseHoverHeight = 1.4f;
    [SerializeField, Min(0f)] float _hoverMoveBoost = 0.6f;
    [SerializeField, Min(0f)] float _hoverBobAmplitude = 0.2f;
    [SerializeField, Min(0f)] float _hoverBobFrequency = 1.4f;

    Rigidbody _rigidbody;
    Collider[] _selfColliders;

    float _fireTimer;
    bool _flip;
    Vector3 _currentVelocity;
    float _hoverPhase;
    float _initialHeight;
    bool _isDead;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.useGravity = false;
        _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;

        _selfColliders = GetComponentsInChildren<Collider>();
        _initialHeight = transform.position.y;
        _maxHealth = Mathf.Max(1f, _maxHealth);
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        if (_currentHealth <= 0f)
        {
            _currentHealth = _maxHealth;
        }
        _isDead = false;
    }

    void OnEnable()
    {
        _fireTimer = _fireInterval;
        _flip = false;
        _currentVelocity = Vector3.zero;
        _currentHealth = _maxHealth;
        _isDead = false;
    }

    void Update()
    {
        if (_isDead)
        {
            return;
        }
        GameStateController state = GameStateController.Instance;
        GameStateController.GamePhase phase = state != null ? state.CurrentPhase : GameStateController.GamePhase.Intro;
        bool canEngage = phase == GameStateController.GamePhase.Combat;

        PlayerController target = FindClosestPlayer();
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = target.transform.position;
        FaceTarget(targetPosition);
        AimBarrel(_leftBarrel, targetPosition);
        AimBarrel(_rightBarrel, targetPosition);

        MoveTowardTarget(targetPosition, canEngage);

        if (canEngage)
        {
            HandleShooting(target, targetPosition);
        }
    }

    PlayerController FindClosestPlayer()
    {
        IReadOnlyList<PlayerController> players = GameStateController.Instance?.Players;
        if (players == null)
        {
            return null;
        }

        PlayerController closest = null;
        float bestSqrDistance = float.MaxValue;

        foreach (PlayerController player in players)
        {
            if (player == null || !player.IsAlive)
            {
                continue;
            }

            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                closest = player;
            }
        }

        return closest;
    }

    void FaceTarget(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < Mathf.Epsilon)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(toTarget, Vector3.up);
    }

    void AimBarrel(Transform barrel, Vector3 targetPosition)
    {
        if (barrel == null)
        {
            return;
        }

        barrel.LookAt(targetPosition, Vector3.up);
    }

    void MoveTowardTarget(Vector3 targetPosition, bool canEngage)
    {
        Vector3 position = transform.position;
        Vector3 planarToTarget = targetPosition - position;
        planarToTarget.y = 0f;

        Vector3 desiredVelocity = Vector3.zero;
        if (canEngage)
        {
            float distance = planarToTarget.magnitude;
            float desiredRange = Mathf.Max(0f, _fireRange - _rangeMargin);

            if (distance > desiredRange + _stopTolerance)
            {
                desiredVelocity = planarToTarget.normalized * _moveSpeed;
            }
            else if (distance < desiredRange - _stopTolerance)
            {
                desiredVelocity = -planarToTarget.normalized * (_moveSpeed * 0.5f);
            }
        }

        desiredVelocity += ComputeAvoidanceForce(position);
        desiredVelocity = Vector3.ClampMagnitude(desiredVelocity, _moveSpeed);

        _currentVelocity = Vector3.MoveTowards(_currentVelocity, desiredVelocity, _acceleration * Time.deltaTime);

        Vector3 nextPosition = position + _currentVelocity * Time.deltaTime;
        nextPosition.y = ComputeHoverHeight();

        _rigidbody.MovePosition(nextPosition);
    }

    Vector3 ComputeAvoidanceForce(Vector3 currentPosition)
    {
        if (_avoidanceRadius <= Mathf.Epsilon)
        {
            return Vector3.zero;
        }

        Vector3 separation = Vector3.zero;
        float sqrRadius = _avoidanceRadius * _avoidanceRadius;

        EnemyRangedController[] enemies = FindObjectsByType<EnemyRangedController>(FindObjectsSortMode.None);
        foreach (EnemyRangedController enemy in enemies)
        {
            if (enemy == null || enemy == this)
            {
                continue;
            }

            Vector3 offset = currentPosition - enemy.transform.position;
            offset.y = 0f;
            float sqrMag = offset.sqrMagnitude;
            if (sqrMag < Mathf.Epsilon || sqrMag > sqrRadius)
            {
                continue;
            }

            float falloff = 1f - Mathf.Sqrt(sqrMag) / _avoidanceRadius;
            separation += offset.normalized * (_avoidanceStrength * falloff);
        }

        return separation;
    }

    float ComputeHoverHeight()
    {
        float moveFactor = Mathf.Clamp01(_currentVelocity.magnitude / Mathf.Max(_moveSpeed, 0.01f));
        _hoverPhase += Time.deltaTime * Mathf.Lerp(_hoverBobFrequency, _hoverBobFrequency * 1.6f, moveFactor);
        float bob = Mathf.Sin(_hoverPhase) * _hoverBobAmplitude * Mathf.Lerp(0.15f, 1f, moveFactor);
        float heightOffset = Mathf.Lerp(0f, _hoverMoveBoost, moveFactor);
        return _initialHeight + _baseHoverHeight + heightOffset + bob;
    }

    public void TakeDamage(float amount)
    {
        if (_isDead)
        {
            return;
        }

        _currentHealth = Mathf.Clamp(_currentHealth - amount, 0f, _maxHealth);
        Debug.Log($"[EnemyRangedController] {gameObject.name} took {amount} damage. Remaining {_currentHealth}.", this);

        if (_currentHealth <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        _isDead = true;
        Debug.Log($"[EnemyRangedController] {gameObject.name} destroyed.", this);
        Destroy(gameObject);
    }

    void HandleShooting(PlayerController target, Vector3 targetPosition)
    {
        if (_projectilePrefab == null)
        {
            return;
        }

        Vector3 toTarget = targetPosition - transform.position;
        float sqrDistance = toTarget.sqrMagnitude;
        float maxSqrRange = _fireRange * _fireRange;

        if (sqrDistance > maxSqrRange)
        {
            _fireTimer = Mathf.Min(_fireTimer, 0f);
            return;
        }

        if (!HasLineOfSight(target, targetPosition))
        {
            _fireTimer = Mathf.Min(_fireTimer, 0f);
            return;
        }

        _fireTimer -= Time.deltaTime;
        if (_fireTimer > 0f)
        {
            return;
        }

        FireProjectile(_flip ? _rightBarrel : _leftBarrel, targetPosition);
        _flip = !_flip;
        _fireTimer = _fireInterval;
    }

    bool HasLineOfSight(PlayerController target, Vector3 targetPosition)
    {
        Vector3 origin = transform.position + Vector3.up * 0.35f;
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            return true;
        }

        direction /= distance;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, _lineOfSightMask, QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        Collider closestCollider = null;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            if (IsSelfCollider(hit.collider))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestCollider = hit.collider;
            }
        }

        if (closestCollider == null)
        {
            return true;
        }

        return target != null && closestCollider.transform.IsChildOf(target.transform);
    }

    bool IsSelfCollider(Collider collider)
    {
        if (_selfColliders == null)
        {
            return false;
        }

        for (int i = 0; i < _selfColliders.Length; i++)
        {
            if (_selfColliders[i] == null)
            {
                continue;
            }

            if (ReferenceEquals(_selfColliders[i], collider))
            {
                return true;
            }
        }

        return false;
    }

    void FireProjectile(Transform barrel, Vector3 targetPosition)
    {
        if (barrel == null)
        {
            return;
        }

        Vector3 direction = targetPosition - barrel.position;
        if (direction.sqrMagnitude < Mathf.Epsilon)
        {
            return;
        }

        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Destroy(Instantiate(_projectilePrefab, barrel.position, rotation), 5f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Projectile Player"))
        {
            return;
        }

        Debug.Log($"[EnemyRangedController] {gameObject.name} hit by player projectile.", this);

        var playerProjectile = other.GetComponent<PlayerProjectile>();
        TakeDamage(playerProjectile.Damage);
        Destroy(other.gameObject);
    }
}
