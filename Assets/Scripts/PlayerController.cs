using UnityEngine;

public class PlayerController : MonoBehaviour
{
    Rigidbody _rigidbody;

    [SerializeField]
    float _moveSpeed = 5f;

    Vector2 _movementInput;

    Camera _mainCamera;

    Animator _animator;

    void Awake()
    {
        _mainCamera = Camera.main;
        _movementInput = Vector2.zero;
    }

    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (gameObject.CompareTag("Player 1"))
        {
            float x = 0;
            if (Input.GetKey(KeyCode.D)) x += 1;
            if (Input.GetKey(KeyCode.A)) x -= 1;
            float y = 0;
            if (Input.GetKey(KeyCode.W)) y += 1;
            if (Input.GetKey(KeyCode.S)) y -= 1;
            _movementInput = new Vector2(x, y);
        }
        else if (gameObject.CompareTag("Player 2"))
        {
            float x = 0;
            if (Input.GetKey(KeyCode.RightArrow)) x += 1;
            if (Input.GetKey(KeyCode.LeftArrow)) x -= 1;
            float y = 0;
            if (Input.GetKey(KeyCode.UpArrow)) y += 1;
            if (Input.GetKey(KeyCode.DownArrow)) y -= 1;
            _movementInput = new Vector2(x, y);
        }
    }

    void FixedUpdate()
    {
        if (_movementInput.magnitude > 0)
        {
            Vector3 forward = _mainCamera.transform.forward;
            forward.y = 0;
            forward.Normalize();

            Vector3 right = _mainCamera.transform.right;
            right.y = 0;
            right.Normalize();

            Vector3 movement = right * _movementInput.x + forward * _movementInput.y;
            if (movement != Vector3.zero)
            {
                movement.Normalize();
            }
            _rigidbody.linearVelocity = movement * _moveSpeed;

            _animator.SetInteger("move", 1);
            Debug.Log("Set move to 1");
        }
        else
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _movementInput = Vector2.zero;

            _animator.SetInteger("move", 0);
            Debug.Log("Set move to 0");
        }
    }
}
