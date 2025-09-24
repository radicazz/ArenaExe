using UnityEngine;

public class PlayerController : MonoBehaviour
{
    Rigidbody _rigidbody;
    Animator _animator;

    Vector2 _movementInput;

    [SerializeField] float _moveSpeed = 5f;

    [Header("Health")]
    [SerializeField, Min(1f)] float _maxHealth = 100f;
    [SerializeField, Min(0f)] float _startingHealth = 100f;

    float _currentHealth;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsAlive => _currentHealth > 0f;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponentInChildren<Animator>();
        _movementInput = Vector2.zero;
        _currentHealth = Mathf.Clamp(_startingHealth, 0f, _maxHealth);
    }

    void OnEnable()
    {
        HealthPickupController.HealthGranted += HandleHealthGranted;
        GameState.Instance?.RegisterPlayer(this);
    }

    void OnDisable()
    {
        HealthPickupController.HealthGranted -= HandleHealthGranted;
        GameState.Instance?.UnregisterPlayer(this);
    }

    void Update()
    {
        if (!IsAlive)
        {
            _movementInput = Vector2.zero;
            return;
        }

        if (CompareTag("Player 1"))
        {
            float x = 0f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            float y = 0f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;
            _movementInput = new Vector2(x, y);
        }
        else if (CompareTag("Player 2"))
        {
            float x = 0f;
            if (Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            float y = 0f;
            if (Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            _movementInput = new Vector2(x, y);
        }
        else
        {
            _movementInput = Vector2.zero;
        }
    }

    void FixedUpdate()
    {
        if (_movementInput.sqrMagnitude > 0f && IsAlive)
        {
            Vector3 movement = transform.right * _movementInput.x + transform.forward * _movementInput.y;
            if (movement != Vector3.zero)
            {
                movement.Normalize();
            }

            _rigidbody.linearVelocity = movement * _moveSpeed;
            _animator.SetInteger("move", 1);
        }
        else
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _movementInput = Vector2.zero;
            _animator.SetInteger("move", 0);
        }
    }

    void HandleHealthGranted(object sender, HealthPickupController.HealthGrantedEventArgs e)
    {
        if (e.Player != this)
        {
            return;
        }

        AddHealth(e.HealAmount);
        GameState.Instance?.RecordHealthPickupUsed(this);
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive)
        {
            return;
        }

        float damage = Mathf.Max(0f, amount);
        if (damage <= 0f)
        {
            return;
        }

        _currentHealth = Mathf.Clamp(_currentHealth - damage, 0f, _maxHealth);
        GameState.Instance?.RecordDamageTaken(this, damage);

        if (!IsAlive)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _movementInput = Vector2.zero;
            _animator.SetInteger("move", 0);
        }
    }

    void AddHealth(float amount)
    {
        _currentHealth = Mathf.Clamp(_currentHealth + amount, 0f, _maxHealth);
    }
}
