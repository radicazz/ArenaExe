using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField, Min(0f)] float _lifeTime = 5f;

    Rigidbody _rigidbody;
    float _damage;
    float _speed;
    Vector3 _direction;
    bool _initialized;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.useGravity = false;
    }

    void OnEnable()
    {
        if (_lifeTime > 0f)
        {
            Invoke(nameof(DestroySelf), _lifeTime);
        }
    }

    void OnDisable()
    {
        CancelInvoke(nameof(DestroySelf));
    }

    void FixedUpdate()
    {
        if (!_initialized)
        {
            return;
        }

        _rigidbody.linearVelocity = _direction * _speed;
    }

    public void Initialize(float damage, float speed, Vector3 direction)
    {
        _damage = Mathf.Max(0f, damage);
        _speed = Mathf.Max(0f, speed);
        _direction = direction.normalized;
        _initialized = true;
        _rigidbody.linearVelocity = _direction * _speed;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player 1") && !other.CompareTag("Player 2"))
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(_damage);
            DestroySelf();
        }
    }

    void DestroySelf()
    {
        Destroy(gameObject);
    }
}
