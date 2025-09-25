using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] float _damage = 15f;
    [SerializeField] float _speed = 20f;

    float _baseDamage;
    float _baseSpeed;
    bool _baseStatsCached;

    public float Damage => _damage;
    public float Speed => _speed;

    void Awake()
    {
        CacheBaseStats();
    }

    void CacheBaseStats()
    {
        if (_baseStatsCached)
        {
            return;
        }

        _baseDamage = _damage;
        _baseSpeed = _speed;
        _baseStatsCached = true;
    }

    public void Configure(float damageMultiplier, float speedMultiplier = 1f)
    {
        CacheBaseStats();

        float safeDamageMultiplier = Mathf.Max(0.01f, damageMultiplier);
        float safeSpeedMultiplier = Mathf.Max(0.01f, speedMultiplier);

        _damage = _baseDamage * safeDamageMultiplier;
        _speed = _baseSpeed * safeSpeedMultiplier;
    }

    void Update()
    {
        transform.position += transform.forward * _speed * Time.deltaTime;
    }
}

