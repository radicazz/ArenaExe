using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HealthPickupController : MonoBehaviour
{
    public sealed class HealthGrantedEventArgs : EventArgs
    {
        public HealthGrantedEventArgs(PlayerController player, float healAmount)
        {
            Player = player;
            HealAmount = healAmount;
        }

        public PlayerController Player { get; }
        public float HealAmount { get; }
    }

    public static event EventHandler<HealthGrantedEventArgs> HealthGranted;

    [Header("Pickup Settings")]
    [SerializeField, Min(0f)] float _healAmount = 25f;

    [Header("Presentation")]
    [SerializeField, Min(0f)] float _rotationSpeed = 50f;
    [SerializeField, Min(0f)] float _bobAmplitude = 0.35f;
    [SerializeField, Min(0f)] float _bobFrequency = 1.1f;

    Vector3 _initialWorldPosition;
    Vector3 _initialLocalPosition;
    bool _collected;

    void Awake()
    {
        Collider pickupCollider = GetComponent<Collider>();
        pickupCollider.isTrigger = true;
        _initialWorldPosition = transform.position;
        _initialLocalPosition = transform.localPosition;
    }

    void Update()
    {
        if (_collected)
        {
            return;
        }

        transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.World);

        float bobOffset = Mathf.Sin(Time.time * _bobFrequency) * _bobAmplitude;
        if (transform.parent == null)
        {
            Vector3 position = _initialWorldPosition;
            position.y += bobOffset;
            transform.position = position;
        }
        else
        {
            Vector3 localPosition = _initialLocalPosition;
            localPosition.y += bobOffset;
            transform.localPosition = localPosition;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_collected)
        {
            return;
        }

        if (!other.CompareTag("Player 1") && !other.CompareTag("Player 2"))
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            return;
        }

        _collected = true;
        HealthGranted?.Invoke(this, new HealthGrantedEventArgs(player, _healAmount));
        Destroy(gameObject);
    }

    public float HealAmount => _healAmount;
}
