using System.Collections.Generic;
using System.Net.Security;
using UnityEngine;

public class EnemyRangedController : MonoBehaviour
{
    [SerializeField] Transform _leftBarrel;
    [SerializeField] Transform _rightBarrel;
    [SerializeField] GameObject _projectilePrefab;
    [SerializeField, Min(0.05f)] float _fireInterval = 1.5f;
    [SerializeField, Min(0f)] float _fireRange = 20f;

    float _fireTimer;
    bool _flip;

    void OnEnable()
    {
        _fireTimer = _fireInterval;
        _flip = false;
    }

    void Update()
    {
        PlayerController target = FindClosestPlayer();
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = target.transform.position;
        FaceTarget(targetPosition);
        AimBarrel(_leftBarrel, targetPosition);
        AimBarrel(_rightBarrel, targetPosition);
        HandleShooting(targetPosition);
    }

    PlayerController FindClosestPlayer()
    {
        IReadOnlyList<PlayerController> players = GameState.Instance?.Players;
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

    void HandleShooting(Vector3 targetPosition)
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

        _fireTimer -= Time.deltaTime;
        if (_fireTimer > 0f)
        {
            return;
        }

        FireProjectile(_flip ? _rightBarrel : _leftBarrel, targetPosition);
        _flip = !_flip;

        _fireTimer = _fireInterval;
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
        Destroy(Instantiate(_projectilePrefab, barrel.position, rotation), 5);
    }
}
