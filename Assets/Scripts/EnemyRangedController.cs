using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyRangedController : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField, Min(0f)] float _engageRange = 25f;
    [SerializeField, Min(0f)] float _stoppingDistance = 8f;
    [SerializeField, Min(0f)] float _rotationSpeed = 8f;

    [Header("Movement")]
    [SerializeField, Min(0f)] float _baseMoveSpeed = 3.5f;
    [SerializeField, Min(0f)] float _moveSpeedGrowthPerWave = 0.4f;

    [Header("Weapons")]
    [SerializeField] Transform _leftBarrel;
    [SerializeField] Transform _rightBarrel;
    [SerializeField] GameObject _projectilePrefab;
    [SerializeField, Min(0f)] float _baseFireInterval = 1.6f;
    [SerializeField, Min(0f)] float _fireIntervalReductionPerWave = 0.12f;
    [SerializeField, Min(0f)] float _baseDamage = 10f;
    [SerializeField, Min(0f)] float _damageGrowthPerWave = 2f;
    [SerializeField, Min(0f)] float _baseProjectileSpeed = 14f;
    [SerializeField, Min(0f)] float _projectileSpeedGrowthPerWave = 1.25f;

    Rigidbody _rigidbody;
    PlayerController _currentTarget;
    Vector3 _desiredVelocity;
    float _fireTimer;
    float _currentMoveSpeed;
    float _currentFireInterval;
    float _currentDamage;
    float _currentProjectileSpeed;
    bool _useLeftBarrel;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _useLeftBarrel = Random.value > 0.5f;
    }

    void OnEnable()
    {
        _fireTimer = Random.Range(0f, _baseFireInterval);
    }

    void Update()
    {
        AcquireTarget();
        DriveMovement();
        HandleShooting();
    }

    void FixedUpdate()
    {
        _rigidbody.linearVelocity = _desiredVelocity;
    }

    public void Initialize(int waveNumber)
    {
        int clampedWave = Mathf.Max(1, waveNumber);
        _currentMoveSpeed = _baseMoveSpeed + (clampedWave - 1) * _moveSpeedGrowthPerWave;
        _currentFireInterval = Mathf.Max(0.35f, _baseFireInterval - (clampedWave - 1) * _fireIntervalReductionPerWave);
        _currentDamage = _baseDamage + (clampedWave - 1) * _damageGrowthPerWave;
        _currentProjectileSpeed = _baseProjectileSpeed + (clampedWave - 1) * _projectileSpeedGrowthPerWave;
        _fireTimer = _currentFireInterval;
    }

    void AcquireTarget()
    {
        _currentTarget = null;

        IReadOnlyList<PlayerController> players = GameState.Instance?.Players;
        if (players == null)
        {
            return;
        }

        float bestDistance = float.MaxValue;
        foreach (PlayerController player in players)
        {
            if (player == null || !player.IsAlive)
            {
                continue;
            }

            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance < bestDistance)
            {
                bestDistance = sqrDistance;
                _currentTarget = player;
            }
        }
    }

    void DriveMovement()
    {
        if (_currentTarget == null)
        {
            _desiredVelocity = Vector3.zero;
            return;
        }

        Vector3 toTarget = _currentTarget.transform.position - transform.position;
        Vector3 planarToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
        float distance = planarToTarget.magnitude;

        if (planarToTarget.sqrMagnitude > Mathf.Epsilon)
        {
            Vector3 direction = planarToTarget.normalized;
            Quaternion goalRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, goalRotation, _rotationSpeed * Time.deltaTime);
        }

        if (distance > _stoppingDistance)
        {
            Vector3 moveDirection = planarToTarget.normalized;
            _desiredVelocity = moveDirection * _currentMoveSpeed;
        }
        else
        {
            _desiredVelocity = Vector3.zero;
        }
    }

    void HandleShooting()
    {
        if (_currentTarget == null)
        {
            return;
        }

        Vector3 toTarget = _currentTarget.transform.position - transform.position;
        float planarDistance = new Vector3(toTarget.x, 0f, toTarget.z).magnitude;

        _fireTimer -= Time.deltaTime;
        if (_fireTimer > 0f)
        {
            return;
        }

        if (planarDistance > _engageRange)
        {
            _fireTimer = 0f;
            return;
        }

        FireProjectile();
        _fireTimer = _currentFireInterval;
    }

    void FireProjectile()
    {
        if (_projectilePrefab == null)
        {
            return;
        }

        Transform barrel = SelectBarrel();
        if (barrel == null)
        {
            return;
        }

        Vector3 targetPoint = _currentTarget.transform.position + Vector3.up * 1.1f;
        Vector3 direction = (targetPoint - barrel.position).normalized;

        GameObject projectileInstance = Instantiate(_projectilePrefab, barrel.position, Quaternion.LookRotation(direction, Vector3.up));
        EnemyProjectile projectile = projectileInstance.GetComponent<EnemyProjectile>();
        if (projectile != null)
        {
            projectile.Initialize(_currentDamage, _currentProjectileSpeed, direction);
        }
        else if (projectileInstance.TryGetComponent<Rigidbody>(out Rigidbody projectileBody))
        {
            projectileBody.linearVelocity = direction * _currentProjectileSpeed;
        }
    }

    Transform SelectBarrel()
    {
        Transform barrel = _useLeftBarrel ? _leftBarrel : _rightBarrel;
        _useLeftBarrel = !_useLeftBarrel;

        if (barrel == null)
        {
            barrel = _leftBarrel != null ? _leftBarrel : _rightBarrel;
        }

        return barrel;
    }
}

