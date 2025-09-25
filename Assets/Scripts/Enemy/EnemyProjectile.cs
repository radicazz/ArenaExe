using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] float _damage = 15f;
    [SerializeField] float _speed = 20f;

    public float Damage => _damage;
    public float Speed => _speed;

    void Update()
    {
        transform.position += transform.forward * Speed * Time.deltaTime;
    }
}
