using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerProjectile : MonoBehaviour
{
    [SerializeField] float _damage = 15f;
    [SerializeField] float _speed = 20f;
    [SerializeField, Min(0f)] float _lifetime = 5f;

    public float Damage => _damage;
    public float Speed => _speed;

    void OnEnable()
    {
        if (_lifetime > 0f)
        {
            Destroy(gameObject, _lifetime);
        }
    }

    void Update()
    {
        transform.position += transform.forward * Speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other == null || other.isTrigger)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerController>() != null)
        {
            return;
        }

        EnemyRangedController enemy = other.GetComponentInParent<EnemyRangedController>();
        if (enemy != null)
        {
            Destroy(enemy.gameObject);
        }

        Destroy(gameObject);
    }
}
