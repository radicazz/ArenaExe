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

    [Header("Weapon")]
    [SerializeField] Transform _barrel;
    [SerializeField] GameObject _projectilePrefab;
    [SerializeField, Min(0.05f)] float _fireCooldown = 0.35f;
    [SerializeField, Min(0)] int _maxAmmo = 12;
    [SerializeField, Min(0)] int _startingAmmo = 12;
    [SerializeField] LayerMask _fireLineOfSightMask = ~0;

    [Header("Audio")]
    [SerializeField] AudioClip _fireClip;
    [SerializeField, Range(0f, 1f)] float _fireVolume = 1f;
    [SerializeField] AudioClip _playerOneDamageClip;
    [SerializeField] AudioClip _playerTwoDamageClip;
    [SerializeField, Range(0f, 1f)] float _damageVolume = 1f;

    Transform _closestEnemy;
    float _enemyRefreshTimer;
    Quaternion _movementFrame;
    int _currentAmmo;
    float _fireTimer;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsAlive => _currentHealth > 0f;
    public int CurrentAmmo => _currentAmmo;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        _animator = GetComponentInChildren<Animator>();

        _movementInput = Vector2.zero;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        _maxAmmo = Mathf.Max(0, _maxAmmo);
        _startingAmmo = Mathf.Max(0, _startingAmmo);
        if (_maxAmmo > 0)
        {
            _startingAmmo = Mathf.Min(_startingAmmo, _maxAmmo);
        }
        _currentAmmo = _startingAmmo;
        _fireTimer = 0f;

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
        HandleFiring(phase == GameStateController.GamePhase.Combat);
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

        AudioClip damageClip = null;
        if (CompareTag("Player 1"))
        {
            damageClip = _playerOneDamageClip;
        }
        else if (CompareTag("Player 2"))
        {
            damageClip = _playerTwoDamageClip;
        }

        if (damageClip != null)
        {
            AudioSource.PlayClipAtPoint(damageClip, transform.position, Mathf.Clamp01(_damageVolume));
        }

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

    void HandleFiring(bool canFire)
    {
        _fireTimer = Mathf.Max(0f, _fireTimer - Time.deltaTime);

        if (!canFire || _projectilePrefab == null || _barrel == null)
        {
            return;
        }

        if (_fireTimer > 0f)
        {
            return;
        }

        if (!GetFireInput())
        {
            return;
        }

        if (_currentAmmo <= 0)
        {
            Debug.Log("[PlayerController] Out of ammo.", this);
            return;
        }

        Transform target = _closestEnemy != null && _closestEnemy.gameObject.activeInHierarchy ? _closestEnemy : FindClosestEnemy();
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = target.position + Vector3.up * 0.4f;

        if (!HasLineOfSight(target, targetPosition))
        {
            return;
        }

        FireProjectile(targetPosition);
    }

    bool GetFireInput()
    {
        if (CompareTag("Player 1"))
        {
            return Input.GetKeyDown(KeyCode.F);
        }

        if (CompareTag("Player 2"))
        {
            return Input.GetKeyDown(KeyCode.K);
        }

        Debug.LogError("[PlayerController] Unrecognized player tag for firing input.", this);

        return false;
    }

    bool HasLineOfSight(Transform target, Vector3 targetPosition)
    {
        if (_barrel == null)
        {
            return true;
        }

        Vector3 origin = _barrel.position;
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            return true;
        }

        direction /= distance;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, _fireLineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && target != null && hit.collider.transform.IsChildOf(target))
            {
                return true;
            }

            return false;
        }

        return true;
    }

    void FireProjectile(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - _barrel.position;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            direction = transform.forward;
        }

        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Instantiate(_projectilePrefab, _barrel.position, rotation);

        if (_fireClip != null)
        {
            Vector3 audioPosition = _barrel != null ? _barrel.position : transform.position;
            AudioSource.PlayClipAtPoint(_fireClip, audioPosition, Mathf.Clamp01(_fireVolume));
        }

        _currentAmmo = Mathf.Max(0, _currentAmmo - 1);
        _fireTimer = _fireCooldown;
    }

    public void SetAmmoForWave(int ammoAmount)
    {
        int ammo = Mathf.Max(0, ammoAmount);
        _maxAmmo = Mathf.Max(ammo, _maxAmmo);
        _startingAmmo = ammo;
        _currentAmmo = ammo;
        _fireTimer = 0f;
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
