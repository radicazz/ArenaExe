using UnityEngine;

public class PlayerController : MonoBehaviour
{
    const float ENEMY_REFRESH_INTERVAL = 0.25f;

    Rigidbody _rigidbody;
    Animator _animator;

    Vector2 _movementInput;

    [SerializeField] float _moveSpeed = 5f;

    [Header("Health")]
    [SerializeField, Min(1f)] float _maxHealth = 100f;
    [SerializeField, Min(1f)] float _currentHealth = 100f;

    [SerializeField] Light _spotLight;

    [Header("Orientation")]
    [SerializeField, Min(0f)] float _turnSpeedDegreesPerSecond = 540f;

    Transform _closestEnemy;
    float _enemyRefreshTimer;
    Quaternion _movementFrame;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsAlive => _currentHealth > 0f;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        _animator = GetComponentInChildren<Animator>();

        _movementInput = Vector2.zero;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);

        Vector3 planarForward = transform.forward;
        planarForward.y = 0f;
        if (planarForward.sqrMagnitude <= Mathf.Epsilon)
        {
            planarForward = Vector3.forward;
        }
        planarForward.Normalize();
        _movementFrame = Quaternion.LookRotation(planarForward, Vector3.up);
    }

    void OnEnable()
    {
        HealthPickupController.HealthGranted += HandleHealthGranted;
        GameStateController.Instance?.RegisterPlayer(this);
    }

    void OnDisable()
    {
        HealthPickupController.HealthGranted -= HandleHealthGranted;
        GameStateController.Instance?.UnregisterPlayer(this);
    }

    void Update()
    {
        if (!IsAlive)
        {
            _movementInput = Vector2.zero;
            return;
        }

        GameStateController state = GameStateController.Instance;
        GameStateController.GamePhase phase = state != null ? state.CurrentPhase : GameStateController.GamePhase.Intro;
        bool canMove = phase == GameStateController.GamePhase.Preparation || phase == GameStateController.GamePhase.Combat;

        if (canMove)
        {
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
        else
        {
            _movementInput = Vector2.zero;
        }

        UpdateFacing();
    }

    void FixedUpdate()
    {
        if (!IsAlive)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _movementInput = Vector2.zero;
            _animator.SetInteger("move", 2);
            return;
        }

        if (_movementInput.sqrMagnitude > 0f)
        {
            Vector3 inputVector = new Vector3(_movementInput.x, 0f, _movementInput.y);
            Vector3 movement = _movementFrame * inputVector;
            movement.y = 0f;

            if (movement.sqrMagnitude > 1f)
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
        GameStateController.Instance?.RecordHealthPickupUsed(this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Projectile Enemy"))
        {
            return;
        }

        EnemyProjectile projectile = other.GetComponent<EnemyProjectile>();
        if (projectile != null)
        {
            TakeDamage(projectile.Damage);
            Destroy(other.gameObject);
        }
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

        Debug.Log($"[PlayerController] {gameObject.name} took {damage} damage.", this);

        _currentHealth = Mathf.Clamp(_currentHealth - damage, 0f, _maxHealth);
        GameStateController.Instance?.RecordDamageTaken(this, damage);

        if (!IsAlive)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _movementInput = Vector2.zero;
            _animator.SetInteger("move", 2);
            _spotLight.GetComponent<Light>().range = 0f;
            Debug.Log($"[PlayerController] {gameObject.name} has died.", this);
        }
    }

    void AddHealth(float amount)
    {
        _currentHealth = Mathf.Clamp(_currentHealth + amount, 0f, _maxHealth);
    }

    void UpdateFacing()
    {
        _enemyRefreshTimer -= Time.deltaTime;

        if (_closestEnemy == null || !_closestEnemy.gameObject.activeInHierarchy || _enemyRefreshTimer <= 0f)
        {
            _closestEnemy = FindClosestEnemy();
            _enemyRefreshTimer = ENEMY_REFRESH_INTERVAL;
        }

        Vector3 desiredDirection;
        if (_closestEnemy != null)
        {
            desiredDirection = _closestEnemy.position - transform.position;
        }
        else
        {
            desiredDirection = -new Vector3(transform.position.x, 0f, transform.position.z);
        }

        desiredDirection.y = 0f;

        if (desiredDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(desiredDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _turnSpeedDegreesPerSecond * Time.deltaTime);
    }

    Transform FindClosestEnemy()
    {
        EnemyRangedController[] enemies = FindObjectsByType<EnemyRangedController>(FindObjectsSortMode.None);
        float bestSqrDistance = float.MaxValue;
        Transform closest = null;

        foreach (EnemyRangedController enemy in enemies)
        {
            if (enemy == null)
            {
                continue;
            }

            Transform enemyTransform = enemy.transform;
            if (!enemyTransform.gameObject.activeInHierarchy)
            {
                continue;
            }

            float sqrDistance = (enemyTransform.position - transform.position).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                closest = enemyTransform;
            }
        }

        return closest;
    }
}
